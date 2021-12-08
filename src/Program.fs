module RedisStreamsMonitor.Main

open System
open FsConfig
open StackExchange
open Serilog
open Serilog.Formatting.Compact

[<FsConfig.Convention("REDIS", Separator="_")>]
type RedisConfig =
  { StreamKey : string list
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

[<FsConfig.Convention("APP", Separator="_")>]
type AppConfig =
  { [<FsConfig.DefaultValue("false")>]
    EnableVerboseLogging: bool }

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
    let appConfig = parseConfig<AppConfig>()
    use log =
        let logLevel =
            match appConfig.EnableVerboseLogging with
            | true -> Events.LogEventLevel.Debug
            | false -> Events.LogEventLevel.Information
        LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .WriteTo.Console(CompactJsonFormatter()).CreateLogger();

    try
        // Connect to Redis
        let redisConfig = parseConfig<RedisConfig>()
        let parsedRedisConfig = Redis.ConfigurationOptions.Parse(redisConfig.ConnectionString)

        do
            let sanitizedConfig =
                let clone = parsedRedisConfig.Clone()
                if not (String.IsNullOrWhiteSpace clone.Password) then
                    clone.Password <- "***"
                clone
            log.Information("Initializing StackExchange.Redis client with {@config}",
                            sanitizedConfig)

        use redis = Redis.ConnectionMultiplexer.Connect(parsedRedisConfig)
        let db = redis.GetDatabase(int(redisConfig.Database))

        // Start Prometheus server
        let promConfig = parseConfig<PrometheusConfig>()
        use server = new Prometheus.MetricServer(promConfig.Hostname, int(promConfig.Port))
        do server.Start() |> ignore
        log.Information("Prometheus server listening on http://{@hostname}:{@port}",
                        promConfig.Hostname, promConfig.Port)

        let pollInterval =
            redisConfig.PollIntervalMs
            |> float
            |> TimeSpan.FromMilliseconds

        // Start polling loop
        do
            redisConfig.StreamKey
            |> List.map (fun key ->
                let monitorConfig = { MonitorConfiguration.PollInterval = pollInterval; StreamKey = key }

                log.Information("Initializing Stream monitor on database {@db} with {@config}",
                                db, monitorConfig)

                DatabaseMonitor(db, monitorConfig, log).Run()
            )
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore

        // TODO: Trap kill signal, tear down poll loop and server gracefully
        0
    with
    | exn ->
        log.Fatal(exn, "Unhandled exception, application terminating")
        1
