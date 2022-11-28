using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilePollingWatcher
{
    /// <summary>
    /// Information get by <see cref="FilePollingWatcher"/>.
    /// </summary>
    public class FilePollingInfo
    {
        /// <summary>
        /// Path of the file.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Size of the file.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Last modified date. Correspond to last write time date.
        /// </summary>
        public DateTime ModifiedDate { get; set; }
    }
}
