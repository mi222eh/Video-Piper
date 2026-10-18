# UX Standards — Video-Piper UI (WinUI 3 / Fluent 2, Swedish)

Actionable standards for building and reviewing the app's UI: card-based layout, list rows,
buttons, progress bars, download queue, search box, settings page, inline status cards.
Rules are imperative and directly applicable. Sources: [NN/g](https://www.nngroup.com/articles/ten-usability-heuristics/),
[Microsoft Learn Fluent/WinUI control guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/),
[Laws of UX](https://lawsofux.com/).

---

## UX standards

### Feedback & system status

**1. Acknowledge every action within 100 ms** ([response-time limits](https://www.nngroup.com/articles/response-times-3-important-limits/)).
- <0.1 s: the user must see the result (pressed state, row change, badge count update). If the click produces no visible change, users assume it was not registered and click again.
- 0.1–1 s: the user notices the delay — show a working indicator (busy state on the button, row switches to "I kön").
- >10 s: show a determinate progress bar with percentage/ETA **and** a way to interrupt it.
- Example: clicking "Ladda ner" instantly flips the queue row to "I kön"; when the worker starts, the thin progress bar appears — no silent gap between click and state change.

**2. Show progress matching what is known** ([MS progress controls](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/progress-controls), [NN/g progress indicators](https://www.nngroup.com/articles/progress-indicators/)).
- Known duration (downloads — always): determinate `ProgressBar` with real percentage. Indeterminate bars/spinners are for truly unknown waits (metadata fetch, tool update check).
- A spinner alone says *something* is happening, not *what*. Pair any indicator with text ("Hämtar metadata…", "Nedladdas… 43 %").
- One progress element per task. Don't drop a spinner on every row of a virtualized list — one bar at the top of the collection is enough.
- Example: queue row = status glyph + title + "43 %" + thin determinate bar; the "Kö N" badge stays updated while the list scrolls.

**3. Confirm destructive/irreversible actions — or offer undo** ([MS dialog guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs)).
- Deleting files, clearing the queue: `ContentDialog` with a *specific* question, not "Är du säker?". The destructive verb is the primary button: "Ta bort videon?" → [Ta bort] [Avbryt].
- Every dialog always has one safe, nondestructive button (rightmost); "do it" buttons go leftmost. Specific responses ("Tillåt"/"Blockera", "Ta bort"/"Avbryt") beat generic OK/Cancel.
- Prefer undo over a confirmation dialog when the cost of a wrong action is low; confirm only what cannot be undone (files deleted from disk).

**4. Error messages: plain language + cause + next step** ([NN/g heuristics #9](https://www.nngroup.com/articles/ten-usability-heuristics/), [MS InfoBar](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/infobar)).
- Format: what happened → (why, if it helps) → what the user can do. All in Swedish; never raw codes ("yt-dlp exit 255") or English strings.
- Show the error where it happened — inline on the affected card/row, not a far-away modal. Validation errors belong to the field (inline, red border + short text), not a dialog ([MS dialogs: don't use dialogs for contextual errors](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs)).
- Never signal meaning by color or icon alone: state color **plus** glyph **plus** text (also an accessibility requirement).
- Example: error card "Nedladdningen misslyckades — network timed out" + [Försök igen] button.

**5. Every long operation has an emergency exit** ([NN/g heuristics #3](https://www.nngroup.com/articles/ten-usability-heuristics/)).
- Running downloads: visible cancel (Avbryt) on the queue card while active.
- After cancel, the new state must be visible and stable: active row shows "Avbruten", remaining rows still "I kön" — never a frozen row with no explanation.

### Cognitive load & layout

**6. Limit choices per decision** ([Hick's law](https://lawsofux.com/hicks-law/)).
- Decision time grows with the number and complexity of options: ≤ 2–3 actions per row/card; the rest go to an overflow (⋯) menu.
- Exactly one accent (primary) button per view; everything else is neutral/quiet. Two competing accent buttons = no primary action.
- Split complex flows into steps (URL → format → start) instead of one big form.

**7. Chunk related content** ([NN/g chunking](https://www.nngroup.com/articles/chunking/)).
- Working memory holds ~7±2 chunks ([Miller](https://lawsofux.com/millers-law/)) — never ask a user to track more than a handful of items across one decision.
- One card = one concern (status card, queue card, settings card). Group related fields under a header; in forms keep 24 px between fields and 48 px between groups ([MS forms](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/forms)).
- Present numbers in recognizable groups (12.4 MiB, 01:23:45, "43 %") — never raw byte counts or epoch seconds.

**8. Big targets, near their context** ([Fitts's law](https://www.nngroup.com/articles/fitts-law/)).
- Keep click targets ≥ 40×40 px and never make a bare icon the only hit area — icon **+ label** is a bigger, faster, unambiguous target.
- Place actions next to the content they act on: row actions in the row (or its ⋯ menu), Avbryt on the queue card, retry on the error card — not across the window.
- Screen edges are free ("infinite") targets for frequent things (app bar, left sidebar); keep rarely used actions in overflow menus.

**9. Recognition over recall** ([NN/g memory article](https://www.nngroup.com/articles/recognition-and-recall/)).
- Show options instead of making users type from memory: recent folders, previously used links, library items, queue history.
- Keep context visible: selected item, active collection, current filter. Never require remembering what was open.
- Use lists/combo boxes for known sets (quality, framerate, sort order); free text only for genuinely open input (search query, pasted URL).

**10. First seconds: the window must explain itself** (3-second first-impression rule; [Jakob's law](https://www.nngroup.com/videos/jakobs-law-internet-ux/)).
- Within seconds of launch: app bar/title says what the app is, the hero area shows the main content (library list or search results), the mode switcher is self-evident.
- Persist what reduces reorientation: window size, app mode, last library root, last folder.
- The main task must not be covered by chrome: player dock, status cards and queue sit in a bottom band and collapse when idle — the browse area is the hero.

**11. Follow platform conventions** ([Jakob's law](https://www.nngroup.com/videos/jakobs-law-internet-ux/), [NN/g heuristics #4](https://www.nngroup.com/articles/ten-usability-heuristics/)).
- Users arrive from other Windows apps: gear = settings, search box + button = search, red/inline = error, confirm dialog = delete, grouped toggles = settings page.
- Use standard Fluent controls with default behavior: `ToggleSwitch`, `ComboBox`, `ProgressBar`, `InfoBar`, `ContentDialog`. Restyle freely, but never change what a control *does*.
- Consistency beats novelty: if a pattern is standard in Windows apps, use it as-is.

### Consistency & standards

**12. One term per concept, everywhere** ([NN/g heuristics #4](https://www.nngroup.com/articles/ten-usability-heuristics/)).
- Same action = same label app-wide: "Ladda ner" is always "Ladda ner" — never "Hämta"/"Spara"/"Nedladda" for the same thing. "Ta bort" for deleting, everywhere (selected-item bar, dialog, menu).
- Same state = same color + glyph everywhere: use the shared `Vp*` state tokens so a "failed" item looks identical in the library list, the queue panel, and the error card.
- Keep every string in one localized string table (Swedish UI — no English fallbacks leaking through).

**13. Same shape = same behavior** ([MS buttons](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/buttons)).
- Accent button = the primary action of its context; neutral button = secondary; icon button = minor. Never swap roles between views.
- If one row is clickable/selectable, all rows of that list must show the same hover/selected affordance (library rows, search results, queue rows).
- "Disabled" must always mean "not available *right now*, for a reason the user can understand" — never silently, never as a bug.

### Minimalism & progressive disclosure

**14. Primary first, advanced on demand** ([NN/g progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/)).
- Surface only what the common task needs: paste link → pick format → download. Advanced options (max resolution, min framerate, delete mode) live on the settings page.
- The split is the hard part: everything users need *often* must be on the surface; hide only the rare. Label the entry to the advanced level so users can predict its contents ("Inställningar", "Fler alternativ").
- Per-row detail (queue log) opens on click, not permanently expanded — one expanded log per queue card at most.

**15. Hide by state, not arbitrarily.**
- Cards/panels collapse only when they have nothing to show: queue card hidden when empty, status card when idle.
- Hidden ≠ forgotten: a hidden area must have a discoverable entry point (the "Kö N" badge, the sidebar section with a count).
- Prefer visible-but-disabled over hidden for actions that exist but are currently unavailable (e.g. "Ladda ner" disabled with the reason shown).

### Labels & microcopy

**16. Button labels: verbs, user-centric, specific** ([MS button text rules](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/buttons)).
- Name the action, in the user's language: "Ladda ner MP3", "Försök igen", "Öppna mapp", "Klistra in" — not nouns ("Nedladdning") and not system terms ("Execute", "Retry job").
- Specific beats generic: the delete dialog's primary button is "Ta bort", the search box's button is "Sök".
- Keep labels short but plan for Swedish compounds — they run longer than English; cap ~26 characters and allow a wider/wrapping button instead of truncation.

**17. Icon + label over icon-only; tooltips for the rest** ([MS tooltips](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/tooltips), [NN/g Fitts's law: icons need labels](https://www.nngroup.com/articles/fitts-law/)).
- Default: labeled button. Icon-only only for the most universal glyphs (theme toggle, pop-out, gear) — and those always get a tooltip.
- Tooltips are supplemental: if the user *needs* the info to complete a task, put it in the UI as a label or inline text, not behind hover.
- Tooltips: short, never repeat the visible label, never contain interactive content, never used for errors/warnings/status (use `InfoBar`/inline text for those).

**18. Empty states say what belongs there + how to get there.**
- An empty list is not a blank area: one line of what should be there + one action that fills it. "Inga filer än — klistra in en YouTube-länk" or a search CTA.
- Empty search results restate the query and suggest a fix: "Inga resultat för '…' — kontrollera stavningen eller försök igen."
- Empty queue: hide the card entirely — an empty table with headers is noise.

**19. Settings: labeled, instant, grouped** ([MS toggle switches](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/toggles), [MS forms](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/forms)).
- Each toggle: a short header naming the setting (1–3 words) + a one-line description of its effect. Changes apply **immediately** — no "Apply" button on the settings page.
- `ToggleSwitch` for binary settings that take effect at once (dark theme, delete mode); `ComboBox` for enumerated values (resolution, framerate, sort order). Use a checkbox only when a submit button applies the change.
- Group related settings into one card with a header (Bibliotek: delete mode + quality + framerate); the whole page should be scannable in under 10 seconds.

---

## Common confusion antipatterns

1. **Ambiguous button labels** — "OK"/"Submit"/"Gör" on everything. *Fix:* label buttons as specific answers to the question on screen: "Ladda ner", "Ta bort", "Sök".
2. **Icon-only controls with no label or tooltip** — user must guess the glyph. *Fix:* add a text label (which also enlarges the target per Fitts's law) or, at minimum, a tooltip on universal glyphs.
3. **Hidden/stuck state** — a row sits at "I kön" with no motion, no percentage, no text; the user can't tell if it works. *Fix:* every row shows status text + progress; the system always explains what it is doing now.
4. **Inconsistent terminology** — "Ta bort" in one place, "Radera" in another; "Nedladdas" vs "Hämtas" for the same state. *Fix:* one term per concept in the shared string table; enforce in review.
5. **Confirmation-dialog fatigue** — a modal for every minor action, so users stop reading and smash "OK". *Fix:* modals only for blocking/irreversible decisions; use inline `InfoBar`/state for everything else; offer undo instead of confirm when possible.
6. **Indeterminate spinners for long downloads** — a spinner for 3 minutes reads as "frozen". *Fix:* determinate percentage + ETA for anything with known progress; reserve spinners for short unknown waits (< 10 s).
7. **Raw error codes / mixed languages** — "yt-dlp: ERROR: unable to download" in a Swedish UI. *Fix:* translate, strip jargon, and end with an actionable step ([Försök igen]).
8. **Color-only state signaling** — a red row with no text; invisible in color-blindness and high-contrast modes. *Fix:* state = color **+** glyph **+** text, always.
9. **Wall of options** — 15 flat toggles on one settings page, or 6 buttons on one card. *Fix:* group into headed cards (chunking), keep ≤ 2–3 actions per row, push the rest to overflow or a second level.
10. **Recall-based interaction** — the user must type the exact save folder from memory or remember the last search term. *Fix:* show suggestions/recents, persist last values, keep the current context visible on screen.

---

## Sources

**Nielsen Norman Group**
- 10 Usability Heuristics for User Interface Design (current wording, rev. Jan 2024) — https://www.nngroup.com/articles/ten-usability-heuristics/
- Visibility of System Status — https://www.nngroup.com/articles/visibility-system-status/
- Response Times: The 3 Important Limits (0.1 s / 1 s / 10 s) — https://www.nngroup.com/articles/response-times-3-important-limits/
- Progress Indicators Make a Slow System Less Insufferable — https://www.nngroup.com/articles/progress-indicators/
- Fitts's Law and Its Applications in UX — https://www.nngroup.com/articles/fitts-law/
- Progressive Disclosure — https://www.nngroup.com/articles/progressive-disclosure/
- Memory Recognition and Recall in User Interfaces — https://www.nngroup.com/articles/recognition-and-recall/
- How Chunking Helps Content Processing (Miller's 7±2, correctly applied) — https://www.nngroup.com/articles/chunking/
- Minimize Cognitive Load to Maximize Usability — https://www.nngroup.com/articles/minimize-cognitive-load/
- Jakob's Law of Internet User Experience — https://www.nngroup.com/videos/jakobs-law-internet-ux/

**Microsoft Learn — Windows apps / Fluent 2 / WinUI 3**
- Controls for Windows apps (overview) — https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/
- Progress controls (determinate vs indeterminate; bar vs ring) — https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/progress-controls
- Buttons (label rules, width limits, ordering) — https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/buttons
- InfoBar (severity levels, when to use vs dialog/flyout) — https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/infobar
- Dialog controls (confirmation patterns, safe action, button placement) — https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs
- Forms (labels, spacing, inline validation, when to disable submit) — https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/forms
- Tooltips (when to use, what to avoid) — https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/tooltips
- Toggle switches (immediate-effect semantics, On/Off labels) — https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/toggles
- Fluent 2 Design System — https://fluent2.microsoft.design/

**Laws of UX**
- Hick's Law — https://lawsofux.com/hicks-law/
- Miller's Law — https://lawsofux.com/millers-law/
