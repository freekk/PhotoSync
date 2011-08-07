using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FileTest
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
    }
}
