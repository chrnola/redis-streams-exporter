[![Docker Build Status](https://github.com/chrnola/redis-streams-monitor/workflows/Docker/badge.svg)](https://github.com/chrnola/redis-streams-exporter/actions?query=workflow%3ADocker)
[![Latest Docker Tag](https://img.shields.io/docker/v/chrnola/redis-streams-exporter?sort=semver)](https://hub.docker.com/r/chrnola/redis-streams-exporter)
[![Docker Image Size](https://img.shields.io/docker/image-size/chrnola/redis-streams-exporter?sort=semver)](https://hub.docker.com/r/chrnola/redis-streams-exporter)
[![Gitpod ready-to-code](https://img.shields.io/badge/Gitpod-ready--to--code-blue?logo=gitpod)](https://gitpod.io/#https://github.com/chrnola/redis-streams-exporter)
[![License](https://img.shields.io/github/license/chrnola/redis-streams-exporter)](https://github.com/chrnola/redis-streams-exporter/blob/canon/LICENSE)

# Redis Streams Exporter

A tool for exposing data about Redis Streams consumer groups as Prometheus metrics.

## Running

Assuming the Redis you wish to monitor is accessible at `localhost:6379` and the Stream you wish to monitor is named `my-stream-key`:

```bash
$ docker run --env REDIS_STREAM_KEY=my-stream-key -p 3000:3000 -it chrnola/redis-streams-exporter:latest
```

Your Prometheus metrics will be available at `http://localhost:3000/metrics`.
See below for more configuration options.

## Configuration

All configuration is handled via environment variables.

| Environment Variable | Required | Description |
| --- | --- | --- |
| `REDIS_STREAM_KEY` | Yes | The name of the Stream(s) to monitor. Can specify multiple keys by delimiting with a semi-colon, e.g. `key-one;key-two`. |
| `REDIS_CONNECTION_STRING` | No, defaults to "localhost" | Any valid `StackExchange.Redis` connection string, see [docs](https://stackexchange.github.io/StackExchange.Redis/Configuration.html#basic-configuration-strings). |
| `REDIS_DATABASE` | No, defaults to "0" | The Redis database where the Stream to-be-monitored exists. |
| `REDIS_POLL_INTERVAL_MS` | No, defaults to "10000" (10s) | The amount of time that the exporter will wait in between Redis polls. |
| `PROMETHEUS_HOSTNAME` | No, defaults to "+" | The interface to bind the Prometheus server to. |
| `PROMETHEUS_PORT` | No, defaults to "3000". | The TCP port for the Prometheus server to listen on. |
| `APP_ENABLE_VERBOSE_LOGGING` | No, defaults to "false". | If "true", writes detailed log events to stdout. |

## Metrics

Metrics will be exported at varying levels of granularity.

### Stream

```
# HELP redis_stream_length Number of messages in the stream
# TYPE redis_stream_length gauge
redis_stream_length{stream="my-stream-key"} 24601

# HELP redis_stream_earliest_id The epoch timestamp of the earliest message on the stream
# TYPE redis_stream_earliest_id gauge
redis_stream_earliest_id{stream="my-stream-key"} 1597104418874

# HELP redis_stream_latest_id The epoch timestamp of the latest message on the stream
# TYPE redis_stream_latest_id gauge
redis_stream_latest_id{stream="my-stream-key"} 1597152683722

# HELP redis_stream_consumer_groups_total Number of consumer groups for the stream
# TYPE redis_stream_consumer_groups_total gauge
redis_stream_consumer_groups_total{stream="my-stream-key"} 3
```

### Consumer Group
```
# HELP redis_stream_consumer_group_last_delivered_id The epoch timestamp of the last delivered message
# TYPE redis_stream_consumer_group_last_delivered_id gauge
redis_stream_consumer_group_last_delivered_id{stream="my-stream-key",group="group-a"} 1597152683722
redis_stream_consumer_group_last_delivered_id{stream="my-stream-key",group="group-b"} 1597152683722
redis_stream_consumer_group_last_delivered_id{stream="my-stream-key",group="group-c"} 1597152683722

# HELP redis_stream_consumer_group_pending_messages_total Number of pending messages for the group
# TYPE redis_stream_consumer_group_pending_messages_total gauge
redis_stream_consumer_group_pending_messages_total{stream="my-stream-key",group="group-a"} 0
redis_stream_consumer_group_pending_messages_total{stream="my-stream-key",group="group-b"} 0
redis_stream_consumer_group_pending_messages_total{stream="my-stream-key",group="group-c"} 0

# HELP redis_stream_consumer_group_consumers_total Number of consumers in the group
# TYPE redis_stream_consumer_group_consumers_total gauge
redis_stream_consumer_group_consumers_total{stream="my-stream-key",group="group-a"} 1
redis_stream_consumer_group_consumers_total{stream="my-stream-key",group="group-b"} 1
redis_stream_consumer_group_consumers_total{stream="my-stream-key",group="group-c"} 1
```

### Consumer
```
# HELP redis_stream_consumer_pending_messages_total Number of pending messages for the consumer
# TYPE redis_stream_consumer_pending_messages_total gauge
redis_stream_consumer_pending_messages_total{stream="my-stream-key",group="group-a",consumer="dhHXcC1E3"} 0
redis_stream_consumer_pending_messages_total{stream="my-stream-key",group="group-b",consumer="UgfoRw0ew"} 0
redis_stream_consumer_pending_messages_total{stream="my-stream-key",group="group-c",consumer="4gXR54IYg"} 0

# HELP redis_stream_consumer_idle_time_seconds The amount of time for which the consumer has been idle
# TYPE redis_stream_consumer_idle_time_seconds gauge
redis_stream_consumer_idle_time_seconds{stream="my-stream-key",group="group-a",consumer="dhHXcC1E3"} 77.063
redis_stream_consumer_idle_time_seconds{stream="my-stream-key",group="group-b",consumer="UgfoRw0ew"} 77.064
redis_stream_consumer_idle_time_seconds{stream="my-stream-key",group="group-c",consumer="4gXR54IYg"} 77.064
```

## Building

Requires the .NET SDK (6.0):
```bash
$ dotnet build src
```

Alternatively, use the included `Dockerfile`:
```bash
$ docker build .
```
