using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PhotoSync
{
    public static class FileLength
    {
        public static long FromMegaBytes(double megaBytes)
        {
            return Convert.ToInt64(
                megaBytes
                * 1024 // kB
                * 1024); // B
        }

        public static long FromGigaBytes(double gigaBytes)
        {
            return FromMegaBytes(gigaBytes * 1024);
        }

        public static double ToMegaBytes(double bytes)
        {
            return Convert.ToDouble(bytes) / 1024 / 1024;
        }

        public static double LengthInMegaBytes(this FileInfo fi)
        {
            return ToMegaBytes(fi.Length);
        }
    }
}
