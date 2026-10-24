# SaPosts

A simple, self-hosted personal blog: ASP.NET Core 10 Razor Pages, SQLite, local media storage,
one admin, no third-party requests from visitors' browsers.

## Run with Docker

Requirements: Docker with Compose v2.

```sh
cp .env.example .env      # then edit the values
docker compose up --build
```

The site is at <http://localhost:8080> and the health check at <http://localhost:8080/healthz>.
The database and uploaded media live in the named volumes `saposts_data` and `saposts_media`,
so they survive `docker compose down` (but not `docker compose down -v`).

## Run for development

Requirements: .NET SDK 10.0.

```sh
dotnet run --project src/SaPosts.Web   # http://localhost:5243
dotnet test
dotnet format --verify-no-changes      # the CI format check
```

## Configuration

Settings come from `appsettings.json`, overridden by environment variables (in Docker, via `.env`).
Environment variable names use `__` for nesting.

| Variable | Default | Purpose |
|---|---|---|
| `HTTP_PORT` | `8080` | Host port published by Docker Compose (loopback only) |
| `Site__Title` | `Blog` | Site title in header, footer and page titles |
| `Site__Tagline` | `Notes et articles` | Shown under the title; empty hides it |
| `Site__PortfolioUrl` | `https://example.com` | Portfolio link in the header |
| `AllowedHosts` | `*` | Set to your domain in production |

## Deploy

To be written once hosting is chosen (M6). The app listens on HTTP port 8080 and expects a reverse
proxy in front of it to terminate TLS. HSTS is only sent on HTTPS requests outside development.

## Backup and restore

To be written in M6.

## Licences

The bundled Source Serif 4 font is licensed under the SIL Open Font License 1.1
(`src/SaPosts.Web/wwwroot/fonts/source-serif-4/OFL.txt`).
