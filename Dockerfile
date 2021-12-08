FROM mcr.microsoft.com/dotnet/sdk:6.0 AS builder

WORKDIR /App

COPY src/ .

RUN dotnet publish -c Release

FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine

COPY --from=builder /App/bin/Release/net6.0/publish/ App/

WORKDIR /App

ENTRYPOINT [ "dotnet", "RedisStreamsMonitor.dll" ]
