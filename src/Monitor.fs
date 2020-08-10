namespace RedisStreamsMonitor

open System
open StackExchange.Redis

type MonitorConfiguration =
    { PollInterval : TimeSpan
      StreamKey    : string }
with
    static member Default key =
        { PollInterval = TimeSpan.FromSeconds 30.
          StreamKey = key }

type private ConsumerInfo =
    { Group: StreamGroupInfo
      Consumers: StreamConsumerInfo[] }

type DatabaseMonitor(database: IDatabase, config: MonitorConfiguration) =
    /// The key of the Stream to monitor, as a `RedisKey`
    let streamKey = RedisKey config.StreamKey

    /// Async sleep for the amount of time specified via config
    let snooze: Async<unit> =
        config.PollInterval.TotalMilliseconds
        |> Math.Ceiling
        |> int
        |> Async.Sleep

    let getStreamInfo () =
        database.StreamInfoAsync streamKey
        |> Async.AwaitTask

    /// Enumerates all consumer groups on the Stream
    let getGroups () =
        database.StreamGroupInfoAsync streamKey
        |> Async.AwaitTask

    let getConsumerInfo (group: StreamGroupInfo) : Async<ConsumerInfo> = async {
        let groupName' = RedisValue group.Name

        let! consumers =
            database.StreamConsumerInfoAsync(streamKey, groupName')
            |> Async.AwaitTask

        return { Group=group; Consumers=consumers }
    }

    /// The main poll loop that will run indefinitely
    member this.Run() = async {
        let! stream = getStreamInfo ()

        printfn
            "Got stream info|first=%s|last=%s|length=%i|groups=%i"
            (stream.FirstEntry.Id.ToString())
            (stream.LastEntry.Id.ToString())
            stream.Length
            stream.ConsumerGroupCount

        let! groups = getGroups ()
        let! consumers =
            groups
            |> Array.map getConsumerInfo
            |> Async.Parallel

        consumers
        |> Array.iter(fun ci ->
            printfn
                "Got consumer info|group=%s|consumers=%i"
                ci.Group.Name
                ci.Group.ConsumerCount
        )
        // # consumers, last deliv, # pending, name

        do! snooze

        return! this.Run()
    }
