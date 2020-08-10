module RedisStreamsMonitor.Main

open System
open StackExchange.Redis

[<EntryPoint>]
let main _argv =
    use redis = ConnectionMultiplexer.Connect("host.docker.internal")
    let db = redis.GetDatabase(0)

    let monitorConfig =
        { MonitorConfiguration.PollInterval = TimeSpan.FromSeconds 5.
          StreamKey = "the-stream" }

    let monitor = DatabaseMonitor(db, monitorConfig)

    // TODO: Trap kill signal, tear down poll loop gracefully
    monitor.Run()
    |> Async.RunSynchronously

    0
