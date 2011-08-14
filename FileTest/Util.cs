using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PhotoSync
{
    public static class Util
    {
        public static string GetRelativePath(this string fullFileName, string rootDir)
        {
            return fullFileName.Remove(0, rootDir.Length).TrimStart('\\');
        }

        public static string GetRelativePath(this FileInfo fi, string rootDir)
        {
            return fi.FullName.GetRelativePath(rootDir);
        }
    }
}
