module RedisStreamsMonitor.Main

open System
open FsConfig
open StackExchange

[<FsConfig.ConventionAttribute("REDIS", Separator="_")>]
type RedisConfig =
  { [<FsConfig.DefaultValue("localhost")>]
    ConnectionString: string
    [<FsConfig.DefaultValue("0")>]
    Database: uint8
    StreamKey: string
    [<FsConfig.DefaultValue("10000")>]
    PollIntervalMs: uint32 }
with
  static member Parse () =
      match FsConfig.EnvConfig.Get<RedisConfig>() with
      | Ok config -> config
      | Error error ->
        match error with
        | ConfigParseError.NotFound envVarName ->
          failwithf "Environment variable %s not found" envVarName
        | ConfigParseError.BadValue (envVarName, value) ->
          failwithf "Environment variable %s has invalid value '%s'" envVarName value
        | ConfigParseError.NotSupported msg ->
          failwith msg

[<EntryPoint>]
let main _argv =
    let config = RedisConfig.Parse()

    use redis = Redis.ConnectionMultiplexer.Connect(config.ConnectionString)
    let db = redis.GetDatabase(int(config.Database))

    let monitorConfig =
        { MonitorConfiguration.PollInterval =
            config.PollIntervalMs
            |> float
            |> TimeSpan.FromMilliseconds
          StreamKey = config.StreamKey }

    let monitor = DatabaseMonitor(db, monitorConfig)

    monitor.Run()
    |> Async.RunSynchronously

    // TODO: Trap kill signal, tear down poll loop gracefully

    0
