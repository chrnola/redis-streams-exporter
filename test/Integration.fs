module RedisStreamsMonitor.Test.Integration

open StackExchange
open Xunit

type AsyncRedisClient(database: Redis.IDatabase) =
    member __.StreamAdd(key: string, (streamKey, streamValue): string * string, streamId: string option) = async {
        let key' = Redis.RedisKey key
        let streamKey' = Redis.RedisValue streamKey
        let streamValue' = Redis.RedisValue streamValue
        let streamId' = Option.defaultValue "*" streamId
        let streamId'' = streamId' |> Redis.RedisValue |> System.Nullable
        let! resp = database.StreamAddAsync(key', streamKey', streamValue', streamId'') |> Async.AwaitTask
        if not resp.HasValue || resp.IsNullOrEmpty then
            failwithf "Could not create message on stream %s with ID %s. Response: %s" key streamId' (resp.ToString())
        return resp.ToString()
    }

type ScrapedMetric =
    { Prefix : string
      Metric : float }

type PromClient(uri: System.Uri) =
    let client = new System.Net.Http.HttpClient()

    member __.Fetch() = async {
        let! response = client.GetStringAsync(uri) |> Async.AwaitTask

        if System.String.IsNullOrWhiteSpace response then
            failwithf "Empty response from server|uri=%s" (uri.ToString())

        let lines =
            response.Split(System.Environment.NewLine)
            |> Array.filter(System.String.IsNullOrEmpty >> not)
            |> Array.filter(fun line -> not (line.StartsWith("#")))
            |> Array.map(fun line ->
                let spaceSplits = line.Split(' ')
                let metric = spaceSplits |> Array.last |> System.Double.Parse
                let prefix =
                    spaceSplits
                    |> Array.fold(fun sb)
                { ScrapedMetric.Prefix = line; Metric = metric }
            )

        return lines
    }

    interface System.IDisposable with
        member __.Dispose() = client.Dispose()

let [<Fact>] ``My test`` () =
    let conn = "localhost:6379"
    let redisConfig : RedisStreamsMonitor.Config.RedisConfig =
        { StreamKey = "stream"
          ConnectionString = conn
          Database = 0uy
          PollIntervalMs = (System.TimeSpan.FromSeconds 1.).TotalMilliseconds |> uint32 }

    let promPort = 3000
    let promConfig : RedisStreamsMonitor.Config.PrometheusConfig =
        { Hostname = "+"
          Port = uint16 promPort }

    use httpClient = new System.Net.Http.HttpClient()
    let uri = sprintf "http://localhost:%i/metrics" promPort |> System.Uri

    let app = RedisStreamsMonitor.Main.app redisConfig promConfig None

    let complete = ref false
    let checks = async {
        use! red = Redis.ConnectionMultiplexer.ConnectAsync conn |> Async.AwaitTask
        let db = red.GetDatabase(0) // TODO: Use database to run tests concurrently?
        let client = AsyncRedisClient(db)
        let! _ = client.StreamAdd("stream", ("foo", "bar"), None)

        httpClient.GetStringAsync(uri) |> Async.AwaitTask
        complete := true
    }

    try
        let tasks = Async.Parallel [ app; checks ]
        let timeout = (System.TimeSpan.FromSeconds 5.).TotalMilliseconds |> int

        Async.RunSynchronously (tasks, timeout)
        |> ignore
    with
    | :? System.TimeoutException -> ignore()
    | _ -> reraise()

    Assert.True(!complete, "Tests did not complete before timeout")
