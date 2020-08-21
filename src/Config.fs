module RedisStreamsMonitor.Config

let private parseConfig<'T when 'T: not struct> () =
    match FsConfig.EnvConfig.Get<'T>() with
    | Ok config -> config
    | Error error ->
        match error with
        | FsConfig.ConfigParseError.NotFound envVarName ->
            failwithf "Environment variable %s not found" envVarName
        | FsConfig.ConfigParseError.BadValue (envVarName, value) ->
            failwithf "Environment variable %s has invalid value '%s'" envVarName value
        | FsConfig.ConfigParseError.NotSupported msg ->
            failwith msg

[<FsConfig.Convention("REDIS", Separator="_")>]
type RedisConfig =
  { StreamKey : string
    [<FsConfig.DefaultValue("localhost")>]
    ConnectionString : string
    [<FsConfig.DefaultValue("0")>]
    Database : uint8
    [<FsConfig.DefaultValue("10000")>]
    PollIntervalMs : uint32 }
with
    static member ParseFromEnv () =
        parseConfig<RedisConfig> ()

[<FsConfig.Convention("PROMETHEUS", Separator="_")>]
type PrometheusConfig =
  { [<FsConfig.DefaultValue("+")>]
    Hostname : string
    [<FsConfig.DefaultValue("3000")>]
    Port : uint16 }
with
    static member ParseFromEnv () =
        parseConfig<PrometheusConfig> ()

[<FsConfig.Convention("APP", Separator="_")>]
type AppConfig =
  { [<FsConfig.DefaultValue("false")>]
    EnableVerboseLogging: bool }
with
    static member ParseFromEnv () =
        parseConfig<AppConfig> ()
