# syntax=docker/dockerfile:1

# Use .NET 8 runtime as base image (multi-arch, supports arm64)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

# Use .NET 8 SDK for build stage (multi-arch)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MqttBroker.csproj", "."]
RUN dotnet restore "./MqttBroker.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "./MqttBroker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MqttBroker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MqttBroker.dll"]