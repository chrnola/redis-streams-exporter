# Redis Streams Exporter

A tool for monitoring Redis Streams consumer groups.

## Building

Requires the .NET Core SDK (3.1):
```
$ dotnet build src
```

Alternatively, use the included `Dockerfile`:
```
$ docker build .
```

## Configuration

| Environment Variable | Required | Description |
| --- | --- | --- |
| `REDIS_CONNECTION_STRING` | No, defaults to "localhost" | Any valid `StackExchange.Redis` connection string, see [docs](https://stackexchange.github.io/StackExchange.Redis/Configuration.html#basic-configuration-strings). |
| `REDIS_STREAM_KEY` | Yes | The name of the Stream to monitor. |
| `REDIS_DATABASE` | No, defaults to 0 | The Redis database where the Stream to-be-monitored exists. |
| `REDIS_POLL_INTERVAL_MS` | No, defaults to 10000 (10s) | The amount of time that the exporter will wait in between Redis polls. |
