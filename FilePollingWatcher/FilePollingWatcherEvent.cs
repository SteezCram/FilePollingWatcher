using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilePollingWatcher
{
    /// <summary>
    /// Event of <see cref="FilePollingWatcher"/>.
    /// </summary>
    /// 
    /// <remarks>
    /// Use it as a flag when working. Use it as a simple enum when executing the callback.
    /// </remarks>
    [Flags]
    public enum FilePollingWatcherEvent
    {
        // Base flags
        None = 0,
        Created = 1,
        Deleted = 2,
        DateModified = 4,
        SizeModified = 8,

        // Helper flags
        Modified = DateModified | SizeModified,
        All = Created | Deleted | Modified,
    }
}
