# Muninn Design System
*your memory, returned*

This document is the canonical design reference for the Muninn iOS/macOS app. Every UI decision in the MAUI codebase should trace back to a rule here. Claude Code should read this before writing any XAML.

The corresponding token file is `Styles.xaml` in `app/Resources/Styles/`. All colors, font sizes, corner radii, and shadows are defined there as named resources. Never hardcode a hex value or font size in a page — always reference a named resource.

---

## Personality

Muninn is calm, purposeful, and a little rare. It handles memory and attention — things people feel quietly anxious about — so the interface should feel like relief, not stimulation. The olive accent is deliberate: dark, muted, earthy. Not a tech-blue trying to feel trustworthy. Not a bright green celebrating completion. Something quieter. The tagline is "your memory, returned" — the design should feel like that.

---

## Color

### Core palette

| Token | Hex | Usage |
|---|---|---|
| `ColorBackground` | `#FAFAF8` | App background, all screens |
| `ColorSurface` | `#FFFFFF` | Card surfaces, input fields |
| `ColorSunken` | `#F2F2EF` | Recessed surfaces, empty state backgrounds |
| `ColorTextPrimary` | `#1A1A1A` | Headlines, card titles, primary labels |
| `ColorTextSecondary` | `#8E8E93` | Timestamps, source domains, subtitles |
| `ColorTextTertiary` | `#C2C2C6` | Placeholder text, disabled states |
| `ColorSeparator` | `#E5E5EA` | Only use where whitespace alone is insufficient |

### Accent — deep olive

| Token | Hex | Usage |
|---|---|---|
| `ColorAccent` | `#444A2E` | Primary buttons (white text), completed checkmarks, active tab icons |
| `ColorAccentTint` | `#444A2E` at 12% opacity | Category tags (background), selected states, secondary buttons |
| `ColorAccentTintText` | `#444A2E` | Text on tinted surfaces |

**Tint system rules — read carefully:**
- `ColorAccent` solid is used in exactly two places: full-width primary action buttons and the completed-state checkmark circle.
- `ColorAccentTint` (12% opacity fill, `ColorAccentTintText` label) is used for category tags and secondary/tinted buttons.
- Never fill a large background area with solid olive — it's dark and desaturated and will make the screen feel heavy. Keep it to small, deliberate marks.
- The completed card state uses a solid olive circle checkmark on the right, and the card title gets a strikethrough in `ColorTextTertiary`.

### Digest urgency banners
Subtle gray banners (`ColorSunken` background, `ColorTextSecondary` text, clock icon) that appear above digest cards with contextual urgency copy ("Portland trip is in 4 days. Still no dinner reservation."). These are never olive — they read as the AI's voice, not a call to action.

---

## Typography

All type uses the system font stack: `-apple-system, SF Pro Display, SF Pro Text`. MAUI on iOS/macOS renders real SF Pro automatically. No custom font loading required.

| Token | Size | Weight | Usage |
|---|---|---|---|
| `FontSizeTitle` | 28pt | Bold (700) | Screen titles (Inbox, Today's Digest), Muninn wordmark on Auth |
| `FontSizeBody` | 17pt | Regular (400) | Card titles, body content, input field text |
| `FontSizeBodyMedium` | 17pt | Medium (500) | List row primary labels, nav bar title |
| `FontSizeSecondary` | 13pt | Regular (400) | Timestamps, source domains, AI reason lines, tag labels |
| `FontSizeCaption` | 11pt | Regular (400) | Status bar, fine print |

**Rules:**
- Never introduce a new size outside this scale.
- Large bold titles use `FontSizeTitle`. Everything else is body or secondary.
- The AI-inferred reason line (sparkle icon + italic-light text) uses `FontSizeSecondary` in `ColorTextSecondary`. The sparkle/AI icon precedes it.
- Card titles that are completed get a strikethrough, no size change.

---

## Spacing

**Base unit: 8pt.** Every margin, padding, and gap is a multiple of 8.

| Token | Value | Usage |
|---|---|---|
| `SpaceXS` | 4pt | Icon-to-label gaps, tag internal horizontal padding |
| `SpaceSM` | 8pt | Tag internal vertical padding, between secondary elements |
| `SpaceMD` | 16pt | Card internal padding (all sides), section header margins |
| `SpaceLG` | 24pt | Between card groups, screen horizontal margins |
| `SpaceXL` | 32pt | Auth screen vertical rhythm between elements |
| `SpaceXXL` | 48pt | Auth screen top spacing above wordmark |

Screen edge margins: **24pt** left and right on all screens.

---

## Cards — the primary content unit

Cards are the single most important component. Every save is a card. Get these right.

### Save card anatomy (top to bottom, left to right)

```
┌─────────────────────────────────────────┐
│  [favicon/monogram 40×40]  domain · time    ○  │
│                            Bold title         │
│                            ✦ AI reason line   │
│  [• Category tag]                             │
└─────────────────────────────────────────┘
```

- **Background:** `ColorSurface` (#FFFFFF)
- **Corner radius:** `CornerRadiusCard` = 16pt
- **Shadow:** `ShadowCard` = Y offset 1pt, blur 4pt, color #000000 at 6% opacity. Subtle. Not dramatic.
- **Internal padding:** `SpaceMD` (16pt) all sides
- **Favicon/monogram:** 40×40pt, 10pt corner radius, `ColorSunken` background, `ColorTextSecondary` initial letter at `FontSizeBody` medium weight
- **Domain + timestamp:** same line, `FontSizeSecondary`, `ColorTextSecondary`
- **Title:** `FontSizeBody` bold, `ColorTextPrimary`, max 2 lines then truncate
- **AI reason line:** sparkle SF Symbol (small), `FontSizeSecondary`, `ColorTextSecondary`, italic preferred
- **Category tag:** `ColorAccentTint` background, `ColorAccentTintText` text, bullet prefix, 20pt corner radius (fully rounded), `FontSizeSecondary`
- **Complete circle:** right-aligned, 24×24pt. Empty = `ColorSeparator` border, transparent fill. Completed = `ColorAccent` fill, white checkmark SF Symbol.

### Completed card state
- Circle: filled `ColorAccent`, white checkmark
- Title: strikethrough, color changes to `ColorTextTertiary`
- AI reason and tag: fade to `ColorTextTertiary`
- Card background: stays white, no color change

---

## Buttons

**One primary button per screen. Always full-width on mobile.**

| Variant | Background | Text color | Usage |
|---|---|---|---|
| Primary (filled) | `ColorAccent` | White | Sign in, Save link, Mark complete |
| Secondary (tinted) | `ColorAccentTint` | `ColorAccentTintText` | Archive, Add, secondary actions |
| Plain | Transparent | `ColorAccentTintText` | Cancel, text-only actions |
| Destructive | System red tint | System red | Destructive actions only |

- **Corner radius:** `CornerRadiusButton` = 14pt
- **Height:** 52pt for primary full-width, 44pt for inline buttons
- **Font:** `FontSizeBody` medium weight
- **Full-width primary:** 24pt horizontal margin from screen edge (matches `SpaceLG`)

---

## Input fields

- **Background:** `ColorSurface`
- **Border:** 1pt `ColorSeparator` at rest, 2pt `ColorAccent` when focused
- **Corner radius:** `CornerRadiusInput` = 12pt
- **Height:** 52pt
- **Padding:** 16pt horizontal
- **Placeholder:** `FontSizeBody`, `ColorTextTertiary`
- **Text:** `FontSizeBody`, `ColorTextPrimary`
- The paste-a-link field in the Inbox has a chain-link SF Symbol prefix icon in `ColorTextTertiary`

---

## Tab bar

Three tabs: **Inbox**, **Digest**, **Archive**.

- **Style:** iOS 26 Liquid Glass — translucent blur, do not set a solid background color
- **Active tab:** icon + label in `ColorAccent`
- **Inactive tab:** icon + label in `ColorTextSecondary`
- **Icons (SF Symbols):** tray (inbox), sparkles (digest), archivebox (archive)
- Tab bar sits at the bottom of the screen and floats above content

---

## Navigation bar

- **Style:** Liquid Glass — large title style (title left-aligned, large, scrolls to inline on scroll)
- **Title:** `FontSizeTitle`, `ColorTextPrimary`, bold
- **Trailing icons:** SF Symbols, `ColorTextPrimary`, 22pt
- Do not set a solid nav bar background — let it stay translucent

> **Platform note:** This nav bar / header spec describes the **iOS build** (iPhone/iPad), which renders native iOS large-title nav bars and Liquid Glass chrome. The **Mac Catalyst build** uses native macOS window chrome instead — a centered toolbar title in the window title bar — which is expected and correct for a Mac app, not a bug. Always verify the header/nav design against the **iOS Simulator**, not the Mac build.

---

## Empty states

Centered vertically in the content area. Three elements only: SF Symbol icon (large, `ColorTextTertiary`), bold title (`FontSizeBody` bold, `ColorTextPrimary`), and one line of supporting copy (`FontSizeSecondary`, `ColorTextSecondary`).

Examples from the prototype:
- Archive: archivebox icon, "Nothing archived yet", "When you complete or archive a save, it rests here."
- Digest (no saves): sparkles icon, "You're all caught up", "Check back tomorrow."

No buttons in empty states unless the action is the obvious recovery.

---

## Auth screen

Special layout — no tab bar, no nav bar.

- Background: `ColorBackground`
- Top spacing before wordmark: `SpaceXXL` (48pt) plus safe area
- **Wordmark:** "Muninn" in `FontSizeTitle` (28pt) bold, `ColorTextPrimary`, centered
- **Tagline:** "your memory, returned" in `FontSizeSecondary`, `ColorTextSecondary`, centered, 8pt below wordmark
- `SpaceXL` (32pt) gap before first input field
- Email field, 12pt gap, Password field
- `SpaceMD` (16pt) gap, then full-width primary Sign in button
- `SpaceSM` (8pt) gap, then "Don't have an account? Register" in `FontSizeSecondary`, centered, `ColorTextSecondary` with `ColorAccentTintText` on the "Register" tap target
- The toggle between Sign in / Register changes the button label and the toggle line copy only — no navigation

---

## Digest screen

- Nav bar title: "Today's Digest" (`FontSizeTitle`)
- Date subtitle: `FontSizeSecondary`, `ColorTextSecondary`, directly below title, 4pt gap
- **Urgency banners:** appear above their associated card. `ColorSunken` background, `ColorTextSecondary` text, 12pt corner radius, clock SF Symbol prefix. Copy is contextual and written by the AI — time-sensitive framing ("Portland trip is in 4 days. Still no dinner reservation."). Never olive.
- Cards: identical to Inbox save cards
- Cards in the digest do not show the complete circle — they link to Save Detail instead

---

## Liquid Glass usage rules

Glass is used in exactly two places:
1. **Tab bar** — always
2. **Navigation bar** — always

Glass is never used on:
- Save cards (solid white)
- Input fields (solid white)
- Buttons (solid or tinted)
- Any content surface

This restraint is intentional. Glass on every surface is noise. Glass only on chrome keeps the content legible and the interface calm.

---

## Corner radius reference

| Token | Value | Used on |
|---|---|---|
| `CornerRadiusCard` | 16pt | Save cards, link preview cards |
| `CornerRadiusButton` | 14pt | All buttons |
| `CornerRadiusInput` | 12pt | Text input fields |
| `CornerRadiusTag` | 20pt | Category tags (fully pill-shaped) |
| `CornerRadiusFavicon` | 10pt | Favicon/monogram containers |
| `CornerRadiusBanner` | 12pt | Digest urgency banners |

---

## Things to never do

- Hardcode a color hex or font size in XAML — use named resources from `Styles.xaml`
- Use a solid background on the tab bar or nav bar
- Put solid olive on a large background area
- Add a divider line where spacing handles the separation
- Introduce a font size outside the five-size scale
- Use more than one primary button per screen
- Add decoration (gradients, patterns, illustrations) that doesn't carry information
- Change the card corner radius per screen — it's always 16pt
