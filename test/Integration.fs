module RedisStreamsMonitor.Test.Integration

open System
open Xunit

let [<Fact>] ``My test`` () =
    let redisConfig : RedisStreamsMonitor.Config.RedisConfig =
        { StreamKey = "stream"
          ConnectionString = "localhost:6379"
          Database = 0uy
          PollIntervalMs = (TimeSpan.FromSeconds 1.).TotalMilliseconds |> uint32 }

    let promConfig : RedisStreamsMonitor.Config.PrometheusConfig =
        { Hostname = "+"
          Port = 3000us }

    let app = RedisStreamsMonitor.Main.app redisConfig promConfig None

    try
        let tasks = Async.Parallel [ app ]
        let timeout = (TimeSpan.FromSeconds 5.).TotalMilliseconds |> int

        Async.RunSynchronously (tasks, timeout)
        |> ignore
    with
    | :? TimeoutException -> ignore()
    | _ -> reraise()

    Assert.True(true)
