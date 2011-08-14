using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhotoSync
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
