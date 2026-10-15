# ApolloBot: style guide

> Design step 1 of [scope §7.3](../scope.md#73-design-process-before-real-ui-work) (issue #26).
> The living version is the `/styleguide` page of the web app. The values here are the same as in code;
> if they disagree, the code wins and this file gets fixed.

| What | Where in code |
|---|---|
| Colour tokens | `src/Apollo.Web.Client/Theme/ApolloColors.cs` |
| MudBlazor theme | `src/Apollo.Web.Client/Theme/ApolloTheme.cs` |
| Chart palette | `src/Apollo.Web.Client/Theme/ChartPalette.cs` |
| ApexCharts options | `src/Apollo.Web.Client/Theme/ApexChartDefaults.cs` |
| Number/date culture | `src/Apollo.Web.Client/Formatting/UiCulture.cs` |
| Components | `src/Apollo.Web.Client/Components/` (`StatTile`, `SectionCard`, `EmptyState`) |
| Global CSS | `src/Apollo.Web/wwwroot/app.css` (section "Apollo design system") |

Screenshots: [desktop](img/styleguide-desktop.png), [360px](img/styleguide-360.png).

## 1. Direction

- **Dark first.** Dark grey surfaces in the Discord/Fluxer spirit, a light theme may follow later (scope §7.1.1).
- **Own accent.** A blurple-like `Primary` (`#5D66E8`) that is close to, but not, Discord's brand colour. No Discord logos or brand assets.
- **Rounded, flat cards.** No shadows; cards separate from the background by a lighter surface and a 1px `Line` border.
- **Dense but readable.** 15px body text, compact headings, numbers with equal-width digits.
- **Mobile first.** Every page works at 360px: grids collapse to one column, stat tiles stack, charts resize.

## 2. Colour tokens

Surfaces get lighter as they come forward: `Drawer` < `Background` < `AppBar` < `Surface` < `SurfaceRaised`.

| Token | Hex | Use |
|---|---|---|
| `Drawer` | `#18191D` | Navigation drawer, darkest layer |
| `Background` | `#1E1F24` | Page background |
| `AppBar` | `#222328` | App bar |
| `Surface` | `#2A2C32` | Cards, chart background |
| `SurfaceRaised` | `#33363D` | Hover, menus, table stripes, skeletons |
| `Divider` | `#393C44` | Dividers, chart grid |
| `Line` | `#3D4048` | Card borders, chart axes |
| `TextPrimary` | `#F1F2F4` | Body text, headings |
| `TextSecondary` | `#B3B8C2` | Labels, captions, chart labels, drawer text |
| `TextDisabled` | `#6E737D` | Disabled controls, input outlines |
| `Primary` | `#5D66E8` | Filled primary buttons, active state, switches |
| `PrimaryLight` | `#8A91FF` | Primary used **as text** (links, text/outlined buttons), first chart series |
| `Secondary` | `#2FB8A6` | Secondary actions |
| `Tertiary` | `#D96FB4` | Highlights in recaps |
| `Success` | `#3DB86E` | "Erfasst", positive trend |
| `Warning` | `#F0B23E` | Bootstrap mode, attention |
| `Error` | `#F26B70` | Errors, negative trend |
| `Info` | `#4BA3F0` | Hints |
| `OnPrimary` | `#FFFFFF` | Text on `Primary` |
| `OnStatus` | `#0F1114` | Text on `Secondary`, `Tertiary` and status colours |

Every MudBlazor dark-palette slot the style guide uses is set from these tokens (`ApolloThemeTests` checks it),
so no MudBlazor default leaks through.

### Contrast (WCAG 2.x)

Computed with the WCAG relative-luminance formula; `ApolloThemeTests` and `ChartPaletteTests` re-check the thresholds on every build.

**Text on surfaces** (AA for normal text: ≥ 4.5:1)

| Text | on `Background` | on `Surface` | on `SurfaceRaised` | on `Drawer` |
|---|---|---|---|---|
| `TextPrimary` | 14.68 | 12.46 | 10.80 | 15.68 |
| `TextSecondary` | 8.27 | 7.01 | 6.08 | 8.83 |
| `TextDisabled` | 3.46 | 2.93 | 2.54 | 3.69 |

`TextDisabled` is below 4.5 on purpose: WCAG exempts disabled controls.

**Accents** (text on the filled colour ≥ 4.5:1; the colour itself against `Surface` ≥ 3:1 for non-text UI)

| Fill | Text on fill | Ratio | Fill vs `Surface` | Fill vs `Background` |
|---|---|---|---|---|
| `Primary` | `OnPrimary` | 4.63 | 3.01 | 3.55 |
| `PrimaryLight` | `OnStatus` | 6.82 | 5.03 | 5.93 |
| `Secondary` | `OnStatus` | 7.68 | 5.67 | 6.68 |
| `Tertiary` | `OnStatus` | 6.20 | 4.57 | 5.39 |
| `Success` | `OnStatus` | 7.46 | 5.50 | 6.49 |
| `Warning` | `OnStatus` | 10.03 | 7.40 | 8.73 |
| `Error` | `OnStatus` | 6.40 | 4.73 | 5.57 |
| `Info` | `OnStatus` | 7.02 | 5.18 | 6.11 |

`Primary` is only 3:1 against `Surface`, which is enough for fills and borders but not for text.
`app.css` therefore swaps in `PrimaryLight` wherever MudBlazor uses primary as a text colour
(`.mud-button-text-primary`, `.mud-button-outlined-primary`, `.mud-primary-text`).
All other accents reach 4.5:1 on `Surface` and can be used as text directly (e.g. `Error` helper text, trend deltas).

## 3. Typography

System font stack, no web font download and no third-party CDN:
`system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, "Noto Sans", sans-serif`.

| Style | Size | Weight | Use |
|---|---|---|---|
| `h1` | 2.25rem (36px) | 700 | Page title (rare, e.g. recap header) |
| `h2` | 1.875rem (30px) | 700 | |
| `h3` | 1.5rem (24px) | 600 | Section heading on a page |
| `h4` | 1.25rem (20px) | 600 | Stat values |
| `h5` | 1.125rem (18px) | 600 | Sub-section heading |
| `h6` | 1rem (16px) | 600 | Card title (`SectionCard`), empty-state title |
| `subtitle1` | 1rem | 500 | Date range, lead lines |
| `subtitle2` | 0.875rem | 600 | Stat-tile label, swatch name |
| `body1` | 0.9375rem (15px) | 400 | Default text |
| `body2` | 0.875rem (14px) | 400 | Dense lists, tables, secondary lines |
| `caption` | 0.75rem (12px) | 400 | Hints, hex values, footnotes |
| `overline` | 0.6875rem (11px) | 700, uppercase | Small tags |
| `button` | 0.875rem | 600, **no uppercase** | Buttons |

**Numerals:** stat values and number columns use the `apollo-numeric` class (`font-variant-numeric: tabular-nums`)
so digits line up across tiles and rows.

## 4. Spacing and shape

- **Spacing unit 4px**, via MudBlazor's spacing classes (`pa-4` = 16px). Use the steps 1, 2, 3, 4, 6, 8, 10
  (4, 8, 12, 16, 24, 32, 40px).
- Card padding: `pa-4` (16px). Gap between cards in a grid: `MudGrid Spacing="3"` (12px). Between page sections: `mb-10` (40px).
- **Corner radius 8px** (`ApolloTheme.BorderRadius`) for cards, buttons, inputs, chips.
- No elevation (shadows); depth comes from the surface steps and the 1px `Line` border.

## 5. Components

### `StatTile`
One key number. Parameters: `Label` (required), `Value` (`double?`), `Decimals` (default 0), `Unit`, `Delta` (percent vs previous period), `SubText`, `Icon`.

- Value formatted with `de-CH` (`1'234'567`, `842.2 h`). A missing value shows `–`.
- `Delta` shows signed percent: `+12.5 %` in `Success`, `-3 %` in `Error`, `±0 %` in secondary text.
- Grid: `MudItem xs="12" sm="6" md="3"`: four per row on desktop, two on tablets, stacked on phones.

### `SectionCard`
A titled card. Parameters: `Title` (required), `Action` (optional header control, e.g. a small text button "Alle anzeigen"), `ChildContent`.

### `EmptyState`
Shown instead of a chart or list that has no data. Parameters: `Icon` (default inbox), `Title`, `Explanation`, optional `ChildContent` (an action).
It contains no text of its own; the caller passes everything (see §8 for wording).

## 6. Charts

Use `ApexChartDefaults.Create<T>()` (or `CreateHeatmap<T>(maxValue)`) for every chart. It returns a fresh options object with:
dark mode, `Surface` background, palette colours, `Divider` grid (dashed), `Line` axes, `TextSecondary` labels and legend,
dark tooltip, no toolbar/zoom, no data labels, and `de-CH` number formatting on the value axis and tooltip.
Horizontal bar charts swap the axes, so set `Xaxis.Labels.Formatter = ApexChartDefaults.SwissNumberFormatter` there
and clear the y-axis formatter (see `StyleGuideCharts.razor`).

### Series palette

Based on Okabe-Ito (distinguishable with the common colour-vision deficiencies), lightened where needed for the dark background.
Each colour has ≥ 5:1 against `Surface`.

| # | Hex | Name | vs `Surface` |
|---|---|---|---|
| 1 | `#8A91FF` | Blurple (`PrimaryLight`) | 5.03 |
| 2 | `#E69F00` | Orange | 6.20 |
| 3 | `#56B4E9` | Sky blue | 6.05 |
| 4 | `#2EC4A0` | Bluish green | 6.33 |
| 5 | `#F0E442` | Yellow | 10.55 |
| 6 | `#FF7A45` | Vermilion | 5.40 |
| 7 | `#D88BC0` | Reddish purple | 5.56 |
| 8 | `#C3C8D1` | Light grey | 8.31 |

Rules: assign colours in this order; with more than 8 series, show the top 7 and group the rest as "Andere" (grey).
Don't rely on colour alone where it matters: keep the legend on and label lines when there are more than 3.

### Heatmap scale

Single hue, dark to light: `#2F3138` → `#353A6B` → `#3F48A3` → `#5560D6` → `#8A91FF` → `#C5C9FF`.
`CreateHeatmap` splits `0..maxValue` into equal ranges, one per step. Weekdays run Mo–So top to bottom.

### Grid / axis / label

| Element | Token |
|---|---|
| Background | `Surface` `#2A2C32` |
| Grid lines | `Divider` `#393C44`, dashed |
| Axis lines and ticks | `Line` `#3D4048` |
| Labels, legend | `TextSecondary` `#B3B8C2` |

## 7. Layout at 360px

- Use `MudContainer` + `MudGrid`; every `MudItem` has `xs="12"` unless the content is tiny (swatches use `xs="6"`).
- No fixed widths wider than 328px (360 minus 2×16px gutter). Long text wraps (`overflow-wrap: anywhere` on stat values).
- Charts take the card width (`ApexChart` default) and a fixed height (280px; heatmaps 320px).
- Tabs scroll horizontally inside their bar; the page itself never scrolls sideways.

## 8. German wording guide

UI text is German (Swiss conventions where they differ), everything else is English.

### Tone
- Address the user with **"du"**, informal and friendly, but short. No exclamation marks in normal UI.
- Say what is happening and what the user can do: "Apollo erfasst erst, wenn du eine Rolle festlegst."
- Swiss spelling: **"ss" instead of "ß"** ("Fliesstext", "schliessen", "grösser").

### Terminology

| Use | Not | Meaning |
|---|---|---|
| Mitglieder | User, Benutzer | Members of the server |
| Erfasst / Nicht erfasst | Getrackt, Tracked | Whether a member's activity is recorded |
| Tracking-Rolle | Tracking Rolle, Trackingrolle | The role that opts members in |
| Zugriffsrollen | Access-Rollen | Roles that may open the member area |
| Einstellungen | Settings, Konfiguration | Settings area |
| Kein Zugriff | Zugriff verweigert, Forbidden | Missing permission (403) |
| Rückblick (Monats-/Jahresrückblick) | Recap, Wrapped | Monthly/yearly snapshot |
| Übersicht | Dashboard | Overview page |
| Nachrichten | Messages | |
| Voice-Stunden / Voice-Minuten | Sprachzeit | Time in voice channels |
| Kanäle | Channels | |
| Reaktionen | Reactions | |
| Aktivität | | Activity over time |
| Zeitraum | Range | Selected date range |
| Server | Guild, Gilde | The Discord/Fluxer server |
| Bootstrap-Modus | | No tracking role set yet |

Platform names stay as they are: Discord, Fluxer. Slash commands stay in code font: `/apollo tracking`.

### Numbers and dates (`de-CH`)
- Thousands with apostrophe, decimal point: `1'234'567`, `98.6`. In code always format through `UiCulture.Culture`.
- Percent with a space: `12.5 %`; deltas signed: `+12.5 %`, `-3 %`, `±0 %`.
- Units after a space: `842.2 h`, `35 min`.
- Dates `15.10.2026`, short form in charts `15.10.`; time `14:30` (24h); range `01.10.2026 – 15.10.2026` (en dash with spaces).
- Weekdays abbreviated `Mo Di Mi Do Fr Sa So`; months written out in headings ("Oktober 2026").
- Missing value: `–` (en dash), never `0` or `n/a`.

### Capitalisation and buttons
- Normal German noun capitalisation; no ALL CAPS (buttons are not uppercased).
- Headings and labels are short noun phrases: "Top-Kanäle", "Aktivität nach Wochentag und Stunde".
- Buttons are a **verb in the infinitive**, one or two words: "Speichern", "Abbrechen", "Löschen", "Exportieren", "Alle anzeigen", "Rückblick erstellen".
- The primary action is a filled `Primary` button; cancel is outlined or text. Destructive actions use `Error` and ask for confirmation.

### Empty states
- Title: what is missing, as a short statement: "Noch keine Daten", "Noch keine Rückblicke".
- Explanation: one sentence on why, and when or how it changes: "Der erste Monatsrückblick erscheint am 1. November."
- Optional action button only if the user can fix it right there.

### Errors
- Say what failed and what to do next, without blame and without technical details:
  "Einstellungen konnten nicht gespeichert werden. Versuche es später noch einmal."
- Permission errors: title "Kein Zugriff", explanation names the needed role if known.
- Show errors inline (`MudAlert`, field helper text) near the cause; snackbars only for background actions.
