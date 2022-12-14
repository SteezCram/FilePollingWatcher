using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FilePollingWatcher
{
    /// <summary>
    /// File system watcher using polling.
    /// </summary>
    /// 
    /// <remarks>
    /// Due to polling, "Rename" event is not achievable. You cannot track renamed file.
    /// Each file rename will be break into 2 events: Deleted (old file name) and Created (new file name).
    /// </remarks>
    /// 
    /// <example>
    /// FilePollingWatcher fpw = new(FilePollingWatcherEvent.All)
    /// {
    ///     Callback = new Task(events => { /* handle all the files */ }),
    ///     Folder = "my-folder",
    ///     PollingTime = 60000, // 1 minutes
    /// };
    /// fpw.Start();
    /// 
    /// // later in your code
    /// 
    /// fpw.Stop();
    /// </example>
    /// 
    /// <seealso cref="FileSystemWatcher"/>
    public class FilePollingWatcher : IDisposable
    {
        /// <summary>
        /// Callback to execute when the file watcher has found a modifiction.
        /// </summary>
        public Action<IEnumerable<FilePollingWatcherEventArgs>> Callback { get; init; }

        /// <summary>
        /// Callback to execute in async mode when the file watcher has found a modifiction.
        /// </summary>
        public Func<IEnumerable<FilePollingWatcherEventArgs>, Task> CallbackAsync { get; init; }

        /// <summary>
        /// Events to handle.
        /// </summary>
        ///  
        /// <remarks>
        /// Default event = FilePollingWatcherEvent.All
        /// </remarks>
        public FilePollingWatcherEvent FilePollingWatcherEvent { get; init; }

        /// <summary>
        /// Filters regex pattern.
        /// </summary>
        public Regex[] Filters { get; init; }

        /// <summary>
        /// Invert the filters regex or not. By default, the filters act as a whitelist. Turn this option to true for the filters to act as a blacklist.
        /// </summary>
        public bool FiltersInverted { get; init; }

        /// <summary>
        /// Folder to watch.
        /// </summary>
        public string Folder { get; init; }

        /// <summary>
        /// Include or not sub directories.
        /// </summary>
        public bool IncludeSubdirectories { get; init; }

        /// <summary>
        /// Polling time between 2 checks in ms.
        /// </summary>
        /// 
        /// <remarks>
        /// Default polling time = 10 seconds
        /// </remarks>
        public int PollingTime { get; init; }


        private readonly CancellationTokenSource _cts;
        private Thread _worker;
        private Task _workerTask;

        private readonly bool _initTriggerEvent;
        

        /// <summary>
        /// File system watcher using polling.
        /// </summary>
        /// 
        /// <param name="initTriggerEvent">Send or not an init trigger event. All the files found will be send as parameters with an event type of <see cref="FilePollingWatcherEvent.Created"/>. It is useful if you need to instanciate something before using the other events.</param>
        public FilePollingWatcher(bool initTriggerEvent = false)
        {
            _cts = new CancellationTokenSource();
            _initTriggerEvent = initTriggerEvent;

            FilePollingWatcherEvent = FilePollingWatcherEvent is FilePollingWatcherEvent.None ? FilePollingWatcherEvent.All : FilePollingWatcherEvent;
            Filters = Filters is null ? Array.Empty<Regex>() : Filters;
            PollingTime = PollingTime is 0 ? 10000 : PollingTime;
        }


        public async void Dispose()
        {
            if (_worker is not null) Stop();
            if (_workerTask is not null) await StopAsync();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start the file watcher.
        /// </summary>
        public void Start()
        {
            if (_workerTask is not null) return;
            if (_worker is not null) return;

            _worker = new Thread(() => Work(_cts.Token));
            _worker.Start();
        }

        /// <summary>
        /// Start the file watcher.
        /// </summary>
        public void StartAsync()
        {
            if (_workerTask is not null) return;
            if (_worker is not null) return;

            _worker = new(() => Work(_cts.Token));
            _worker.Start();
        }

        /// <summary>
        /// Stop the file watcher. Wait the worker to be stopped.
        /// </summary>
        public void Stop()
        {
            if (_worker is null) return;

            _cts.Cancel();
            _worker.Join();
        }

        /// <summary>
        /// Stop the file watcher. Wait the worker to be stopped.
        /// </summary>
        /// 
        /// <returns>Task to wait</returns>
        public async Task StopAsync()
        {
            if (_workerTask is null) return;

            _cts.Cancel();
            await Task.WhenAll(_workerTask);
        }


        /// <summary>
        /// Worker loop.
        /// </summary>
        /// 
        /// <param name="token">Cancellation token to stop the worker</param>
        private async void Work(CancellationToken token)
        {
            Dictionary<string, FilePollingInfo> cache = _Init();

            if (_initTriggerEvent)
            {
                IEnumerable<FilePollingWatcherEventArgs> events = cache.Select(x => new FilePollingWatcherEventArgs()
                {
                    Event = FilePollingWatcherEvent.None,
                    FileInfo = x.Value,
                });

                Callback?.Invoke(events);
                await CallbackAsync?.Invoke(events);
            }

            while (true)
            {
                // Exit the worker if the token is cancelled
                if (token.IsCancellationRequested) return;

                Thread.Sleep(PollingTime);

                List<FilePollingWatcherEventArgs> events = new();

                if (FilePollingWatcherEvent.HasFlag(FilePollingWatcherEvent.Created))
                    _UpdateCreated(ref cache, ref events);

                if (FilePollingWatcherEvent.HasFlag(FilePollingWatcherEvent.Deleted))
                    _UpdateDeleted(ref cache, ref events);

                if (FilePollingWatcherEvent.HasFlag(FilePollingWatcherEvent.DateModified) || FilePollingWatcherEvent.HasFlag(FilePollingWatcherEvent.SizeModified))
                    _UpdateModified(ref cache, ref events);


                Callback?.Invoke(events);
                await CallbackAsync?.Invoke(events);

                // Force memory GC
                events.Clear();
            }
        }


        /// <summary>
        /// Create the base dictionary.
        /// </summary>
        /// 
        /// <returns>Initialization of the cache dictionary</returns>
        private Dictionary<string, FilePollingInfo> _Init()
        {
            return Directory.EnumerateFiles(Folder, "*.*", IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(x => _Validate(x))
                .ToDictionary(x => x, x => new FilePollingInfo()
                {
                    Path = x,
                    Size = new FileInfo(x).Length, // TO:DO accelerate this
                    ModifiedDate = File.GetLastWriteTimeUtc(x),
                });
        }

        /// <summary>
        /// Validate a file path. Verify if the file is match by at least one exclusion regex.
        /// </summary>
        /// 
        /// <param name="file">String file path</param>
        /// 
        /// <returns>True if the file is valid, else false</returns>
        private bool _Validate(string file)
        {
            int filtersLength = Filters.Length;

            if (filtersLength == 0) return true;

            bool isMatch = false;

            for (int i = 0; i < filtersLength; i++)
            {
                if (Filters[i].IsMatch(file))
                {
                    isMatch = true;
                    break;
                }
            }

            return FiltersInverted ? !isMatch : isMatch;
        }

        /// <summary>
        /// Update the cache dictionary. Search for new file.
        /// </summary>
        /// 
        /// <param name="cache">Cache dictionary to update</param>
        /// <param name="events">Events list to update</param>
        private void _UpdateCreated(ref Dictionary<string, FilePollingInfo> cache, ref List<FilePollingWatcherEventArgs> events)
        {
            foreach (string file in Directory.EnumerateFiles(Folder, "*.*", IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                if (cache.ContainsKey(file) || !_Validate(file)) continue;

                using FileStream fs = new(file, FileMode.Open, FileAccess.Read);

                FilePollingInfo fpi = new()
                {
                    Path = file,
                    Size = fs.Length,
                    ModifiedDate = File.GetLastWriteTimeUtc(file),
                };

                events.Add(new()
                {
                    Event = FilePollingWatcherEvent.Created,
                    FileInfo = fpi,
                });

                cache.Add(file, fpi);
            }
        }

        /// <summary>
        /// Update the cache dictionary. Search for deleted file.
        /// </summary>
        /// 
        /// <param name="cache">Cache dictionary to update</param>
        /// <param name="events">Events list to update</param>
        private void _UpdateDeleted(ref Dictionary<string, FilePollingInfo> cache, ref List<FilePollingWatcherEventArgs> events)
        {
            int cacheCount = cache.Count - 1;

            // Reverse looping since we remove item
            for (int i = cacheCount; i >= 0; i--)
            {
                KeyValuePair<string, FilePollingInfo> kv = cache.ElementAt(i);

                if (File.Exists(kv.Value.Path))
                    continue;

                events.Add(new()
                {
                    Event = FilePollingWatcherEvent.Deleted,
                    FileInfo = kv.Value,
                });
                cache.Remove(kv.Key);
            }
        }

        /// <summary>
        /// Update the cache dictionary. Search for date modified file and size modified file.
        /// </summary>
        /// 
        /// <param name="cache">Cache dictionary to update</param>
        /// <param name="events">Events list to update</param>
        private void _UpdateModified(ref Dictionary<string, FilePollingInfo> cache, ref List<FilePollingWatcherEventArgs> events)
        {
            foreach (string k in cache.Keys)
            {
                bool handled = false;

                if (FilePollingWatcherEvent.HasFlag(FilePollingWatcherEvent.DateModified))
                {
                    DateTime kDt = File.GetLastWriteTimeUtc(k);

                    if (DateTime.Compare(cache[k].ModifiedDate, kDt) == 0)
                        continue;

                    handled = true;
                    cache[k].ModifiedDate = kDt;

                    events.Add(new()
                    {
                        Event = FilePollingWatcherEvent.DateModified,
                        FileInfo = cache[k],
                    });
                }

                // Search for size modification after date modification since streams are slower
                if (FilePollingWatcherEvent.HasFlag(FilePollingWatcherEvent.SizeModified) && !handled)
                {
                    using FileStream fs = new(k, FileMode.Open, FileAccess.Read);

                    if (cache[k].Size == fs.Length)
                        continue;

                    cache[k].Size = fs.Length;

                    events.Add(new()
                    {
                        Event = FilePollingWatcherEvent.SizeModified,
                        FileInfo = cache[k],
                    });
                }
            }
        }
    }
}
