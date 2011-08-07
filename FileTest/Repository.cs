using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using log4net;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FileTest
{
    public class Repository
    {
        private const string TempFileName = "file_copy.tmp";
        private const string SnapshotFileName = "sync_dir.data";

        private static ILog _logger = LogManager.GetLogger(typeof(Repository));

        private string _rootPath;

        public string RootPath
        {
            get { return _rootPath; }
            set 
            {
                _rootPath = value;
                LocalTempFileName = Path.Combine(_rootPath, TempFileName);
            }
        }

        public string LocalTempFileName { get; set; }

        public ConcurrentDictionary<string, CustomInfo> Map { get; private set; }

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="rootPath"></param>
        public Repository(string rootPath, CancellationTokenSource cts)
        {
            this.RootPath = rootPath;
            LoadSnapshot();
            Refresh(cts);
        }

        #region Chargement/Sauvegarde sur disque
        private void LoadSnapshot()
        {
            var list = DeserializeSnapshot(this.RootPath);
            this.Map = new ConcurrentDictionary<string, CustomInfo>();
            foreach (var elt in list)
            {
                if (File.Exists(elt.FileName))
                {
                    // on ne recharge que les fichiers toujours présents
                    Map.GetOrAdd(elt.FileName, elt);
                }
            }
        }

        private static List<CustomInfo> DeserializeSnapshot(string rootPath)
        {
            Directory.SetCurrentDirectory(rootPath);
            string snapshotPath = Path.Combine(rootPath, SnapshotFileName);

            if (!File.Exists(snapshotPath) || new FileInfo(snapshotPath).Length == 0)
            {
                _logger.DebugFormat("Aucun snapshot dans : {0}", snapshotPath);
                return new List<CustomInfo>();
            }

            try
            {
                using (var stream = File.OpenRead(snapshotPath))
                {
                    BinaryFormatter f = new BinaryFormatter();
                    return (List<CustomInfo>)f.Deserialize(stream);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Erreur lors de la désérialisation du Snapshot : " + ex);
                return new List<CustomInfo>();
            }
        }

        private void SaveSnapshot()
        {
            string snapshotPath = Path.Combine(this.RootPath, SnapshotFileName);
            File.Delete(snapshotPath);
            using (var stream = File.OpenWrite(snapshotPath))
            {
                BinaryFormatter f = new BinaryFormatter();
                f.Serialize(stream, this.Map.Values.ToList());
            }
        }
        #endregion

        private void Refresh(CancellationTokenSource cts)
        {
            var snapshotTask = Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    SaveSnapshot();
                    if (cts.Token.IsCancellationRequested)
                    {
                        break;
                    }
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            },
                cts.Token);

            var hashTask = Task.Factory.StartNew(() =>
            {
                Stopwatch watch = Stopwatch.StartNew();
                var filesEnumerator = new DirectoryInfo(this.RootPath).EnumerateFiles("*", SearchOption.AllDirectories);
                
                int repoFileCount = filesEnumerator.Count();
                _logger.InfoFormat("Refresh du repository '{0}' : {1} fichiers ({2:0.00} go).", 
                    this.RootPath, 
                    repoFileCount,
                    filesEnumerator.Sum(f => Convert.ToDouble(f.Length)) / 1024 / 1024 / 1024);

                Stopwatch logWatch = Stopwatch.StartNew();
                int index = 0;
                foreach (var fi in filesEnumerator)
                {
                    if (fi.Name == SnapshotFileName)
                    {
                        // on saute le fichier de snapshot
                        continue;
                    }

                    string relativeFileName = fi.GetRelativePath(this.RootPath);
                    Map.AddOrUpdate(relativeFileName,
                        _ => new CustomInfo(relativeFileName, fi),
                       (_, info) => info.Update(fi));

                    if (cts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    if (logWatch.ElapsedMilliseconds >= 5000)
                    {
                        _logger.InfoFormat("Refresh : {0} %", Convert.ToDouble(index)* 100d / repoFileCount);
                        logWatch.Reset();
                        logWatch.Start();
                    }

                    index++;
                }

                cts.Cancel();
                return watch.Elapsed;
            }, cts.Token);

            Task.WaitAll(snapshotTask, hashTask);
        }

        public void SyncWithMaster(string masterDir, SyncOptions options, CancellationTokenSource cts)
        {
            var master = new Repository(masterDir, cts);

            // hasardeux
            var task = Task.Factory
                .StartNew(_ => PushTo(master, cts), cts)
                .ContinueWith(_ => PullFrom(master, options ?? SyncOptions.Default(), cts), TaskContinuationOptions.OnlyOnRanToCompletion)
                .ContinueWith(_ => cts.Cancel());

            task.Wait();
        }

        public void PushTo(string otherDir, CancellationTokenSource cts)
        {
            PushTo(new Repository(otherDir, cts), cts);
        }

        private void PullFrom(Repository master, SyncOptions options, CancellationTokenSource cts)
        {
            var mapByHash = Map.Values.ToDictionary(ci => ci.Sha1String);

            var filesToCopy = master.Map.Values
                .OrderBy(ci => ci.LastWriteTime)
                .Reverse();

            if (options.MaxLocalStorage != null)
            {
                long totalSize = 0;

                filesToCopy = filesToCopy
                    .TakeWhile(ci =>
                    {
                        totalSize += ci.Length;
                        return totalSize < options.MaxLocalStorage;
                    });


                // purge des fichiers en trop pour le repo local
                var mapToCopy = filesToCopy.ToDictionary(f => f.FileName);
                int countDeleted = 0;
                foreach (var f in Map.Values)
                {
                    if (mapToCopy.ContainsKey(f.FileName) == false)
                    {
                        // suppression du fichier
                        File.Delete(Path.Combine(this.RootPath, f.FileName));
                        countDeleted++;
                    }
                }

                _logger.InfoFormat("{0} fichiers supprimés pour respecter la taille max du repository : {1}", countDeleted, this.RootPath);
            }

            foreach (var f in filesToCopy)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    break;
                }

                if (!mapByHash.ContainsKey(f.Sha1String))
                {
                    this.CopyFrom(master, f);
                }
            }
        }

        private void PushTo(Repository other, CancellationTokenSource cts)
        {
            var newFiles = from info in Map.Values
                           where other.Map.ContainsKey(info.FileName) == false
                           select info;

            var modifiedFiles = from info in Map.Values
                                from otherInfo in other.Map.Values
                                where otherInfo.FileName == info.FileName
                                     && otherInfo.Sha1String != info.Sha1String
                                     && info.LastWriteTime > otherInfo.LastWriteTime
                                select info;

            var filesToCopy = newFiles.Union(modifiedFiles);

            foreach (var f in filesToCopy)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    break;
                }
                other.CopyFrom(this, f);
            }
        }

        /// <summary>
        /// Copie atomique vers ce Repository
        /// </summary>
        private void CopyFrom(Repository otherRepo, CustomInfo f)
        {
            string srcPath = Path.Combine(otherRepo.RootPath, f.FileName);
            string destPath = Path.Combine(this.RootPath, f.FileName);

            File.Copy(srcPath, LocalTempFileName, true);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
            File.Move(LocalTempFileName, destPath);
            
            // ajout dans le repo local
            this.Map.GetOrAdd(f.FileName, f);
        }
    }
}
