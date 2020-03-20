FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["src/IotTelemetrySimulator/IotTelemetrySimulator.csproj", "src/IotTelemetrySimulator/"]
RUN dotnet restore "src/IotTelemetrySimulator/IotTelemetrySimulator.csproj"
COPY . .
WORKDIR "/src/src/IotTelemetrySimulator"
RUN dotnet build "IotTelemetrySimulator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IotTelemetrySimulator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IotTelemetrySimulator.dll"]