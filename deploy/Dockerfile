# Build context: raíz del repositorio (ver docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY global.json Directory.Build.props Leaderboard.slnx ./
COPY src/ src/
RUN dotnet publish src/Leaderboard.Api -c Release -o /app --no-self-contained

# Imagen chiseled: mínima y sin root por defecto
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Leaderboard.Api.dll"]
