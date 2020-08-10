module RedisStreamsMonitor.Main

open System
open FsConfig
open StackExchange

[<FsConfig.Convention("REDIS", Separator="_")>]
type RedisConfig =
  { StreamKey : string
    [<FsConfig.DefaultValue("localhost")>]
    ConnectionString : string
    [<FsConfig.DefaultValue("0")>]
    Database : uint8
    [<FsConfig.DefaultValue("10000")>]
    PollIntervalMs : uint32 }

[<FsConfig.Convention("PROMETHEUS", Separator="_")>]
type PrometheusConfig =
  { [<FsConfig.DefaultValue("+")>]
    Hostname : string
    [<FsConfig.DefaultValue("3000")>]
    Port : uint16 }

let parseConfig<'T when 'T: not struct> () =
    match FsConfig.EnvConfig.Get<'T>() with
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
    // Connect to Redis
    let redisConfig = parseConfig<RedisConfig>()
    use redis = Redis.ConnectionMultiplexer.Connect(redisConfig.ConnectionString)
    let db = redis.GetDatabase(int(redisConfig.Database))

    // Start Prometheus server
    let promConfig = parseConfig<PrometheusConfig>()
    use server = new Prometheus.MetricServer(promConfig.Hostname, int(promConfig.Port))
    do server.Start() |> ignore
    printfn "Prometheus server listening on http://%s:%i" promConfig.Hostname promConfig.Port

    // Start polling loop
    let monitorConfig =
        { MonitorConfiguration.PollInterval =
            redisConfig.PollIntervalMs
            |> float
            |> TimeSpan.FromMilliseconds
          StreamKey = redisConfig.StreamKey }

    let monitor = DatabaseMonitor(db, monitorConfig)

    monitor.Run()
    |> Async.RunSynchronously

    // TODO: Trap kill signal, tear down poll loop and server gracefully

    0
