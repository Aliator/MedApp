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
    // Dialog titles are centred in their header, between the dialog's icon (start) and its close button (end).
    'dialog-header-title-block': {
      base: {alignItems: 'center', textAlign: 'center'},
    },
  },
});
