# WebGL Threading Patcher

This plugin makes more async await code works in Unity WebGL.
For instance all of following will work:
```charp
task.ContinueWith(t => Debug.Log("ContinuWith works"));

Task.Run(() => Debug.Log("Task.Run works!"));

await task.ConfigureAwait(false);

await Task.Delay(1000);
```

## How Does it work?

This plug-in use IIl2CppProcessor callback to hijack mscorelib and change some methods implementation.
It change ThreadPool methods that enqueue work items to delegate work to SynchronizationContext so all items will be executed in same thread.
Also it patch Timer implementation to use Javascript timer functionality.

## Limitations

All tasks will be executed by just one thread so any blocking calls will freeze whole application. Basically it similar to async await behavior in Blazor. 
