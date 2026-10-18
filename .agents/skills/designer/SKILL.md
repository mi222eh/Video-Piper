---
name: designer
description: This skill should be used when the user asks to "design", "style", "improve the UI", "make it look better", "redesign a view", "add a card/row/button", "pick colors", "improve the UX", "design the interaction", "handle empty/loading/error states", "add progress or feedback", "handle a destructive action or confirmation", or needs help matching the Video-Piper design system or interaction patterns. It codifies the project's visual language (Vp* state tokens, WinUI theme resources, corner radii, spacing, type scale, C# Markup patterns), universal Fluent 2 / WinUI 3 conventions, and the app's UI/UX behavior rules (states, long-running feedback, destructive actions, scannability, accessibility) — so new UI looks right and behaves right instead of drifting.
---

# Video-Piper Designer

Use this skill for any UI work in the Video-Piper app (`video-piper/VideoPiper/`).
It captures the project's design system — extracted from `App.xaml` and
`MainPage.cs` — together with the Fluent 2 / WinUI 3 rules that those values
follow. Goal: new surfaces look like they were part of the app from the start.

## When to use

- Adding or restyling a view, card, row, button, or dialog.
- Choosing a color, corner radius, spacing, or font size for a new element.
- Reviewing whether a change matches the existing design language.
- Fixing a surface that looks off (wrong tone, inconsistent radius, theme drift).
- Deciding how the UI **behaves** — states (empty/loading/error), progress &
  feedback, destructive actions, affordances, or accessibility.

## The hard rules (project-specific)

1. **Swedish strings, always.** Every user-facing string is Swedish
   ("Ladda ner", "Klart", "Misslyckades", "I kön", "Rensa klara",
   "Inga loggar ännu."). Never ship an English-only string.
2. **UI is C# Markup, not XAML.** Views live in `MainPage.cs`
   (`Uno.Extensions.Markup`). Shared row `DataTemplate`s live in `App.xaml`
   resources and are fetched with
   `Application.Current.Resources["Key"] as DataTemplate`.
3. **Inside `.Convert(...)` lambdas: never `ThemeResource.Get<Brush>(…)`** —
   it is a markup helper that returns a builder, not a `Brush`. Use a plain
   `new SolidColorBrush(...)` or the `BrushFromResource(key, fallback)` runtime
   lookup in `MainPage`.
4. **State colors only from the `Vp*` tokens** (below) — never ad-hoc
   `ColorHelper.FromArgb` for success/warning/error.
5. **Theme-aware surfaces only.** Backgrounds/foregrounds come from WinUI
   theme resources (`CardBackgroundFillColorDefaultBrush`,
   `TextFillColorSecondaryBrush`, …) so dark/light both work. The app has a
   persisted theme toggle (`App.SetTheme`) — hardcoded non-state colors break
   one of the themes.
6. **Uno XAML gotchas** (they will fail the build or silently no-op):
   - No `TabView<T>` generics or `TabView.Items`.
   - No `RelativeSource AncestorType=…` (the compiler rejects it).
   - A command inside an ItemTemplate can't reach the page/VM — wire
     `ListView.ItemClick` in code-behind instead of an ancestor binding.
   - `SelectionItemPattern.Select()` does not fire `ItemClick` (automation
     caveat, not a design rule).
    - `ToolTipService` lives in `Microsoft.UI.Xaml.Controls` (NOT
      `Controls.Primitives` like WinUI 3), and `Window` has no `XamlRoot` — take it
      from the window's content element
      (`(App.MainWindowInstance?.Content as UIElement)?.XamlRoot`).

## Design tokens

### State colors (`App.xaml` — the only allowed source for state)

| Token | Value | Use |
|---|---|---|
| `VpSuccessColor` | `#FF22C55E` | success text/glyph |
| `VpWarningColor` | `#FFEAB308` | warning text/glyph |
| `VpErrorColor` | `#FFEF4444` | error text/glyph |
| `VpSuccessBrush` / `VpWarningBrush` / `VpErrorBrush` | solid | foregrounds |
| `VpSuccessBgBrush` | `#2222C55E` | tinted card background |
| `VpSuccessBorderBrush` | `#6622C55E` | tinted border |
| `VpWarningBgBrush` / `VpWarningBorderBrush` | `#22…` / `#66…` | same pattern |
| `VpErrorBgBrush` / `VpErrorBorderBrush` | `#22…` / `#66…` | same pattern |

The `Bg` variant is 13% alpha (`#22`), the `Border` variant 40% (`#66`).
`StatusToBrushConverter` (`Converters/StatusToBrushConverter.cs`) maps
`ItemStatus` → these brushes for queue rows; register new status converters in
`App.xaml` resources the same way.

### Theme resources (structural, theme-aware)

- Cards: `CardBackgroundFillColorDefaultBrush` + `CardStrokeColorDefaultBrush`
  (1px border).
- Elevated layers inside a card: `LayerFillColorDefaultBrush`.
- Subtle chips/badges: `SubtleFillColorSecondaryBrush`.
- Text: primary = `TextFillColorPrimaryBrush` (default); secondary =
  `TextFillColorSecondaryBrush` (metadata, timestamps, counts); tertiary =
  `TextFillColorTertiaryBrush` (empty-state icons).
- Accent (primary buttons/progress): `AccentFillColorDefaultBrush`; primary
  buttons use `Style(ThemeResource.Get<Style>("AccentButtonStyle"))`.

## Shape language (cornerRadius)

Measured across `MainPage.cs` + `App.xaml` — reuse these values, don't invent
new ones:

| Radius | Used for |
|---|---|
| `2` | hairline progress bars (Height 3–4) |
| `4` | small chips/badges (e.g. "MP3"/"MP4", "Kö N") |
| `6` | buttons (the workhorse), small icon boxes |
| `8` | input boxes (search box, omnibox), medium cards, icon tiles |
| `10` | large cards (queue card, progress card) |
| `12` | the largest containers |

Rule of thumb (Fluent 2): outer container radius = inner element radius + 4.
If a card is `10`, the button inside it is `6`–`8`; the badge inside the button
is `4`.

## Spacing

- **Grid rhythm**: `RowSpacing`/`ColumnSpacing` are `6`, `8`, `10`, `12`,
  `14` (12 is the default between cards/sections; 8 within a card).
- **StackPanel `Spacing`**: `4` (icon+label pairs, button internals), `6`
  (row content), `8` (card sections), `10`–`12` (looser groupings).
- **Card padding**: `12,10,12,10` or `12,12,12,12` for cards; `10,8,10,8` for
  rows inside a card.
- **Button padding**: primary `14,9,14,9` (or height 48 stretched); secondary
  `10,4,10,4`; small `8,2,8,2` / `6,2,6,2`.
- **Icon tiles**: `36×36`, radius `8` (library/search rows).
- **4-px baseline grid** is the Fluent 2 unit — prefer multiples of 4; the
  values above are all on-grid.

## Type scale

| Size | Role |
|---|---|
| `10`–`11` | timestamps, log lines, tiny badges |
| `12` | secondary text, metadata (uploader, duration) |
| `13` | body text, row titles, buttons |
| `14` | emphasized row title / medium heading |
| `16` | section heading / empty-state title |
| `17`–`20` | view titles |
| `44` | empty-state glyphs (`FontIcon`) |

Weights: `SemiBold` for headings/titles, `Medium` for sub-headings, default
(regular) for body. Font: the app default (Segoe UI Variable on Windows) —
never set an explicit font family.

## Layout patterns (reuse, don't reinvent)

- **Card** = `Border` { `CornerRadius(10)`,
  `Background(ThemeResource.Get<Brush>("CardBackgroundFillColorDefaultBrush"))`,
  `BorderBrush(ThemeResource.Get<Brush>("CardStrokeColorDefaultBrush"))`,
  `BorderThickness(1)`, `Padding(12, 10, 12, 10)` }.
- **List row** = `Grid` with a 36×36 radius-8 icon tile in column 0, title +
  secondary metadata stacked in column 1, a chip or control in column 2
  (right-aligned). Row templates live in `App.xaml`
  (`QueueEntryTemplate`, `SearchResultTemplate`, `LibraryItemTemplate`).
- **Chip/badge** = `Border` radius 4, `Padding(6,2,6,2)`,
  `Background(SubtleFillColorSecondaryBrush)`, 11px secondary text.
- **Progress**: thin `ProgressBar` (height 3–4, radius 2); indeterminate
  `ProgressRing` (14–32px) for fetching.
- **Section header** = 13px `SemiBold` label + 12px secondary count, with a
  14px glyph.
- **Empty state** = centered 44px tertiary glyph + 16px SemiBold title +
  12px secondary description + an optional CTA button.
- **Primary action** = `AccentButtonStyle`, radius 8, padding `14,9,14,9`;
  **secondary action** = default button, radius 6, padding `10,4,10,4`.

## Binding & MVVM patterns

- Bind with fluent `.Binding(() => vm.Prop)` expressions; two-way where the
  control drives the model (e.g. `SelectedQueueEntry`, `SearchQuery`).
- Inline visibility/conversion: `.Visibility(x => x.Binding(() => vm.X)
  .Convert(v => v ? Visibility.Visible : Visibility.Collapsed))`.
- Commands via `RelayCommand` / `ParameterizedRelayCommand`
  (`ViewModels/RelayCommand.cs`); refresh `CanExecute` when relevant state
  changes.
- View models implement `INotifyPropertyChanged` with a private
  `Set(ref field, value)` helper.
- **Threading**: never mutate a UI-bound object off the UI thread (WinRT
  `COMException` → silent worker death). VM mutations → `OnUi(...)`;
  `LibraryItem` model mutations are safe anywhere (its `PropertyChanged` is
  marshaled via the `PostToUi` hook wired in `App.OnLaunched`).

## Fluent 2 / WinUI 3 conventions (the universal layer)

- **8-pt grid** for macro layout, **4-pt grid** for micro spacing.
- **Roundedness**: 4 / 8 / 12 as the base scale; the project maps these to the
  chip/box/card steps above.
- **Elevation by layer, not shadow**: WinUI 3 expresses depth with layered
  fills (`LayerFillColorDefaultBrush` over `CardBackgroundFillColorDefaultBrush`)
  and 1px strokes (`CardStrokeColorDefaultBrush`), not drop shadows.
- **Type**: Segoe UI Variable, 13px body, 12px caption, 16–20px headings;
  SemiBold (600) for headings, Medium (500) for sub-headings.
- **State feedback**: success/warning/error via the `Vp*` tokens (13% bg /
  40% border / solid foreground); neutral states via `SubtleFillColor*`.
- **Dark/light**: everything structural comes from `ThemeResource` lookups so
  the persisted theme toggle works with zero extra code.
- **Motion**: keep it minimal — WinUI default transitions (row hover, card
  enter) are enough; do not add explicit animations.

## UI & UX best practices (the behavior layer)

The sections above cover how the app **looks**; this covers how it **behaves** —
the interaction rules that keep a download manager usable. Grounded in this app's
real patterns (file refs) and the Fluent 2 / Nielsen heuristics cited per cluster.
Where it overlaps the visual layer: appearance follows the tokens above, behavior
follows this section.

### Interaction & commands
- **Place commands by frequency**: high-frequency on the primary surface (search box,
  the "Sök"↔"Ladda ner" context button, download buttons), context-dependent ones in
  the selected-item bar / menus.
- **Prefer direct manipulation**: row-click opens the item (search result → info page);
  don't add a per-row button that duplicates the row's click target. (The fake per-row
  "Ladda ner" was removed for a chevron for exactly this reason.)
- **Keyboard is primary, not a fallback**: every control focusable + invokable
  (Enter/Space); tab order = visual order; real `Button`s, not `PointerPressed` on
  images.

### Feedback & status (long-running work)
- **Match the indicator to the duration**: indeterminate `ProgressRing` for fetches
  (search, metadata); determinate % bar for the download; switch to determinate the
  moment a total is known. A stalled bar reads as a frozen app.
- **Show what remains, not just what's done**: % + "N i kön" position + a live
  per-item log (diagnoses a stuck job). Estimates rough, never fake-precise.
- **Non-modal, cancellable, recoverable**: downloads run in a background queue; every
  job has an obvious **Avbryt**; every failed job has a first-class **Försök igen**
  (three entry points: failed-section CTA, error card, queue log panel). This is the
  app's core UX contract (Nielsen #9: recognize/diagnose/recover).
- **Completion must be salient**: the user walks away during downloads — the success
  state (queue row → "Klart" + check; simple-mode finish card) is the reward.

### Empty / loading / error / success states
- **Distinguish "loading" from "empty"**: an empty list while a fetch is running is a
  lie about system state — show the indicator first, then a true empty state.
- **Never a bare empty panel**: status + learning cue + a CTA (search "Sök efter musik
  på YouTube", the collection empty state, queue "Inga loggar ännu.").
- **Match severity to presentation**: informational → inline; a blocking failure → a
  highlighted error card next to the failed item, not a modal. Inline errors never
  belong in a dialog.
- **Error copy** (every Swedish error string): plain language, no raw `.NET` text,
  specific + constructive + non-blaming, next step stated. Raw `ex.Message` is never
  shown: every VM error path goes through `UiErrors.FromException(ex, fallback)`
  (`ViewModels/UiErrors.cs`), which maps known failure classes (access denied,
  file/directory not found, network/TLS) to Swedish messages with a next step and
  falls back to a Swedish string otherwise. Add new mappings there, not in the VMs.

### Destructive actions
- **Reserve confirmation for the truly irreversible.** Per-item **Ta bort** confirms
  only in the default `DeleteFiles` mode (`MainPage.ConfirmDeleteAsync` — a
  `ContentDialog` that names the concrete consequence: the file is removed from disk,
  it cannot be undone); `KeepFiles` mode never asks because the media survives. A
  generic "Är du säker? Ja/Nej" without the consequence is useless (reflex "Ja").
- **Separate destructive from benign**: "Ta bort" visually + spatially distinct from
  "Öppna mapp" (VpError tokens, not adjacent to a benign action).
- **Queue clears**: "Rensa väntande" removes only not-yet-started jobs (the active
  download keeps running) and "Rensa klara" removes only finished/failed rows — both
  are low-risk and need no confirmation, but the labels must stay unambiguous
  (never a bare "Rensa" next to "Rensa klara").

### Information hierarchy & scannability
- **Lead each row with the most important fact** (title); secondary metadata
  (uploader · duration, size, format chip) lower-emphasis — users scan, not read.
- **Recognition over recall**: enough per-row context (title, channel, duration,
  format) to identify an item without opening it.
- **Status badges are redundant**: color **and** text/icon, never color alone
  (`StatusToBrushConverter` supplies the color — always pair it with the status text).
- **Progressive disclosure**: primary actions visible; advanced options
  (per-item format) on the per-item bar / info page, not on every row.

### Affordances & the next step
- **Every terminal state names its next step**: empty → "paste a link"; done →
  "Öppna fil / Öppna mapp"; failed → "Försök igen" (the finish card's "Ladda ner en
  to" is the model).
- **No silent no-ops**: a control the user can't use must say why — the info page's
  disabled download buttons are always accompanied by the reason text ("Den här videon
  finns redan i biblioteket.") above them.
- **Consistency**: same word + glyph + placement for the same action everywhere.

### Persistence
- **Remember what matters on relaunch**: window size (done), last mode/format/quality
  (done), library root (done). Gaps: the **queue is session-only** (a restart silently
  drops queued jobs — bad for a start-and-walk-away task) and the active sort/filter
  /section aren't restored.

### Trust & safety (downloader-specific)
- **Show cost before commit**: estimated size + duration before download (the info
  page's duration is a start), destination folder per item, source attribution
  (channel).
- **Be truthful about progress**: fake-precise ETAs, frozen bars, or "no results"
  while loading erode trust faster than honest partial info.

### Accessibility
- **High-contrast**: never hard-code colors in the HC dictionary; **don't set explicit
  `TextBlock.Foreground` in list data templates** (breaks selection-state inversion —
  a real trap for the App.xaml row templates).
- **Never encode meaning by color alone** — pair the `Vp*` state color with a glyph or
  text.
- **Test at 200% text scaling** and with Narrator; long rows truncate, don't overlap.
- **Motion** (complements the rule above): WinUI default transitions only; add a
  no-motion path (WCAG) before introducing any animation.

### Repo gaps worth closing (code-level UX, not yet addressed)
Closed by the designer audits (Oct 2026, two passes):
- Pass 1: a zero-result search now shows an "Inga träffar" state (a search *error*
  resets to the idle state + error banner instead, so it never falsely shows "no
  results"), and the 8 hardcoded `ColorHelper.FromArgb` state-color sites now use the
  `Vp*` tokens.
- Pass 2 (non-confusion audit): delete gets a consequence-named confirmation in
  `DeleteFiles` mode; raw `ex.Message` is mapped through `UiErrors`; the global
  format toggle is labeled "Format" + tooltip (default for new downloads); queue
  buttons are "Rensa väntande" / "Rensa klara"; the queue log panel has a "Dölj"
  close; the info page has exactly one accent button; icon-only buttons (theme,
  gear, player close, pop-out, error close, deselect, resync) all carry tooltips;
  the theme toggle is a FontIcon (moon/sun); 'Visa' is now 'Öppna mapp'; queue
  counts read "N i kön".

Still open:
1. ~~Delete (default `DeleteFiles`) had no confirmation or undo.~~ (closed — pass 2)
2. ~~Disabled info-page buttons gave no reason text.~~ (closed — reason text was in the
   code all along; verified)
3. ~~Zero search results showed a bare empty list.~~ (closed — "Inga träffar" state)
4. ~~Raw `ex.Message` (English) leaked into Swedish error banners.~~ (closed — `UiErrors`)
5. Queue not persisted across restarts.
6. A single shared `Error` slot in library mode — search/download/resync overwrite.

### Sources (authoritative UX guidance)

- **Fluent 2 principles & motion**: fluent2.microsoft.design/design-principles,
  /motion.
- **Windows 11 app design** (principles, best practices, keyboard, high-contrast,
  app-settings, progress controls, dialogs & flyouts, notifications, targeting):
  learn.microsoft.com/windows/apps/design/design-principles,
  /get-started/best-practices, /design/accessibility/keyboard-accessibility,
  /design/accessibility/high-contrast-themes,
  /design/app-settings/guidelines-for-app-settings,
  /develop/ui/controls/progress-controls,
  /develop/ui/controls/dialogs-and-flyouts/dialogs,
  /develop/notifications/app-notifications/app-notifications-ux-guidance,
  /develop/input/guidelines-for-targeting.
- **Nielsen's usability heuristics** (nngroup.com): ten-usability-heuristics,
  empty-state-interface-design, error-message-guidelines, progress-indicators,
  designing-for-waits-and-interruptions, confirmation-dialog, user-control-and-freedom,
  proximity-consequential-options, recognition-and-recall, progressive-disclosure,
  consistency-and-standards.
- **WinUI 3 Gallery** (github.com/microsoft/WinUI-Gallery) is the reference control
  implementation.

## Verification

WinUI 3 UI can't be verified with screenshots in this environment (GPU
composition yields near-black captures). Verify with UIA:
`powershell` + `System.Windows.Automation` — find the app window by
`ProcessIdProperty`, then `FindFirst`/`FindAll` over
`[TreeScope]::Subtree` and print `ControlType` + `Name`. Swedish characters
come through mangled (ö→`"`, å→`„`, ä→`†`) — match with wildcards
(`"YouTube-s*"`, `"*Laddar ner*"`), never literals. Real `mouse_event` clicks
at `BoundingRectangle` centers are required for `ListView.ItemClick`
(`Select()` doesn't fire it). Build with `dotnet build VideoPiper.sln` from
`video-piper/`; test with `dotnet test` (expect the full green suite, 41 tests).
**DSH sandbox note**: `dotnet test` inside the repo crashes at test-host startup
(access denied — a workspace-context artifact); copy the tree outside the repo
(`Copy-Item video-piper C:\Users\micke\vp-test-run -Recurse`) and run it there.
