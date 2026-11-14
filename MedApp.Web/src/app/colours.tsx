import {createContext, useCallback, useContext, useMemo, useState, type CSSProperties, type ReactNode} from 'react';
import type {ColorMode} from './colorMode';

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

const same = (a: ModeColours, b: ModeColours) => a.page === b.page && a.card === b.card && a.nav === b.nav;

/** The theme these colours are, or null when they've been changed by hand (Custom). */
export function themeOf(mode: ColorMode, colours: ModeColours): ColourTheme | null {
  return COLOUR_THEMES.find((theme) => same(theme[mode], colours)) ?? null;
}

const STORAGE_KEY = 'medapp.colours';
const HEX = /^#[0-9a-f]{6}$/i;

/** The remembered colours, with the defaults for anything not set or not a colour (storage may be unavailable). */
function readStored(): Colours {
  try {
    const stored: unknown = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '{}');
    const pick = (mode: ColorMode, part: ColourPart) => {
      const value = (stored as Record<string, Record<string, unknown>> | null)?.[mode]?.[part];
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

function store(colours: Colours) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(colours));
  } catch {
    // Storage blocked: the colours last until the page reloads.
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
 * The colours people chose, kept in this browser. Its children are recoloured through the theme's own tokens, so
 * every card, page and the side nav follow (card header bars and footers too: the theme mixes them from the card
 * colour).
 *
 * `mode` is the app's mode, for the side nav's colour (see below).
 */
export function ColoursProvider({mode, children}: {mode: ColorMode; children: ReactNode}) {
  const [colours, setColours] = useState<Colours>(readStored);

  const set = useCallback((mode: ColorMode, part: ColourPart, colour: string) => {
    setColours((current) => {
      const next = {...current, [mode]: {...current[mode], [part]: colour}};
      store(next);
      return next;
    });
  }, []);

  const setAll = useCallback((mode: ColorMode, modeColours: ModeColours) => {
    setColours((current) => {
      const next = {...current, [mode]: modeColours};
      store(next);
      return next;
    });
  }, []);

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
    '--color-background-muted': `light-dark(color-mix(in srgb, ${colours.light.card} 94%, black), color-mix(in srgb, ${colours.dark.card} 90%, white))`,
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
