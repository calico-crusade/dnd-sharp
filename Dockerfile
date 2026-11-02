# The container only runs the Discord verb; omit the CLI-only image printing verbs
# and their separate ImageSharp licensing requirement from this build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ ./src/
RUN dotnet publish src/DndSharp.Cli/DndSharp.Cli.csproj \
    --configuration Release --output /publish -p:BotOnly=true --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates git libfontconfig1 libfreetype6 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/dndsharp /var/lib/pokeapi \
    && chown app:app /var/lib/dndsharp /var/lib/pokeapi
WORKDIR /app
COPY --from=build /publish/ ./
ENV Discord__GuildId="" \
    Discord__DatabasePath=/var/lib/dndsharp/campaigns.sqlite3 \
    HeartOfDaggers__SessionPath=/var/lib/dndsharp/heart-of-daggers-session.json \
    PokeApi__BasePath=/var/lib/pokeapi/api-data/data
USER app
ENTRYPOINT ["dotnet", "DndSharp.Cli.dll", "discord"]
