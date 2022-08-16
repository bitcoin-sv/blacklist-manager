# Copyright (c) 2020 Bitcoin Association

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
ARG APPVERSION
WORKDIR /src
COPY ["BlacklistManager/BlacklistManager.API.Rest/BlacklistManager.API.Rest.csproj", "BlacklistManager/BlacklistManager.API.Rest/"]
COPY ["BlacklistManager/BlacklistManager.Infrastructure/BlacklistManager.Infrastructure.csproj", "BlacklistManager/BlacklistManager.Infrastructure/"]
COPY ["BlacklistManager/BlacklistManager.Domain/BlacklistManager.Domain.csproj", "BlacklistManager/BlacklistManager.Domain/"]
COPY ["BlacklistManager/BlacklistManager.Cli/BlacklistManager.Cli.csproj", "BlacklistManager/BlacklistManager.Cli/"]
COPY ["BlacklistManager/BlacklistManager.Database/BlacklistManager.Database.csproj", "BlacklistManager/BlacklistManager.Database/"]
COPY ["Common/Common/Common.csproj", "Common/Common/"]

RUN dotnet restore "BlacklistManager/BlacklistManager.API.Rest/BlacklistManager.API.Rest.csproj"
RUN dotnet restore "BlacklistManager/BlacklistManager.Cli/BlacklistManager.Cli.csproj"
COPY . .
WORKDIR "/src/BlacklistManager/BlacklistManager.API.Rest"
RUN dotnet build "BlacklistManager.API.Rest.csproj" -c Release /p:Version=$APPVERSION -o /app/build
WORKDIR "/src/BlacklistManager/BlacklistManager.Cli"
RUN dotnet build "BlacklistManager.Cli.csproj" -c Release /p:Version=$APPVERSION -o /app/buildcli

FROM build AS publish
ARG APPVERSION
WORKDIR "/src/BlacklistManager/BlacklistManager.API.Rest"
RUN dotnet publish "BlacklistManager.API.Rest.csproj" -c Release /p:Version=$APPVERSION -o /app/publish
WORKDIR "/src/BlacklistManager/BlacklistManager.Cli"
RUN dotnet publish "BlacklistManager.Cli.csproj" -c Release /p:Version=$APPVERSION -o /app/publishcli

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS tools-install
RUN dotnet tool install --tool-path /dotnetcore-tools dotnet-sos
RUN dotnet tool install --tool-path /dotnetcore-tools dotnet-trace
RUN dotnet tool install --tool-path /dotnetcore-tools dotnet-dump
RUN dotnet tool install --tool-path /dotnetcore-tools dotnet-counters

RUN apt-get update \
  && apt-get upgrade -y \
  && apt-get install -y \
     file \
     lldb \
  && rm -rf /var/lib/apt/lists/*

FROM base AS final
WORKDIR /app/cli
COPY --from=publish /app/publishcli .
WORKDIR /app
COPY --from=publish /app/publish .

COPY --from=tools-install /dotnetcore-tools /opt/dotnetcore-tools
ENV PATH="/opt/dotnetcore-tools:${PATH}"
RUN dotnet-sos install

WORKDIR /
COPY ["Deploy/run.sh", "."]

RUN chmod +x run.sh

ENTRYPOINT ["./run.sh"]
