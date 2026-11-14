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
const accentText = ['#2563eb', '#60a5fa'] as const; // --primary-600, lightened for dark mode
const accentMuted = ['#eff6ff', '#172554'] as const; // --primary-50, deepened for dark mode

/** The height an attached field error adds under its input; see `components.field`. */
const FIELD_STATUS_LINE =
  'calc(2 * var(--spacing-2) + var(--text-supporting-size) * var(--text-supporting-leading))';

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
    '--radius-container': '12px',
    '--radius-element': '8px',
    '--radius-inner': '6px',
  },
  components: {
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
    // Dialog titles are centred in their header, between the dialog's icon (start) and its close button (end).
    'dialog-header-title-block': {
      base: {alignItems: 'center', textAlign: 'center'},
    },
  },
});
