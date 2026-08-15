# ShortenLink

[![CI](https://github.com/long29103107/portfolio-shorten-link/actions/workflows/ci.yml/badge.svg)](https://github.com/long29103107/portfolio-shorten-link/actions/workflows/ci.yml)
[![Docker Publish](https://github.com/long29103107/portfolio-shorten-link/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/long29103107/portfolio-shorten-link/actions/workflows/docker-publish.yml)

Self-hosted short links for ASP.NET Core, with a React web interface, analytics,
API-key security, SQLite-first storage, and optional PostgreSQL/Redis support.

ShortenLink is both a runnable demo and a reusable .NET package surface. The
demo applications live in `src/`; reusable hosting and domain code lives in
`shared/` and the application layers under `src/`.

## Features

- Create, inspect, deactivate, import, and export short links.
- Redirect short codes to their original URLs.
- React dashboard for link management and operational views.
- Click analytics with optional asynchronous processing.
- API-key/session security, rate limiting, and audit events.
- SQLite by default; PostgreSQL is an opt-in provider.
- In-memory cache by default; Redis is optional.
- Separate public Docker images for the API and web UI.
- Reusable `ShortenLink.Hosting` package for ASP.NET Core applications.

## Quick start with Docker

The fastest way to run the complete public demo is Docker Compose. It uses
SQLite in a persistent volume and Redis for cache/rate limiting.

```bash
docker compose -f docker-compose.public.yml pull
docker compose -f docker-compose.public.yml up -d
```

Open the web UI at <http://localhost:5173>.

Useful endpoints:

- API health: <http://localhost:5188/api/health>
- API base URL: <http://localhost:5188>

View logs or stop the stack:

```bash
docker compose -f docker-compose.public.yml logs -f
docker compose -f docker-compose.public.yml down
```

The public Compose file intentionally uses demo security defaults. Do not
expose it directly to the Internet without enabling authentication and placing
secrets in a secret manager or protected environment variables.

## Docker images

```bash
docker pull ghcr.io/long29103107/portfolio-shorten-link-api:latest
docker pull ghcr.io/long29103107/portfolio-shorten-link-web:latest
```

The API image listens on port `8080`. The web image serves the React build on
port `80` and proxies `/api/*` to the Compose service named `api`.

Run the API alone with SQLite:

```bash
docker volume create shorten-link-data
docker run --rm -p 5188:8080 \
  -v shorten-link-data:/data \
  -e ShortenLink__BaseUrl=http://localhost:5188/ \
  -e ShortenLink__Security__Enabled=false \
  ghcr.io/long29103107/portfolio-shorten-link-api:latest
```

Versioned tags are also published. For reproducible deployments, prefer a
release tag such as `1.0.2` instead of `latest`.

## Run from source

### Requirements

- .NET SDK 10.0+
- Node.js 20+ and npm
- Docker Desktop with Compose v2 (optional)
- Git

Clone and verify the backend:

```bash
git clone https://github.com/long29103107/portfolio-shorten-link.git
cd portfolio-shorten-link
dotnet restore ShortenLink.slnx
dotnet build ShortenLink.slnx
dotnet test ShortenLink.slnx
```

Start the API in one terminal:

```powershell
$env:ShortenLink__Security__Enabled = "false"
dotnet run --project .\src\ShortenLink.Api\ShortenLink.Api.csproj --launch-profile http
```

Start the web UI in another terminal:

```bash
cd src/ShortenLink.Web
npm install
npm run dev
```

The Vite development server proxies `/api` to `http://localhost:5188`.
Override the target with `SHORTENLINK_API_PROXY_TARGET` when needed.

### Frontend with an external API

The frontend uses `/api` by default, which works with the bundled Nginx
reverse proxy. To call an API hosted by another service or domain directly,
set the API base URL before building the frontend:

```bash
VITE_SHORTENLINK_API_BASE_URL=https://api.example.com npm run build
```

The value is the API origin without the `/api` suffix. The frontend will call
`https://api.example.com/api/...`. Because this is a browser cross-origin
request, configure the API with the exact frontend origin:

```bash
ShortenLink__Cors__AllowedOrigins__0=https://web.example.com
```

For Docker, pass the variable as a build argument or build a small custom web
image with the environment value. Vite variables are embedded at build time;
changing them with `docker compose up` after the image was built has no effect.
The API must also allow the frontend origin through CORS.

## Configuration

Configuration uses the standard `ShortenLink__Section__Key` environment variable
format.

| Variable | Default | Description |
| --- | --- | --- |
| `ShortenLink__BaseUrl` | `http://localhost:5188/` | Public URL used in generated links |
| `ShortenLink__Database__UsePostgres` | `false` | Select PostgreSQL instead of SQLite |
| `ShortenLink__Database__SqliteConnectionString` | `Data Source=shorten-link.db` | SQLite database path |
| `ShortenLink__Database__PostgresConnectionString` | — | PostgreSQL connection string |
| `ShortenLink__Cache__Enabled` | `false` | Enable redirect caching |
| `ShortenLink__Cache__Provider` | `Memory` | `Memory` or `Redis` |
| `ShortenLink__Cache__RedisConnectionString` | — | Redis endpoint |
| `ShortenLink__Security__Enabled` | `true` | Enable API-key/session security |
| `ShortenLink__Analytics__Enabled` | `true` | Record click analytics |
| `ShortenLink__RateLimiting__Enabled` | `true` | Enable endpoint rate limiting |

### PostgreSQL option in Compose

`docker-compose.public.yml` is SQLite-first. The PostgreSQL service and API
settings are included as comments so an operator can opt in deliberately:

1. Uncomment the `postgres` service.
2. Set `ShortenLink__Database__UsePostgres` to `"true"`.
3. Uncomment `ShortenLink__Database__PostgresConnectionString`.
4. Add `postgres` back under the API `depends_on` health checks.

Never commit real passwords, API keys, or connection strings.

## API examples

Check health:

```bash
curl http://localhost:5188/api/health
```

Create a link in the demo security-disabled mode:

```bash
curl -X POST http://localhost:5188/api/short-links \
  -H 'Content-Type: application/json' \
  -d '{"originalUrl":"https://example.com","expiredAtUtc":"2030-01-01T00:00:00Z"}'
```

The response contains the generated code and short URL. Visit
`http://localhost:5188/{code}` to test the redirect.

For the complete endpoint contract, run the API in Development and open the
Swagger/OpenAPI page exposed by the application.

## Project layout

```text
shared/
  ShortenLink.Hosting/        # ASP.NET Core integration package
  ShortenLink.Auditing/       # Audit contracts and writers
  ShortenLink.Mediator/       # Request/handler mediator
src/
  ShortenLink.Core/           # Domain models and contracts
  ShortenLink.Application/    # Commands, queries, and handlers
  ShortenLink.Infrastructure/ # EF Core persistence and providers
  ShortenLink.Api/             # Minimal API host
  ShortenLink.Web/             # React + Vite frontend
tests/                         # Unit and integration tests
docs/                          # Release and project documentation
```

## Packages

`ShortenLink.Hosting` is the recommended package for embedding ShortenLink in
another ASP.NET Core host. Lower-level packages are available for applications
that need direct access to domain, infrastructure, or auditing contracts.

```bash
dotnet add package ShortenLink.Hosting
```

The demo API and web app are reference applications and are not required when
consuming the reusable package surface.

## Development commands

```bash
dotnet build ShortenLink.slnx
dotnet test ShortenLink.slnx
```

Frontend commands:

```bash
cd src/ShortenLink.Web
npm ci
npm test
npm run build
```

Build the two container images locally:

```bash
docker build -t shorten-link-api:local -f src/ShortenLink.Api/Dockerfile .
docker build -t shorten-link-web:local -f src/ShortenLink.Web/Dockerfile src/ShortenLink.Web
```

Build a web image that calls an external API directly:

```bash
docker build \
  --build-arg VITE_SHORTENLINK_API_BASE_URL=https://api.example.com \
  -t shorten-link-web:external-api \
  -f src/ShortenLink.Web/Dockerfile src/ShortenLink.Web
```

The same setup is available as a commented `build` and `args` sample directly
inside the `web` service in `docker-compose.public.yml`. Uncomment that block,
run `docker compose build web`, then configure
`ShortenLink__Cors__AllowedOrigins__0` on the API.

## Releases and publishing

Docker publishing is handled by GitHub Actions when a semantic version tag is
pushed. The workflow publishes these images:

- `ghcr.io/long29103107/portfolio-shorten-link-api:<version>`
- `ghcr.io/long29103107/portfolio-shorten-link-web:<version>`
- `ghcr.io/long29103107/portfolio-shorten-link-api:latest`
- `ghcr.io/long29103107/portfolio-shorten-link-web:latest`

Maintainers can publish a release with:

```bash
git tag -a v1.0.3 -m "Release v1.0.3"
git push origin v1.0.3
```

The GHCR packages must be configured as public before anonymous users can pull
them. Use immutable version tags for production deployments and reserve
`latest` for convenience.

## Contributing

Issues and focused pull requests are welcome. Before opening a pull request:

1. Explain the problem and the intended behavior.
2. Add or update tests for behavior changes.
3. Update documentation and configuration examples when relevant.
4. Run the backend and frontend verification commands above.
5. Read [CONTRIBUTING.md](CONTRIBUTING.md) for repository conventions.

## Security

Please do not report security issues in public issues. Use the repository's
private security reporting channel when available. Never commit credentials or
run the demo security-disabled configuration on an Internet-facing deployment.

## License

This repository does not currently include a `LICENSE` file. Until an explicit
open-source license is added by the maintainers, treat the code and published
images as an evaluation/reference release rather than assuming unrestricted
redistribution rights.

## Acknowledgements

The README organization follows conventions commonly used by self-hosted
open-source URL shorteners: a concise project overview, feature list, Docker
quick start, configuration reference, API usage, development workflow, and
contribution guidance.
