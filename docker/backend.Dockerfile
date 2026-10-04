# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy build configuration files
COPY global.json .
COPY Directory.Build.props .

# Copy project files first for layer caching
COPY ["src/backend/MiniPKI.Api/MiniPKI.Api.csproj", "src/backend/MiniPKI.Api/"]
COPY ["src/backend/MiniPKI.Core/MiniPKI.Core.csproj", "src/backend/MiniPKI.Core/"]
COPY ["src/backend/MiniPKI.Infrastructure/MiniPKI.Infrastructure.csproj", "src/backend/MiniPKI.Infrastructure/"]
RUN dotnet restore "src/backend/MiniPKI.Api/MiniPKI.Api.csproj"

# Copy all source files
COPY src/backend/ ./src/backend/

WORKDIR "/src/src/backend/MiniPKI.Api"
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV STORAGE__DATAPATH=/data
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -sf http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "MiniPKI.Api.dll"]
