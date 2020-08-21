module RedisStreamsMonitor.Main

open System
open StackExchange
open Serilog
open Serilog.Formatting.Compact

let app (redisConfig: Config.RedisConfig) (promConfig: Config.PrometheusConfig) (log: Serilog.ILogger option) = async {
    // Connect to Redis
    let parsedRedisConfig = Redis.ConfigurationOptions.Parse(redisConfig.ConnectionString)

    do
        let sanitizedConfig =
            let clone = parsedRedisConfig.Clone()
            if not (String.IsNullOrWhiteSpace clone.Password) then
                clone.Password <- "***"
            clone

        log |> Option.iter (fun logger ->
            logger.Information("Initializing StackExchange.Redis client with {@config}",
                            sanitizedConfig))

    use redis = Redis.ConnectionMultiplexer.Connect(parsedRedisConfig)
    let db = redis.GetDatabase(int(redisConfig.Database))

    // Start Prometheus server
    use server = new Prometheus.MetricServer(promConfig.Hostname, int(promConfig.Port))
    do server.Start() |> ignore

    log |> Option.iter (fun logger ->
        logger.Information("Prometheus server listening on http://{@hostname}:{@port}",
                           promConfig.Hostname, promConfig.Port))

    // Start polling loop
    let monitorConfig =
        { MonitorConfiguration.PollInterval =
            redisConfig.PollIntervalMs
            |> float
            |> TimeSpan.FromMilliseconds
          StreamKey = redisConfig.StreamKey }

    log |> Option.iter (fun logger ->
        logger.Information ("Initializing Stream monitor on database {@db} with {@config}", db, monitorConfig))

    let monitor = DatabaseMonitor(db, monitorConfig, ?logger=log)

    return! monitor.Run()
}

[<EntryPoint>]
let main _argv =
    let appConfig = Config.AppConfig.ParseFromEnv ()
    use log =
        let logLevel =
            match appConfig.EnableVerboseLogging with
            | true -> Events.LogEventLevel.Debug
            | false -> Events.LogEventLevel.Information
        LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .WriteTo.Console(CompactJsonFormatter()).CreateLogger()

    try
        let redisConfig = Config.RedisConfig.ParseFromEnv ()
        let promConfig = Config.PrometheusConfig.ParseFromEnv ()
        let logger = log :> Serilog.ILogger

        app redisConfig promConfig (Some logger)
        |> Async.RunSynchronously

        // TODO: Trap kill signal, tear down poll loop and server gracefully
        0
    with
    | exn ->
        log.Fatal(exn, "Unhandled exception, application terminating")
        1
