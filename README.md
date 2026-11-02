# dnd-sharp
Collection of D&amp;D functions, and a pokemon mystery dungeon D&amp;D character converter

## Running the Discord bot

The bot needs a Discord token and Git on the machine running it. Set `Discord__Token` in the environment or `Discord:Token` with .NET user secrets, then run:

```powershell
dotnet run --project src/DndSharp.Cli -- discord
```

On startup, the bot automatically clones the [PokéAPI static data repository](https://github.com/PokeAPI/api-data) if it is missing, or pulls the latest changes into an existing clean checkout. The first clone can take time and disk space. The default data path is `./pokeapi-data/data` relative to the working directory; override it with `PokeApi__BasePath` pointing to the `data` directory of an `api-data` checkout. Set `PokeApi__AutoUpdate=false` to skip cloning and updates.

The bot only updates a clean checkout whose origin is the expected PokéAPI repository and whose current branch tracks the origin's default branch. It uses fast-forward-only pulls and does not overwrite local changes or unrelated directories. If Git or the network is unavailable, setup logs a warning and the bot continues; Pokémon commands require usable data, while the other commands can still run.

## Docker

Copy `.env.example` to `.env` and fill in the Discord token and, if using Heart of Daggers campaigns, the activated bot account and IMAP settings. Keep `.env` private. In PowerShell:

```powershell
Copy-Item .env.example .env
# Edit .env before starting the bot.
docker compose -f compose.example.yaml up -d
docker compose -f compose.example.yaml logs -f bot
```

On Linux or macOS, use `cp .env.example .env`. Put passwords containing `$` in single quotes in `.env` so Docker Compose treats them literally. The example pulls `ghcr.io/calico-crusade/dnd-sharp:latest`; set `BOT_IMAGE` in `.env` for a fork or a local build. To build locally, run `docker build -t dndsharp-bot:local .` and set `BOT_IMAGE=dndsharp-bot:local`.

Compose stores the campaign database and Heart of Daggers login session in the `bot-data` volume, and the automatically maintained PokéAPI checkout in the `pokeapi-data` volume. No host checkout or manual clone is required. If you used the previous example's host bind mount, those files remain on the host; the new volume starts its own checkout. Keep the bot-data volume and its backups private because the session authenticates the bot account. `docker compose -f compose.example.yaml down` stops the bot while preserving both volumes; `down -v` deletes them.

The image runs the `discord` verb as a non-root user and contains Git for data setup. The [GitHub workflow](.github/workflows/docker.yaml) builds and publishes the image when a tag such as `v1.2.3.4` is pushed.
