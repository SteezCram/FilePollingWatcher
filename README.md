# FilePollingWatcher
File system watcher using polling.

## Why?
The default file system watcher in .Net is not very reliable. It is based on the underlying OS file system watcher, which is difficult to use correctly. This library provides a simple file system watcher that uses polling to detect changes.

## How to install
Install the NuGet package [FilePollingWatcher](https://www.nuget.org/packages/FilePollingWatcher/).

## How to use
By default the watcher will use these parameters:
* PollingTime = 10,000 ms
* IncludeSubdirectories = false
* Filter = "*"
* FiltersInverted = false
* FilePollingWatcherEvent = FilePollingWatcherEvent.All

Basic usage:
```csharp
FilePollingWatcher fpw = new(FilePollingWatcherEvent.All)
{
    Callback = new Task(events => { /* handle all the files */ }),
    Folder = "my-folder",
    PollingTime = 60000, // 1 minutes
};
fpw.Start();

// later in your code
fpw.Stop();
```

Advanced usage (using filters):
```csharp
FilePollingWatcher fpw = new(FilePollingWatcherEvent.All)
{
    Callback = new Task(events => { /* handle all the files */ }),
    Folder = "my-folder",
    PollingTime = 60000, // 1 minutes
    Filters = new Regex[1] {
        new Regex("file_to_skip.json", RegexOptions.Compiled | RegexOptions.CultureInvariant), // Use compiled regex for better performance
    },
};