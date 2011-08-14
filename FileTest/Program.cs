using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime.Serialization.Formatters.Binary;
using log4net;

namespace PhotoSync
{
    class Program
    {
        public static void InitalizeLog4Net()
        {
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net.config"));
            LogManager.GetLogger("Main").InfoFormat("Starting App");
        }
        static void Main(string[] args)
        {
            InitalizeLog4Net();

            string localDirPath = @"C:\Outils\FileTest\Local";
            string masterDirPath = @"C:\Users\Laurent\Pictures\Photos LIGER\";

            var filesEnumerator = new DirectoryInfo(localDirPath).EnumerateFiles("*", SearchOption.AllDirectories);
            int count = filesEnumerator.Count();

            double totalLength = filesEnumerator.Sum(f => f.Length) / 1024.0 / 1024.0;
            Console.WriteLine("{0} fichiers. Taille totale : {1:0.00} Go, Taille moy. : {2:0.00} mo", count, totalLength / 1024, totalLength / count);

            CancellationTokenSource cts = new CancellationTokenSource();
            var breakTask = Task.Factory.StartNew(() => { BreakExecution(cts); }, cts.Token);

            Repository syncDir = new Repository(localDirPath, cts);
            Console.WriteLine();


            // on recommence, mais cette fois pour la synchro vers un autre repertoire
            cts = new CancellationTokenSource();
            breakTask = Task.Factory.StartNew(() => { BreakExecution(cts); }, cts.Token);
            syncDir.SyncWithMaster(masterDirPath, new SyncOptions() { MaxLocalStorage = FileLength.FromGigaBytes(7) }, cts);

            //var doublons = syncDir.Map.Values
            //    .GroupBy(ci => ci.Sha1String)
            //    .Where(g => g.Count() > 1);

            //int totalDoublons = 0;
            //// affichage des doublons : 
            //foreach (var group in doublons)
            //{
            //    foreach (var item in group)
            //    {
            //        Console.WriteLine("{0} : {1}", group.Key, item.FileName);
            //    }
            //    totalDoublons += group.Count();
            //    Console.WriteLine("-------- {0} doublons ---------", group.Count());
            //}

            //Console.WriteLine("-------- {0} doublons au total ---------", totalDoublons);
            Console.WriteLine("Appuyer sur une touche pour fermer cette fenetre.");
            Console.Read();
        }

        
        private static void BreakExecution(CancellationTokenSource cts)
        {
            Console.WriteLine("Appuyer sur Control+B pour arrêter");
            while (true)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    break;
                }

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.B && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        Console.WriteLine("Cancelling...");
                        cts.Cancel();
                        break;
                    }
                }
                Thread.Sleep(250);
            }
        }
    }
}
