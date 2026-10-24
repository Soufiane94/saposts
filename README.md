# SaPosts

A simple, self-hosted personal blog: ASP.NET Core 10 Razor Pages, SQLite, local media storage,
one admin, no third-party requests from visitors' browsers.

## Run with Docker

Requirements: Docker with Compose v2.

```sh
cp .env.example .env      # then set at least Admin__Password (12+ characters)
docker compose up --build
```

The site is at <http://localhost:8080>, the admin at <http://localhost:8080/admin> and the health
check at <http://localhost:8080/healthz>. Without admin settings, the first start stops with a
message saying which variables to set.
The database and uploaded media live in the named volumes `saposts_data` and `saposts_media`,
so they survive `docker compose down` (but not `docker compose down -v`).

## Run for development

Requirements: .NET SDK 10.0.

```sh
dotnet user-secrets set Admin:Email you@example.com --project src/SaPosts.Web
dotnet user-secrets set Admin:Password "a long passphrase" --project src/SaPosts.Web
dotnet run --project src/SaPosts.Web   # http://localhost:5243, data in src/SaPosts.Web/App_Data
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
| `Admin__Email` | (none) | Admin sign-in email |
| `Admin__Password` | (none) | Admin password, 12+ characters |
| `Storage__DataPath` | `App_Data` (`/data` in Docker) | SQLite database and Data Protection keys |
| `Storage__MediaPath` | `App_Data/media` (`/media` in Docker) | Uploaded images and videos |
| `Media__MaxImageBytes` | `26214400` (25 MB) | Largest accepted image |
| `Media__MaxVideoBytes` | `209715200` (200 MB) | Largest accepted video |
| `AllowedHosts` | `*` | Set to your domain in production |

## Admin account

There is exactly one account, and no registration page. It is created on first start from
`Admin__Email` and `Admin__Password`. On later starts, any of these values that is set replaces
the stored one, so to change or reset the password, edit `.env` and run `docker compose up -d`.
This also lifts a lockout. Once the account exists, you may remove the password from `.env`.

- Five failed sign-ins lock the account for 15 minutes.
- Sign-in attempts are limited to 10 per 15 minutes per IP address.
- Sessions last 12 hours, renewed while you use the admin.

## Writing posts

Sign in at `/admin`, then **Articles → Nouvel article**. The body is Markdown, previewed live as it
will be published: paragraphs, `##` section headings, emphasis, links, lists, quotes, code blocks,
tables and footnotes. Raw HTML is shown as text. Leave the slug empty to derive it from the title.
Renaming a published post keeps its old address working (permanent redirect). The publication date
is set on first publish and can be changed afterwards (Paris time).

## Images and videos

Drop files onto the post editor (or use **Image ou vidéo…**), or upload them in **Médiathèque**.
Images ask for an alternative text first; it is required.

- **Images**: JPEG, PNG, WebP, AVIF, GIF, up to 25 MB. Each is resized (400 to 1600 px wide) and
  served as AVIF, WebP and JPEG/PNG; EXIF, GPS and every other metadata block are removed.
- **Videos**: MP4 or WebM, up to 200 MB, stored as uploaded (no transcoding). Export H.264/AAC MP4
  with the "faststart" / "web optimized" option, or VP9 WebM; iPhone HEVC files will not play in
  every browser. The uploader warns when this browser cannot read the file or faststart is missing.
  A poster image can be chosen in the media library.
- Files are identified by their content, not their extension. Only library media appears in posts.
- Limits: `Media__MaxImageBytes` and `Media__MaxVideoBytes` (bytes). A reverse proxy must accept
  bodies at least as large (nginx: `client_max_body_size 210m;`).

Media files live in the `saposts_media` volume (one folder per item).

## Deploy

To be written once hosting is chosen (M6). The app listens on HTTP port 8080 and expects a reverse
proxy in front of it to terminate TLS. HSTS is only sent on HTTPS requests outside development.

## Backup and restore

To be written in M6.

## Licences

- Source Serif 4 font: SIL Open Font License 1.1 (`src/SaPosts.Web/wwwroot/fonts/source-serif-4/OFL.txt`).
- TinyMDE editor: MIT (`src/SaPosts.Web/wwwroot/lib/tiny-mde/LICENSE.txt`).
- Server libraries: Markdig (BSD-2-Clause), HtmlSanitizer (MIT), NetVips (MIT), ASP.NET Core and
  EF Core (MIT).
- libvips and the libraries bundled with it (NetVips.Native): LGPL-3.0-or-later and permissive
  licences, listed in the package's `THIRD-PARTY-NOTICES.md`. They are loaded dynamically. Running
  the site yourself creates no obligation; publishing the Docker image would require shipping those
  notices and offering the LGPL sources.
