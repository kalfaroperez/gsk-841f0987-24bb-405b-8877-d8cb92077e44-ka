# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /repo
COPY global.json Directory.Build.props Leaderboard.slnx .editorconfig ./
COPY src/ src/
RUN dotnet publish src/Leaderboard.Api -c Release -o /app --no-self-contained \
    && mkdir -p /data

# Chiseled runtime: no shell or package manager, runs as non-root "app" (UID 1654) by default.
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .
# The SQLite volume must be writable by the non-root user; a named volume inherits this ownership on first use.
COPY --from=build --chown=1654:1654 /data /data
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Leaderboard="Data Source=/data/leaderboard.db"
EXPOSE 8080
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "Leaderboard.Api.dll"]
