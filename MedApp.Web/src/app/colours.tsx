import {createContext, useCallback, useContext, useMemo, type CSSProperties, type ReactNode} from 'react';
import {contrastRatio, MIN_TEXT_CONTRAST, mixSrgb} from '../utils/contrast';
import type {ColorMode} from './colorMode';
import {defineSetting, useAppSetting} from './settingsStore';

/** The colours people can change (Account Settings, Colours), each for light and for dark mode. */
export type ColourPart = 'page' | 'card' | 'nav';

export type ModeColours = Record<ColourPart, string>;
export type Colours = Record<ColorMode, ModeColours>;

/** The app's own colours (`src/themes/medapp/medappTheme.ts`), with a little of its blue: the Standard theme. */
export const DEFAULT_COLOURS: Colours = {
  light: {page: '#d8e0ef', card: '#e4ebf8', nav: '#0e1322'},
  dark: {page: '#171b25', card: '#212634', nav: '#0e1322'},
};

export interface ColourTheme {
  slug: string;
  name: string;
  light: ModeColours;
  dark: ModeColours;
}

/**
 * Colour themes to start from: Standard (the app's own), and the backgrounds of Astryx's bundled themes that
 * have both a light and a dark mode (so both lists match; Gothic, dark only, is left out). In
 * light mode the page takes a theme's body colour, in dark mode its surface; cards take its card colour, and the
 * side nav (whose text stays light) its darkest background.
 */
export const COLOUR_THEMES: ColourTheme[] = [
  {slug: 'standard', name: 'Standard', ...DEFAULT_COLOURS},
  {
    slug: 'stone',
    name: 'Stone',
    light: {page: '#f0f0f7', card: '#ffffff', nav: '#101018'},
    dark: {page: '#1b1b21', card: '#25252a', nav: '#101018'},
  },
  {
    slug: 'neutral',
    name: 'Neutral',
    light: {page: '#f1f1f1', card: '#ffffff', nav: '#1b1b1b'},
    dark: {page: '#262626', card: '#1b1b1b', nav: '#1b1b1b'},
  },
  {
    slug: 'butter',
    name: 'Butter',
    light: {page: '#fdfbe4', card: '#ffffff', nav: '#261a13'},
    dark: {page: '#2e2117', card: '#3a2a1f', nav: '#261a13'},
  },
  {
    slug: 'chocolate',
    name: 'Chocolate',
    light: {page: '#fffcf7', card: '#ede4d4', nav: '#141010'},
    dark: {page: '#1c1610', card: '#2a2018', nav: '#141010'},
  },
  {
    slug: 'matcha',
    name: 'Matcha',
    light: {page: '#f0f0e0', card: '#ffffff', nav: '#12140e'},
    dark: {page: '#1a1c14', card: '#1e2016', nav: '#12140e'},
  },
  {
    slug: 'y2k',
    name: 'Y2K',
    light: {page: '#cccffa', card: '#ffffff', nav: '#0e0f1a'},
    dark: {page: '#16182b', card: '#16182b', nav: '#0e0f1a'},
  },
];

/**
 * The text shown on the page and cards in each mode: the theme's main and secondary text colours
 * (`--color-text-primary` and `--color-text-secondary`, `src/themes/medapp`). Secondary text (subtitles, Back,
 * descriptions, placeholders) is the lighter grey in light mode and the darker in dark mode. The side nav's text is
 * always dark mode's, as the nav keeps a dark theme of its own. Disabled text is left out: WCAG 1.4.3 exempts it.
 */
export const TEXT_COLOURS: Record<ColorMode, {primary: string; secondary: string}> = {
  light: {primary: '#25252a', secondary: '#5f5f64'},
  dark: {primary: '#f0f0f9', secondary: '#ababb3'},
};

/**
 * How card header bars and footers (`--color-background-muted`) are mixed from the card colour, in each mode: a
 * little black in light mode, a little white in dark (as the theme does).
 */
const HEADER_BAR_MIX: Record<ColorMode, {percent: number; with: 'black' | 'white'}> = {
  light: {percent: 94, with: 'black'},
  dark: {percent: 90, with: 'white'},
};

const cssMix = (mode: ColorMode, card: string) =>
  `color-mix(in srgb, ${card} ${HEADER_BAR_MIX[mode].percent}%, ${HEADER_BAR_MIX[mode].with})`;

/** A card's header bar colour in a mode, mixed from its card colour as the browser mixes it. */
export function headerBarColour(mode: ColorMode, card: string): string {
  const {percent, with: other} = HEADER_BAR_MIX[mode];
  return mixSrgb(card, percent, other === 'black' ? '#000000' : '#ffffff');
}

/**
 * How readable text is on a chosen colour (WCAG's contrast ratio): the worse of the main and secondary text shown on
 * it, and for cards on the card and its header bars. Under `MIN_TEXT_CONTRAST` (4.5:1), the Colours page warns.
 */
export function textContrast(mode: ColorMode, part: ColourPart, colour: string): number {
  // The side nav keeps dark mode's text in both modes.
  const text = TEXT_COLOURS[part === 'nav' ? 'dark' : mode];
  const surfaces = part === 'card' ? [colour, headerBarColour(mode, colour)] : [colour];
  return Math.min(...surfaces.flatMap((surface) => [contrastRatio(text.primary, surface), contrastRatio(text.secondary, surface)]));
}

/**
 * A warning for a chosen colour that leaves text on it under 4.5:1 (WCAG 1.4.3), which way to go to fix it, or
 * nothing. Its words don't change as the colour is dragged, so a screen reader says it once, not at every step.
 */
export function readabilityWarning(mode: ColorMode, part: ColourPart, colour: string): string | undefined {
  if (textContrast(mode, part, colour) >= MIN_TEXT_CONTRAST) return undefined;
  // Text is dark on light mode's page and cards, and light in dark mode and on the side nav.
  const fix = mode === 'light' && part !== 'nav' ? 'lighter' : 'darker';
  return `Text on this colour is hard to read (under ${MIN_TEXT_CONTRAST}:1 contrast). Try a ${fix} colour.`;
}

const same = (a: ModeColours, b: ModeColours) => a.page === b.page && a.card === b.card && a.nav === b.nav;

/** The theme these colours are, or null when they've been changed by hand (Custom). */
export function themeOf(mode: ColorMode, colours: ModeColours): ColourTheme | null {
  return COLOUR_THEMES.find((theme) => same(theme[mode], colours)) ?? null;
}

const STORAGE_KEY = defineSetting('medapp.colours', {keepOnSignOut: true});
const HEX = /^#[0-9a-f]{6}$/i;

/** The remembered colours (JSON), with the defaults for anything not set or not a colour. */
function parseColours(stored: string | null): Colours {
  try {
    const parsed: unknown = JSON.parse(stored ?? '{}');
    const pick = (mode: ColorMode, part: ColourPart) => {
      const value = (parsed as Record<string, Record<string, unknown>> | null)?.[mode]?.[part];
      return typeof value === 'string' && HEX.test(value) ? value : DEFAULT_COLOURS[mode][part];
    };
    const forMode = (mode: ColorMode): ModeColours => ({
      page: pick(mode, 'page'),
      card: pick(mode, 'card'),
      nav: pick(mode, 'nav'),
    });
    return {light: forMode('light'), dark: forMode('dark')};
  } catch {
    return DEFAULT_COLOURS;
  }
}

interface ColoursContextValue {
  colours: Colours;
  /** Changes one colour, at once (the app recolours as it's picked) and for next time. */
  set: (mode: ColorMode, part: ColourPart, colour: string) => void;
  /** Sets all of one mode's colours at once, e.g. from a theme. */
  setAll: (mode: ColorMode, colours: ModeColours) => void;
}

const ColoursContext = createContext<ColoursContextValue | null>(null);

/**
 * The colours people chose, kept in this browser and with the account (`settingsStore`). Its children are recoloured through the theme's own tokens, so
 * every card, page and the side nav follow (card header bars and footers too: the theme mixes them from the card
 * colour).
 *
 * `mode` is the app's mode, for the side nav's colour (see below).
 */
export function ColoursProvider({mode, children}: {mode: ColorMode; children: ReactNode}) {
  const [stored, store] = useAppSetting(STORAGE_KEY);
  const colours = useMemo(() => parseColours(stored), [stored]);

  const set = useCallback(
    (mode: ColorMode, part: ColourPart, colour: string) =>
      store(JSON.stringify({...colours, [mode]: {...colours[mode], [part]: colour}})),
    [colours, store],
  );

  const setAll = useCallback(
    (mode: ColorMode, modeColours: ModeColours) => store(JSON.stringify({...colours, [mode]: modeColours})),
    [colours, store],
  );

  const value = useMemo(() => ({colours, set, setAll}), [colours, set, setAll]);

  // The theme sets its tokens on its own element; set here, just inside it, they win for everything below.
  // Each holds both modes (light-dark), so switching mode needs no change here.
  const both = (part: ColourPart) => `light-dark(${colours.light[part]}, ${colours.dark[part]})`;
  const tokens = {
    display: 'contents',
    '--color-background-body': both('page'),
    '--color-background-surface': both('page'),
    '--color-background-card': both('card'),
    // Menus and popovers (a power search's fields, dropdowns) sit on the cards' colour too.
    '--color-background-popover': both('card'),
    // The theme mixes header bars and footers from the card colour where it defines them, so they're mixed again
    // here from the chosen card colours: slightly darker in light mode, a step lighter in dark (as the theme does).
    '--color-background-muted': `light-dark(${cssMix('light', colours.light.card)}, ${cssMix('dark', colours.dark.card)})`,
    // The side nav (and the rows its folded icons bring out) sits in a dark theme of its own, where light-dark()
    // always picks the dark colour, so it takes the current mode's colour as it is.
    '--medapp-nav-background': colours[mode].nav,
    // The card colour again, under a name no theme sets, for surfaces inside the side nav's own (dark) theme that
    // follow the cards, e.g. its rows' menus. Resolved where it's used, in that surface's own mode.
    '--medapp-card-background': both('card'),
  } as CSSProperties;

  return (
    <ColoursContext.Provider value={value}>
      {/* No Astryx component sets CSS variables, so a plain element carries them; `display: contents` keeps it
          out of the layout. */}
      <div style={tokens}>{children}</div>
    </ColoursContext.Provider>
  );
}

export function useColours(): ColoursContextValue {
  const value = useContext(ColoursContext);
  if (!value) throw new Error('useColours must be used inside ColoursProvider.');
  return value;
}
