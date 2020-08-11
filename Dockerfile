FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS builder

WORKDIR /App

COPY src/ .

RUN dotnet publish -c Release

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-alpine

COPY --from=builder /App/bin/Release/netcoreapp3.1/publish/ App/

WORKDIR /App

ENTRYPOINT [ "dotnet", "RedisStreamsMonitor.dll" ]
