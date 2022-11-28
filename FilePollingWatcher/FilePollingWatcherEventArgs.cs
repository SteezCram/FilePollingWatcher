using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilePollingWatcher
{
    /// <summary>
    /// Event class send by <see cref="FilePollingWatcher"/>.
    /// </summary>

    public class FilePollingWatcherEventArgs
    {
        /// <summary>
        /// Event type.
        /// </summary>
        public FilePollingWatcherEvent Event { get; init; }

        /// <summary>
        /// File info get by the watcher.
        /// </summary>
        public FilePollingInfo FileInfo { get; init; }
    }
}
