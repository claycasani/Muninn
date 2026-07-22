# Roadmap

> **Historical planning document.** This checklist is retained to show the project's original sequencing and how the implementation evolved. It is not a reliable statement of current implementation status; see [README.md](README.md) and [DEVLOG.md](DEVLOG.md) for source-verified current status.

**The app:** Saves the things you screenshot and share, uses AI to figure out why you saved them, and makes sure you actually come back and act on them.

**Stack:** .NET MAUI (C#) for native iOS + Mac · Java + Spring Boot backend · Postgres · an LLM for analysis · GitHub for version control · IntelliJ (backend) + Rider (app).

This is ordered so you reach a usable thing as early as possible, then layer on the harder native pieces. Each phase has checkboxes you can track in the repo.

---

## Decisions to lock early

Before writing much code, pick these, because they shape everything downstream:

- [ ] **LLM provider** for intent inference and reading screenshots (needs a vision-capable model, since saves are often images).
- [ ] **Backend host** (Railway, Render, and Fly.io are the easy solo-friendly options; AWS if you want the resume signal).
- [ ] **Managed Postgres** (Neon, Supabase, or whatever your host bundles).
- [ ] **Object storage** for the actual screenshot/photo files (Cloudflare R2 or S3). Postgres stores metadata, not the images themselves.
- [ ] **Auth approach** (roll your own with Spring Security + JWT, or lean on a service to save time).
- [ ] **Push notifications** path (Apple Push Notification service directly, or through a provider).

---

## Phase 0 — Foundations

- [ ] Create the monorepo on GitHub with README + combined `.gitignore` (`backend/` and `app/` folders).
- [ ] Scaffold the Spring Boot backend into `backend/`, open in IntelliJ.
- [ ] Scaffold the MAUI app into `app/`, open in Rider.
- [ ] Install Xcode + MAUI workloads, confirm you can run an empty MAUI app on both an iOS simulator and your Mac.
- [ ] Get a "hello world" round trip working: MAUI app calls one dummy backend endpoint and shows the response.
- [ ] Set up GitHub Actions to build and test the backend on every push.

## Phase 1 — Product + data model

- [ ] Write out the core screens on paper or in Figma (save inbox, save detail, daily digest, archived).
- [ ] Design the database schema. Likely tables:
  - `users` (id, email, auth, push token, settings)
  - `saves` (id, user_id, type [link/image], source_url or image_ref, status [active/completed/archived], created_at, completed_at)
  - `analyses` (save_id, inferred_intent, category, suggested_action, extracted_text, confidence)
  - `digests` (user_id, sent_at, save_ids) for tracking what's been surfaced
- [ ] Define the API contract (endpoints + request/response shapes) before building, so the app and backend agree.

## Phase 2 — Backend core (the brains)

- [ ] Spring Boot + Postgres wired up, with schema migrations (Flyway or Liquibase).
- [ ] Auth: registration, login, JWT, Spring Security guarding the routes.
- [ ] Save CRUD endpoints:
  - `POST /saves` (create from a link or an uploaded image)
  - `GET /saves` (list, filter by status/category)
  - `GET /saves/{id}`
  - `POST /saves/{id}/complete`
  - `POST /saves/{id}/archive`
- [ ] Object storage integration so image saves upload the file and store a reference.
- [ ] Input validation + error handling.
- [ ] Unit + integration tests for the endpoints.

## Phase 3 — AI analysis pipeline

This is the part that makes the app special, so treat it as its own chunk.

- [ ] **Link path:** backend fetches the URL, extracts the main readable content, sends it to the LLM.
- [ ] **Image path:** send the screenshot to a vision model (or OCR it first) to read what's in it.
- [ ] **Intent inference:** one well-designed prompt that returns *why* it was probably saved, a category, and a suggested action, in structured JSON.
- [ ] **Few-shot calibration:** collect real examples of saved links/images and ideal `inferredIntent`, `category`, and `suggestedAction` outputs, then inject a small curated set into the Gemini prompts before the current save's content.
- [ ] **Eval example dataset:** add an append-friendly examples file under `backend/src/main/resources/prompts/` using JSONL-style records such as `{"saveType":"link|image","saveContent":...,"idealAnalysis":{...}}`. Use it first as prompt calibration/eval data; preserve compatibility with a later fine-tuning dataset.
- [ ] Store the analysis with the save and use it to auto-organize.
- [ ] **Make it asynchronous:** saving should feel instant, with analysis happening in a background job/queue so the user never waits on the LLM.
- [ ] Tests with a mocked LLM so you're not paying per test run.
- [ ] Track LLM cost per save and set sane limits (you've done cost-optimization work, so you know the trap here).

## Phase 4 — MAUI app core

- [ ] App talks to the backend API via `HttpClient`, with auth tokens stored securely.
- [ ] Auth screens (sign up / log in).
- [ ] Save inbox UI (list of recent saves grouped by AI category).
- [ ] Save detail view, with the inferred reason and suggested action.
- [ ] Complete + Archive actions wired to the backend.
- [ ] Settings/account architecture:
  - Settings entry point from Inbox.
  - Account overview screen.
  - Change password screen backed by `POST /account/password`.
  - Delete account confirmation backed by `DELETE /account`.
  - Display name, digest toggle, digest time, notification toggle, and auto-archive settings backed by `PUT /account`.
  - Later: support/legal destinations once URLs or in-app legal pages exist.
- [ ] Native styling pass: tight spacing, restrained palette, good type, the polished Origin/Tricount feel.
- [ ] Offline-friendly behavior and a clean sync with the backend.

## Phase 5 — Ingestion on the client (the hard native bits)

Budget extra time here. These are the trickiest parts of the whole project.

- [ ] **iOS Share Extension** so you can share a link or image into the app from anywhere. This needs native iOS project work alongside MAUI and is genuinely fiddly.
- [ ] **Photo library + screenshots access:** read the user's screenshots using the Photos framework (there's a screenshot media subtype you can filter on), with proper permission prompts.
- [ ] **Mac equivalents:** share target and screenshots-folder access on macOS.

## Phase 6 — Daily digest + notifications

- [ ] Backend scheduled job (Spring `@Scheduled`) that assembles each user's recent saves once a day.
- [ ] Push notifications: APNs setup, device token registration, send the digest as a notification.
- [ ] Digest view in the app showing the day's resurfaced saves.
- [ ] Simpler fallback for v1 if APNs ceremony slows you down: local notifications scheduled on-device.

## Phase 7 — Testing + QA

- [ ] Backend unit + integration tests at solid coverage.
- [ ] App UI tests for the core flows.
- [ ] Manual testing on a real iPhone and your Mac (simulators lie about some things).
- [ ] Edge cases: dead links, weird/blank screenshots, no network, sync conflicts, huge backlogs.
- [ ] Closed beta via TestFlight.

## Phase 8 — Deployment

- [ ] Containerize the backend (Docker), deploy to your host with managed Postgres + object storage + secrets.
- [ ] CI/CD: GitHub Actions builds, tests, and deploys the backend automatically.
- [ ] Apple Developer Program account ($99/yr), code signing, provisioning profiles.
- [ ] App Store Connect setup, TestFlight, then App Store submission for iOS.
- [ ] Mac distribution: Mac App Store, or a notarized direct download.
- [ ] Logging, error tracking, and uptime monitoring.

## Phase 9 — Post-launch

- [ ] Crash reporting + basic analytics.
- [ ] Watch LLM spend as real usage grows.
- [ ] Collect feedback and iterate on intent accuracy (the AI guessing *why* you saved something is the feature that'll need the most tuning).
- [ ] Revisit actual fine-tuning only after real usage produces enough corrections to justify it. Prefer prompt examples first; use Vertex/Gemini fine-tuning later only if recurring analysis failures remain after prompt calibration.

---

## Suggested MVP cut (so you ship, not stall)

If you want something working fast, build this slice first and defer the rest:

1. Auth + save a **link** by pasting it into the app (skip the share extension at first).
2. Backend analyzes the link with the LLM and categorizes it.
3. Inbox list + Complete/Archive.
4. A digest **view** inside the app (skip push; you open the app to see it).

That proves the entire core loop end to end. Then add image saves, the share extension, screenshots-folder ingestion, and real push notifications in that order, since each is independently hard.
