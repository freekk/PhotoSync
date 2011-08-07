using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using log4net;

namespace FileTest
{
    [Serializable]
    public class CustomInfo
    {
        private static ILog _logger = LogManager.GetLogger(typeof(CustomInfo));

        public string FileName { get; set; }
        public long Length { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public byte[] SHA1 { get; set; }
        public string Sha1String { get; set; }

        /// <summary>
        /// Ctor.
        /// </summary>
        public CustomInfo(string relativeFileName, FileInfo fi)
        {
            this.FileName = relativeFileName;
            Update(fi);
        }

        public CustomInfo Update(FileInfo fi)
        {
            if (fi.Length == this.Length
                && fi.LastWriteTimeUtc == this.LastWriteTime
                && this.SHA1.Length > 0 && this.Sha1String != null)
            {
                // le fichier n'a pas changé
                _logger.Debug(this.FileName + " : ok");
                return this;
            }

            this.Length = fi.Length;
            this.CreationTime = fi.CreationTimeUtc;
            this.LastWriteTime = fi.LastWriteTimeUtc;

            using (SHA1 hash = System.Security.Cryptography.SHA1.Create())
            using (var stream = fi.OpenRead())
            {
                _logger.Debug(this.FileName + " : computing SHA1");
                this.SHA1 = hash.ComputeHash(stream);
                this.Sha1String = BitConverter.ToString(this.SHA1);
            }
            return this;
        }
    }
}
