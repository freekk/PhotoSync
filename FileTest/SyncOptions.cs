using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FileTest
{
    public class SyncOptions
    {
        public long? MaxLocalStorage { get; set; }

        //public TimeSpan? LocalStorageSpan { get; set; }

        public static SyncOptions Default()
        {
            return new SyncOptions();
        }
    }
}
