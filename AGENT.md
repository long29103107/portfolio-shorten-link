# Agent Instructions

This file is the entry point for coding agents working in this repository.
Keep it in sync with the repository architecture and the user-facing README.

## Project mission

ShortenLink is a reusable .NET short-link library with a demo ASP.NET Core API,
a React/Vite frontend, optional PostgreSQL and Redis integrations, and a public
Docker image published to GitHub Container Registry.

The reusable package boundary is more important than the demo applications:

- `ShortenLink.Core` owns domain models, contracts, validation, exceptions,
  code generation, and service abstractions.
- `ShortenLink.Infrastructure` owns EF Core persistence, repositories, and
  SQLite/PostgreSQL provider selection.
- `ShortenLink.Hosting` owns ASP.NET Core DI, options, authorization,
  endpoint mapping, caching, analytics, and rate limiting.
- `ShortenLink.Api` is a thin demo host and must not duplicate domain logic.
- `ShortenLink.Web` is a demo React frontend and must not own backend rules.

## Required reading

Before changing source or durable project knowledge, read:

1. `.okf/standards/architecture.md`
2. `.okf/standards/coding-style.md`
3. `.okf/standards/testing.md`
4. The matching workflow under `.okf/workflows/`
5. The relevant source files and the applicable README section

For release/package work, also read the release gate skill and the relevant
files under `docs/` before publishing anything.

## Working rules

- Preserve existing user changes and inspect `git status` before editing.
- Keep SQLite as the default local path; PostgreSQL and Redis are opt-in config.
- Keep reusable projects packable and independent from demo API/Web projects.
- Never commit API keys, passwords, tokens, connection secrets, or `.env` files.
- Treat `src/ShortenLink.Api/appsettings.json` as safe public defaults only.
- Update `README.md` when commands, configuration, packages, Docker usage, or
  public contracts change.
- Keep `.okf` phase/task notes synchronized when a task explicitly belongs to
  an active phase.
- Use the repository commit convention in `CONTRIBUTING.md`.
- Do not stage or restore unrelated user changes.

## Verification

Run the smallest relevant checks, and report skipped checks explicitly:

```powershell
dotnet build ShortenLink.slnx --verbosity minimal
dotnet test ShortenLink.slnx --verbosity minimal
dotnet pack ShortenLink.slnx -c Release --verbosity minimal
docker compose -f docker-compose.yml config
```

For frontend changes:

```powershell
cd .\src\ShortenLink.Web
npm install
npm run build
npm test
```

For Docker changes, build the image and smoke `/api/health`, create, detail,
redirect, and cleanup behavior. Do not publish to a registry unless the user
explicitly requests a release and the target registry/tag are confirmed.

