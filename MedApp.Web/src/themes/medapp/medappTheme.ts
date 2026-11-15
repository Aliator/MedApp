/**
 * MedApp's theme (plan Q11): Astryx's stone theme, given the blue accent, corner radii and system fonts
 * of the Blazor app's app.css. Every other token keeps stone's value. Each colour is a [light, dark] pair;
 * dark is the app default.
 *
 * After changing this file, rebuild the checked-in output: `npm run theme:build`.
 */
import {defineTheme} from '@astryxdesign/core/theme';
import {stoneTheme} from '@astryxdesign/theme-stone';
import {medappIconRegistry} from './icons';

const accent = ['#3b82f6', '#3b82f6'] as const; // app.css --primary-500
// Links and accent text: --primary-700 in light mode (5.6:1 on the light cards; --primary-600 was 4.3:1, under
// WCAG AA's 4.5:1), lightened for dark mode.
const accentText = ['#1d4ed8', '#60a5fa'] as const;

/** Primary buttons' fill: --primary-600, which their white text is 5.2:1 on (the accent, #3b82f6, is 3.7:1). */
const ACCENT_FILL = '#2563eb';
const accentMuted = ['#eff6ff', '#172554'] as const; // --primary-50, deepened for dark mode

/**
 * The height an attached field error adds under its input; see `components.field`. A container whose fields never
 * show an error (e.g. a list's search bar) can set `--medapp-field-status-line: 0px` to drop the line.
 */
const FIELD_STATUS_LINE =
  'var(--medapp-field-status-line, calc(2 * var(--spacing-2) + var(--text-supporting-size) * var(--text-supporting-leading)))';

/** Dark mode's page background, which the side nav keeps in both modes (unless someone picks another). */
const NAV_BACKGROUND = '#0e1322';

const systemFallbacks = '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';

export const medappTheme = defineTheme({
  name: 'medapp',
  extends: stoneTheme,
  icons: medappIconRegistry,
  // A child's typography replaces the base's whole, so stone's scale and heading weights are repeated here;
  // only the families change, to app.css's system font stacks.
  typography: {
    scale: {base: 14, ratio: 1.25},
    body: {family: 'system-ui', fallbacks: systemFallbacks},
    heading: {family: 'system-ui', fallbacks: systemFallbacks, weights: {3: 'bold', 4: 'bold'}},
    code: {family: 'ui-monospace', fallbacks: 'Menlo, monospace'},
  },
  tokens: {
    '--color-accent': [...accent],
    '--color-info': [...accent],
    '--color-icon-accent': [...accent],
    '--color-text-accent': [...accentText],
    '--color-text-info': [...accentText],
    '--color-accent-muted': [...accentMuted],
    '--color-info-muted': [...accentMuted],
    '--color-on-accent': ['#ffffff', '#ffffff'],
    // The page, cards and side nav lean towards the app's blue (Standard, on the Colours page). Light mode mirrors
    // dark mode's layers, lightened, rather than white on white: a grey-blue page and cards a step lighter (not
    // white). People can change these (Account Settings, Colours).
    '--color-background-body': ['#d8e0ef', '#171b25'],
    '--color-background-surface': ['#d8e0ef', '#171b25'],
    '--color-background-card': ['#e4ebf8', '#212634'],
    // Card header bars and footers follow the card's colour: slightly darker in light mode, a step lighter in dark.
    '--color-background-muted': [
      'color-mix(in srgb, var(--color-background-card) 94%, black)',
      'color-mix(in srgb, var(--color-background-card) 90%, white)',
    ],
    '--color-background-popover': ['#fbfbfd', '#302f36'],
    '--radius-container': '12px',
    '--radius-element': '8px',
    '--radius-inner': '6px',
  },
  components: {
    // The side nav is dark in both modes (AppLayout renders it in a dark Theme; this paints the area behind it).
    'app-shell-sidenav': {
      base: {backgroundColor: `var(--medapp-nav-background, ${NAV_BACKGROUND})`},
    },
    // Every field keeps a line under its input for its error message: reserved space that stays empty until there's
    // an error, so an error showing or clearing never moves anything on the page. The field's padding holds the
    // line, and the attached error box takes that padding back, so a one-line error fills it exactly: the box's two
    // paddings and one line of supporting text (it tucks under the input by as much as the gap above it).
    field: {
      base: {paddingBottom: FIELD_STATUS_LINE},
    },
    'field-status': {
      'variant:attached': {marginBottom: `calc(-1 * ${FIELD_STATUS_LINE})`},
    },
    // A DataTable's rows share its height equally (it measures, and sets this variable); other tables size rows to
    // their content. A body cell's height is its row's (minimum) height.
    'table-cell': {
      base: {height: 'var(--medapp-table-row-height, auto)'},
    },
    // A table's column headers sit on the same muted shade as card header bars (PanelHeader), so they stand out.
    'table-header-cell': {
      base: {backgroundColor: 'var(--color-background-muted)'},
    },
    // Primary buttons are a shade darker than the accent, so their white text meets WCAG AA (4.5:1).
    button: {
      'variant:primary': {backgroundColor: ACCENT_FILL},
    },
    // Dialog titles are centred in their header, between the dialog's icon (start) and its close button (end).
    'dialog-header-title-block': {
      base: {alignItems: 'center', textAlign: 'center'},
    },
  },
});
