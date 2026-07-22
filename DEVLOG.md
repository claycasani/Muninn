# Engineering Journey

This is a curated record of Muninn's most important engineering decisions and investigations. It replaces the original session-by-session internal log, which mixed superseded hypotheses, local machine details, signing identifiers, private development endpoints, and temporary verification data.

The source code is authoritative. Status notes below distinguish implemented behavior from simulator verification, physical-device verification, and remaining work.

## Current snapshot — July 2026

Muninn's implemented product loop is:

1. Save a URL by pasting it or using the iOS share extension, or select/review an image.
2. Persist the save immediately.
3. Analyze link content or screenshot bytes asynchronously with Gemini.
4. Store a structured title, inferred intent, category, suggested action, and extracted text.
5. Search, filter, complete, archive, restore, recategorize, or permanently delete the save.

The backend has been exercised as a Docker service on Render. Its deployment datasource is configured through external environment variables for Neon PostgreSQL; local development uses PostgreSQL 16 in Docker. The latest backend revision and schema are not currently deployed, so this document does not claim a live public environment. Image objects live in Cloudflare R2.

The iOS Photos review path and Liquid Glass-inspired UI are functional but remain active engineering areas. In particular, large screenshot backlogs need more memory-bounded loading and physical-device profiling.

## Milestones

### 2026-06-10 to 2026-06-11 — Backend, schema ownership, and authentication

The first vertical slice established Java 21, Spring Boot, PostgreSQL, and the .NET MAUI client.

Key decisions:

- Flyway owns schema evolution; Hibernate runs with `ddl-auto: validate`.
- Authentication is stateless Spring Security plus JWT rather than a hosted identity product.
- Passwords are stored with BCrypt hashes.
- Spring Data repositories own application persistence; no production query is assembled by concatenating user input.
- GitHub Actions starts PostgreSQL and runs `mvn verify` for backend changes.

The initial API supported registration, login, link saves, listing, completing, and archiving.

### 2026-06-11 — Asynchronous Gemini analysis

The analysis pipeline was separated behind `AnalysisService`. `SaveService` persists a save and then dispatches analysis to a named, bounded Spring executor, allowing the HTTP response to return before network content extraction and model inference finish.

Two analysis paths now exist:

- Link saves: jsoup fetches and extracts page title, metadata, and readable text.
- Image saves: the backend reads the referenced R2 object and sends a multimodal request to Gemini.

Both paths request structured JSON. The model name and API key live in configuration, not controllers or business logic. Integration tests replace the provider with a deterministic stub and use Awaitility to verify eventual persistence.

Important limitation: the executor is in-process, not a durable message queue. Restarting the container can interrupt an in-flight analysis.

### 2026-06-14 — Direct-to-R2 image storage

The image path was designed so the Spring API does not proxy uploads:

1. An authenticated client requests a presigned upload URL.
2. The backend creates a user-scoped object key and a 15-minute R2 PUT signature.
3. The client uploads bytes directly to R2.
4. The client creates a save containing the object key.
5. API reads return short-lived presigned GET URLs for rendering.

Deleting an image save attempts to remove its R2 object before deleting the analysis and save rows.

### 2026-06-14 to 2026-06-16 — Client lifecycle, account controls, and categories

The MAUI client added:

- a cold-launch JWT expiry check;
- coordinated mid-session `401` handling that clears stored auth state and presents login once;
- Inbox, Save Detail, Digest, and Archive/Completed views;
- account settings, password changes, and account deletion;
- complete/archive and inverse restore operations;
- manual category overrides and category filtering.

The category model later evolved from values discovered on saves to a per-user `categories` table. Category names now form the allowed Gemini vocabulary. Rename relabels affected saves without re-analysis; delete clears stale overrides and asynchronously recategorizes saves that still point at the removed category.

### 2026-06-15 to 2026-06-17 — iOS share extension

The URL-only iOS share extension uses UIKit, an App Group `NSUserDefaults` suite, and a small native confirmation view.

The handoff is retry-safe: the extension writes `pendingShareUrl`; the authenticated main app posts the URL to the backend and removes the key only after success. Cold launch, login, and resume paths all check for pending work, and the Inbox refreshes after ingestion.

The complete Safari → extension → App Group → main app → backend → Inbox path was verified on an iOS simulator. Physical distribution remains dependent on provisioning the same App Group capability for both the app and extension. Personal-team UI-test builds deliberately exclude the extension when those profiles are unavailable.

### 2026-06-16 to 2026-06-26 — Product depth and cloud hardening

Additional work included:

- a distinct `artifact_title` so cards identify *what* was saved while inferred intent explains *why*;
- persisted profile and settings fields;
- full save deletion with object cleanup;
- search across title, domain, inferred intent, extracted text, and category;
- user-editable categories and AI-assisted recategorization;
- a multi-stage Java 21 Docker image and Render Blueprint;
- external Neon-style JDBC configuration with TLS supplied in `DATABASE_URL`;
- explicit production error redaction and safe JSON exception responses.

Security work added:

- JWT startup failure when `JWT_SECRET` is missing or shorter than 32 characters;
- BCrypt work factor 12;
- stateless sessions;
- an eight-attempts-per-minute limiter for login and registration, keyed by endpoint and the first forwarded client IP with remote-address fallback;
- no wildcard browser CORS surface for the native client;
- parameterized Spring Data/JPQL access rather than concatenated application SQL.

Proxy note: the limiter is designed for the Render reverse-proxy path. If the service is ever exposed directly or moved behind a different proxy topology, forwarded-header trust should be tightened explicitly.

### 2026-06-26 to 2026-06-28 — Render and Neon migration

The original plan named Railway, but the implementation moved to Render plus an external Neon database:

- `backend/Dockerfile` builds the Spring Boot jar and runs it on a Java 21 JRE.
- `render.yaml` defines the Docker web service and injects database, JWT, Gemini, and R2 configuration as external secrets.
- `application.yaml` prefers `DATABASE_URL`, `DB_USERNAME`, and `DB_PASSWORD`, with local aliases as fallbacks.
- `.env.example` documents a Neon direct JDBC endpoint with `sslmode=require`.

The original DEVLOG recorded client testing against the Render service, confirming that the deployment path was exercised rather than remaining only a plan. That environment is not the current source revision. The repository intentionally contains no real Neon hostname, username, or password.

### 2026-06-17 to 2026-07-02 — Screenshot review and memory investigation

The iOS Photos feature uses a review queue rather than silently importing the camera roll:

- full-access priming and a local “start now” baseline;
- a Photos predicate for screenshot media subtype after the baseline;
- local reviewed identifiers for Save/Ignore behavior;
- a foreground screenshot notification that refreshes the badge;
- direct reuse of the presigned R2 upload path.

An early implementation could fall back from the Screenshots smart album to general image enumeration and requested extremely large thumbnails. That path was removed. Current counting is lazy, screenshot-only, baseline-bounded, cached, and performs no image decode. Review thumbnails are capped at 1024px.

The subsequent physical-device respring investigation initially implicated Photos, MAUI CollectionView, Shell, and Syncfusion glass. Diagnostic builds removed each subsystem in turn. The actual cause was eventually isolated to two SVG assets exported with percentage dimensions and enormous view boxes. MAUI Resizetizer generated bitmaps whose decoded sizes were measured in gigabytes, producing paired app/render-server memory growth and iOS jetsam.

The durable fixes were:

- give every SVG an absolute width and height;
- audit generated Resizetizer dimensions;
- keep screenshot count queries lazy and screenshot-specific;
- bound thumbnail dimensions;
- retain launch-time diagnostic switches for future platform regressions.

Physical-device testing after the SVG fix completed without the prior resprings. A separate memory concern remains: the review page still eagerly loads every pending thumbnail and the save/preview paths materialize full image bytes. Paging, cancellation, and large-backlog profiling are still needed before calling ingestion memory-complete.

### 2026-06-22 to 2026-07-21 — Liquid Glass experiments and current implementation

The glass work went through three implementations:

1. A custom MAUI `ContentView` injected a native `UIGlassEffect`, but MAUI composition made it appear as an opaque disc.
2. A custom native view handler owned the UIKit hierarchy, but it was removed after visual results remained inconsistent.
3. The current implementation uses Syncfusion `SfGlassEffectView` for circular chrome and a custom floating tab pill.

Current source truth:

- `ChromeGlassButton` and `GlassTabBar` instantiate Syncfusion glass surfaces.
- `LiquidGlassCompatibility` keeps a runtime `MUNINN_DISABLE_GLASS` kill-switch.
- The fallback is a plain MAUI `Border`, not native glass.
- Search is commit-based because rebuilding the CollectionView while typing consistently dismissed the iOS keyboard, even after granular collection synchronization.

The major respring investigation exonerated glass; unsized SVGs were the cause. Glass was restored and approved on-device at the branch level. Several later interaction refinements—press feedback, search commit behavior, category-sheet polish, and some empty-state alignment—still had explicit device-verification items in the final raw notes and should not be described as fully polished.

## Current known limitations

- The Digest view lists active saves newest-first; there is no implemented daily selection job, digest-history table, notification scheduler, or APNs path.
- Screenshot review needs paging/cancellation and another large-backlog physical-device memory pass.
- Full-image preview/upload paths retain image bytes in managed memory for the lifetime of the operation.
- The URL share extension is simulator-verified; physical/App Store App Group provisioning is incomplete in the checked-in development setup.
- Syncfusion glass is the active surface, with a plain fallback. It is not a direct native `UIGlassEffect` implementation.
- Analysis jobs are not durable across backend restarts.
- Mac Catalyst has no equivalent screenshot-folder ingestion or share target.
- The checked-in client templates are development-oriented; a release build still needs an explicit production backend URL and distribution signing.
- The backend currently has no dedicated public health endpoint.

## Verification discipline

The project uses three levels of evidence:

- **Source verified:** the behavior is present in the current implementation.
- **Automated:** backend integration tests or compilation exercises the behavior.
- **Device verified:** the interaction was observed on simulator or physical hardware.

This distinction matters for native UI, Photos, memory, signing, and share-extension work. A successful compile is not treated as a successful device interaction, and an old DEVLOG claim is not treated as current when the code has since changed.
