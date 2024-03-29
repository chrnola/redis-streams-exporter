namespace RedisStreamsMonitor

open System
open StackExchange.Redis
open Prometheus

type MonitorConfiguration =
    { PollInterval : TimeSpan
      StreamKey : string }

type private ConsumerInfo =
    { Group: StreamGroupInfo
      Consumers: StreamConsumerInfo[] }

type DatabaseMonitor(database: IDatabase, config: MonitorConfiguration, ?logger: Serilog.ILogger) =
    /// The key of the Stream to monitor, as a `RedisKey`
    let streamKey = RedisKey config.StreamKey

    /// Async sleep for the amount of time specified via config
    let snooze: Async<unit> =
        config.PollInterval.TotalMilliseconds
        |> Math.Ceiling
        |> int
        |> Async.Sleep

    let doesKeyExist () = async {
        let! numKeys =
            database.KeyExistsAsync [| streamKey |]
            |> Async.AwaitTask
        return numKeys = 1L
    }

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

    static let streamLabels = [| "stream" |]
    static let streamLength =
        Metrics.CreateGauge("redis_stream_length",
                            "Number of messages in the stream",
                            GaugeConfiguration(LabelNames=streamLabels))
    static let totalGroups =
        Metrics.CreateGauge("redis_stream_consumer_groups_total",
                            "Number of consumer groups for the stream",
                            GaugeConfiguration(LabelNames=streamLabels))
    static let earliestMessage =
        Metrics.CreateGauge("redis_stream_earliest_id",
                            "The epoch timestamp of the earliest message on the stream",
                            GaugeConfiguration(LabelNames=streamLabels))
    static let latestMessage =
        Metrics.CreateGauge("redis_stream_latest_id",
                            "The epoch timestamp of the latest message on the stream",
                            GaugeConfiguration(LabelNames=streamLabels))

    static let groupLabels = [| "stream"; "group" |]
    static let totalConsumers =
        Metrics.CreateGauge("redis_stream_consumer_group_consumers_total",
                            "Number of consumers in the group",
                            GaugeConfiguration(LabelNames=groupLabels))

    static let pendingMessages =
        Metrics.CreateGauge("redis_stream_consumer_group_pending_messages_total",
                            "Number of pending messages for the group",
                            GaugeConfiguration(LabelNames=groupLabels))

    static let lastDelivered =
        Metrics.CreateGauge("redis_stream_consumer_group_last_delivered_id",
                            "The epoch timestamp of the last delivered message",
                            GaugeConfiguration(LabelNames=groupLabels))

    static let consumerLabels = [| "stream"; "group"; "consumer" |]
    static let consumerPendingMessages =
        Metrics.CreateGauge("redis_stream_consumer_pending_messages_total",
                            "Number of pending messages for the consumer",
                            GaugeConfiguration(LabelNames=consumerLabels))
    static let idleTime =
        Metrics.CreateGauge("redis_stream_consumer_idle_time_seconds",
                            "The amount of time for which the consumer has been idle",
                            GaugeConfiguration(LabelNames=consumerLabels))

    /// By convention, the ID of a message in a Redis Stream follows the form:
    ///
    ///   [epochTimeMs]-[uniquifier]
    ///
    /// This function returns the parsed epoch time. Returning `Some`
    /// if it can be parsed, and otherwise `None`.
    let tryParseTimestamp (redisId: string) : DateTimeOffset option =
        match redisId with
        | null ->
            None
        | redisId' ->
            match redisId'.Split('-') with
            | [| time; _uniqueifier |] ->
                match Int64.TryParse time with
                | (false, _) ->
                    None
                | (true, num) ->
                    num
                    |> DateTimeOffset.FromUnixTimeMilliseconds
                    |> Some
            | _ ->
                None

    let recordStreamLevelMetrics (stream: StreamInfo) =
        streamLength.WithLabels(config.StreamKey).Set(stream.Length |> float)
        totalGroups.WithLabels(config.StreamKey).Set(stream.ConsumerGroupCount |> float)

        match tryParseTimestamp (stream.FirstEntry.Id.ToString()) with
        | Some ts ->
            ts.ToUnixTimeMilliseconds()
            |> float
            |> earliestMessage.WithLabels(config.StreamKey).Set
        | None ->
            ignore ()

        match tryParseTimestamp (stream.LastEntry.Id.ToString()) with
        | Some ts ->
            ts.ToUnixTimeMilliseconds()
            |> float
            |> latestMessage.WithLabels(config.StreamKey).Set
        | None ->
            ignore ()

    let recordGroupLevelMetrics (sgi: StreamGroupInfo) =
        let stream = config.StreamKey
        let group = sgi.Name

        totalConsumers.WithLabels(stream, group).Set(sgi.ConsumerCount |> float)
        pendingMessages.WithLabels(stream, group).Set(sgi.PendingMessageCount |> float)

        match tryParseTimestamp sgi.LastDeliveredId with
        | Some ts ->
            ts.ToUnixTimeMilliseconds()
            |> float
            |> lastDelivered.WithLabels(stream, group).Set
        | None ->
            ignore ()

    let recordConsumerLevelMetrics groupName (sci: StreamConsumerInfo) =
        let stream = config.StreamKey
        consumerPendingMessages.WithLabels(stream, groupName, sci.Name).Set(sci.PendingMessageCount |> float)
        let idle = sci.IdleTimeInMilliseconds |> float |> TimeSpan.FromMilliseconds
        idleTime.WithLabels(stream, groupName, sci.Name).Set(idle.TotalSeconds)

    let pollForMetrics () = async {
        let! stream = getStreamInfo ()
        do recordStreamLevelMetrics stream
        do logger
           |> Option.iter (fun log ->
                log.Debug("Got stream info {@stream} {@first} {@last} {@length} {@groupCount} {@lastGeneratedId}",
                          config.StreamKey,
                          stream.FirstEntry.Id.ToString(),
                          stream.LastEntry.Id.ToString(),
                          stream.Length,
                          stream.ConsumerGroupCount,
                          stream.LastGeneratedId.ToString()))

        let! groups = getGroups ()
        let! consumers =
            groups
            |> Array.map getConsumerInfo
            |> Async.Parallel

        consumers
        |> Array.iter(fun ci ->
            do recordGroupLevelMetrics ci.Group
            do logger
               |> Option.iter (fun log ->
                    log.Debug("Got group info {@stream} {@name} {@consumerCount} {@pendingCount} {@lastDeliveredId}",
                              config.StreamKey, ci.Group.Name, ci.Group.ConsumerCount, ci.Group.PendingMessageCount,
                              ci.Group.LastDeliveredId))

            ci.Consumers
            |> Array.iter(fun c ->
                do recordConsumerLevelMetrics ci.Group.Name c
                do logger
                   |> Option.iter (fun log ->
                        log.Debug("Got consumer info {@stream} {@name} {@pendingCount} {@idleMs}",
                                  config.StreamKey, c.Name, c.PendingMessageCount, c.IdleTimeInMilliseconds))
            )
        )
    }

    /// The main poll loop that will run indefinitely
    member this.Run(): Async<unit> = async {
        match! doesKeyExist () with
        | true ->
            do! pollForMetrics ()
        | false ->
            do logger
               |> Option.iter (fun log ->
                    log.Debug("Key {@key} does not exist, snoozing", config.StreamKey))
        do! snooze
        return! this.Run()
    }
