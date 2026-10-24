# CLAUDE.md

Personal blog: ASP.NET Core (.NET 10) Razor Pages, server-rendered, single admin, public readers.
The full brief and milestone plan were agreed in the first session; this file keeps what future work needs.

## Commands

```sh
dotnet build                          # warnings are errors
dotnet test                           # xUnit v3 on Microsoft Testing Platform (see global.json)
dotnet format --verify-no-changes     # what CI checks; run `dotnet format` to fix
dotnet run --project src/SaPosts.Web  # http://localhost:5243 (admin from user-secrets)
docker compose up --build             # http://localhost:8080 (admin from .env)
dotnet tool restore                   # dotnet-ef, pinned in .config/dotnet-tools.json
dotnet ef migrations add <Name> --project src/SaPosts.Web --output-dir Data/Migrations
```

Migrations are applied at startup. They are marked as generated code in `.editorconfig`, so
analyzers and `dotnet format` skip them.

## Layout

- `src/SaPosts.Web`: the only app project, folders by feature: `Posts/`, `Media/`, `Contact/`
  (entity + EF configuration each), `Auth/`, `Data/` (DbContext, startup initialization),
  `Infrastructure/`, `Pages/` (`Pages/Admin/` requires sign-in, except `Login`).
  Interfaces only at real seams (media storage, mail sender, clock), no layered projects.
- `tests/SaPosts.IntegrationTests`: `BlogFactory` gives each instance its own temp data directory.
  Tests that change shared state (lockout, rate limits) create their own instance.
  `AdminSession` signs in and submits forms with their antiforgery token.
- `tests/SaPosts.UnitTests`: slugs, sanitization, validation, time zone. No tests for trivial code.
- `wwwroot/lib/`: vendored third-party front-end files, each with its licence and `VERSION.txt`.

## Conventions

- Code, comments, commits: English. **UI text: French** (`<html lang="fr">`, fr-FR culture).
  A post's own language goes on its `<article lang>`, not on `<html>`.
- Conventional commits, small and runnable. Milestones: M1 skeleton/Docker/CI, M2 data + auth,
  M3 posts + Markdown + public pages, M4 media, M5 contact + email + inbox, M6 SEO/RSS/legal/polish.
- Package versions live in `Directory.Packages.props` (central package management).
  Verify versions and APIs against official docs before adding a library; check its licence.
  Avoid FluentAssertions (commercial licence since v8): use xUnit `Assert`.
- Nothing outside the brief without asking the owner. No secrets in the repo (`.env` is ignored).
  Seed content stays neutral (no real company names).

## Front-end rules

- All colours, fonts, sizes and spacing are tokens in `wwwroot/css/tokens.css`. Other CSS uses
  semantic tokens only (`--color-*`, `--font-*`, `--space-*`), never `--palette-*` or raw values.
  Dark mode later = remap semantic colours in that file.
- Strict CSP (`Infrastructure/Security/SecurityHeaders.cs`): no inline `<script>`, `<style>` or
  `style=""`, no external origins. Visitors' browsers must make zero third-party requests.
  If the admin editor ever needs inline styles, relax `style-src` for `/admin` only.
- Minimal JS, only where needed (editor, upload progress). WCAG 2.2 AA: visible `:focus-visible`,
  targets ≥ 24×24 px, text contrast ≥ 4.5:1 (palette ratios are noted in tokens.css).
- Typeface: Source Serif 4 variable (OFL), self-hosted Latin subset; optical sizing is automatic.
  `MapStaticAssets` fingerprints `~/` URLs but not `url()` inside CSS, so the font preload in
  `_Layout` uses the plain path that tokens.css uses (guarded by `LayoutTests`).
  The page frame and the prose column share their left edge; wide figures extend to the right.

## Decisions

- **Approved additions beyond the brief**: Mailpit in a dev-only compose override (M5); 301 redirects
  from former slugs (done in M3). Declined: TOTP 2FA.
- **Hosting**: still open. Vercel is not possible (no .NET runtime, ephemeral filesystem).
  The target is a small Linux VM running this compose file behind a TLS proxy (decided at M6).
- **SQLite** in WAL mode, single instance by design. Backups with `VACUUM INTO`, not file copies.
  Dates are UTC `DateTime` (EF cannot order or compare `DateTimeOffset` on SQLite); a converter
  marks values read back as UTC.
- **Data Protection keys** persist on the data volume (otherwise each redeploy logs out the admin
  and breaks antiforgery tokens). They are unencrypted at rest; the volume is as sensitive as the DB.
  Paths are resolved from options lazily so test hosts can each use their own directory.
- **Admin account**: Identity core, no Identity UI, no roles (`IdentityUserContext`). Created from
  `Admin:*` settings on first start; later, settings that are present win (password reset path).
  Lockout 5 failures / 15 min; login POST rate limit 10 / 15 min per IP. Lockout lets anyone lock
  the admin out for 15 minutes: accepted for a single-owner blog.
- **Cookies** `saposts.auth` and `saposts.antiforgery`: HttpOnly, SameSite=Strict, Secure when the
  request is HTTPS. Behind the TLS proxy this requires forwarded headers (M6), which the per-IP
  rate limit needs too.
- **Encoding**: `WebEncoderOptions` allows all Unicode ranges, so French text is not entity-encoded.
- Validation is server-side only (client validation disabled: no validation scripts).
- **Markdown** (`Posts/MarkdownRenderer.cs`) is rendered on save (Markdig → HtmlSanitizer) and
  stored with `RendererVersion`; bump `MarkdownRenderer.Version` whenever output changes and posts
  are re-rendered at startup. The editor preview (`?handler=Preview`) uses the same renderer.
  Raw HTML disabled; no MediaLinks/generic attributes; explicit tag/attribute allowlist with
  http/https/mailto only; no `style` (CSP), so pipe-table alignment is dropped; `#` renders as h2
  (the title is the only h1); `pre`/`table` get `tabindex="0"` (scrollable regions).
- **Editor**: TinyMDE 0.2.35, `tiny` build without its toolbar (vendored, MIT). Chosen because it is
  the candidate compatible with the strict CSP (EasyMDE: CDN fetches and a second Markdown parser;
  OverType: injects `<style>`; ink-mde: unmaintained). Its gaps are patched in `post-editor.js`:
  ARIA on the contenteditable, our own `<button>` toolbar, command state read at call time
  (`toggleCommandState` uses a stale cache), caret moved past markers inserted on empty lines.
  Without JS the plain textarea still works.
- **Slugs**: generated from the title (accents folded, numeric suffix if taken); an explicit slug
  that is taken is rejected. Renaming a post that was ever published stores the old slug in
  `PostSlugRedirects` (301 to the current slug); a slug reused by a post replaces its redirect.
- **URLs**: posts at `/articles/{slug}`, home pagination at `/page/{n}` (`?page=` would clash with
  the reserved Razor Pages `page` route value), `/page/1` 301s to `/`.
- **Dates** display in `Site:TimeZone` (Europe/Paris) and in the post's language (fr-FR / en-GB);
  the admin types publication dates in Paris time (`SiteTime`).
- **Contact messages** are stored first, then sent by a background service (the table is the outbox).
- **Absolute URLs** (canonical, OG, RSS, sitemap) come from configuration, never the `Host` header.
