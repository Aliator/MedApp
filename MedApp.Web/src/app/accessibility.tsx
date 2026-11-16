import {createContext, useCallback, useContext, useEffect, useMemo, type ReactNode} from 'react';
import {useColorMode} from './colorMode';
import {defineSetting, useAppSetting} from './settingsStore';

/**
 * The accessibility settings (Account Settings, Accessibility), each a switch, kept in this browser and with the
 * account (`settingsStore`). All start off.
 */
export const ACCESSIBILITY_SETTINGS = [
  {
    key: 'highContrast',
    label: 'High contrast',
    description: 'Stronger text and borders.',
  },
  {
    key: 'reduceMotion',
    label: 'Reduce motion',
    description: 'No sliding, fading or growing.',
  },
  {
    key: 'strongFocus',
    label: 'Stronger focus indicator',
    description: 'A thicker, high-contrast outline round whatever has keyboard focus.',
  },
  {
    key: 'textSpacing',
    label: 'More text spacing',
    description: 'More space between lines, letters and words.',
  },
  {
    key: 'readableFont',
    label: 'Easy-to-read font',
    description: 'Atkinson Hyperlegible, a font whose letters are hard to mix up.',
  },
  {
    key: 'statusIcons',
    label: 'Icons on statuses',
    description: 'Statuses, priorities and Overdue show an icon as well as their colour and name.',
  },
  {
    key: 'colourBlindPalette',
    label: 'Colour-blind-friendly colours',
    description: 'Red and green become vermilion and blue, which most colour-blind people can tell apart.',
  },
  {
    key: 'underlineLinks',
    label: 'Underline links',
    description: 'Links are always underlined, not only under the pointer.',
  },
  {
    key: 'biggerTargets',
    label: 'Bigger buttons and fields',
    description: 'Buttons and fields at least 44 pixels high, easier to hit.',
  },
  {
    key: 'shortcuts',
    label: 'Keyboard shortcuts',
    description: 'Single keys that move around the app. Press ? to see them.',
  },
  {
    key: 'announcements',
    label: 'Screen reader announcements',
    description: 'Your screen reader says which page opened, how many rows a list found, and when a form is saved or a stage finished.',
  },
] as const satisfies readonly {key: string; label: string; description: string}[];

export type AccessibilitySetting = (typeof ACCESSIBILITY_SETTINGS)[number]['key'];
export type AccessibilityFlags = Record<AccessibilitySetting, boolean>;

/** The settings changed from their defaults, as JSON: `{"highContrast": true, …}`. */
const STORAGE_KEY = defineSetting('medapp.accessibility', {keepOnSignOut: true});

function parseChoices(stored: string | null): Partial<AccessibilityFlags> {
  try {
    const parsed: unknown = JSON.parse(stored ?? '{}');
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return {};
    return Object.fromEntries(
      Object.entries(parsed).filter(
        ([key, value]) => ACCESSIBILITY_SETTINGS.some((s) => s.key === key) && typeof value === 'boolean',
      ),
    );
  } catch {
    return {};
  }
}

/** The duration tokens, all zeroed when motion is reduced. */
const DURATIONS = ['fast', 'medium', 'slow'].flatMap((d) => [`--duration-${d}`, `--duration-${d}-min`, `--duration-${d}-max`]);
const LEADINGS = [
  'body',
  'code',
  'label',
  'large',
  'supporting',
  'display-1',
  'display-2',
  'display-3',
  ...[1, 2, 3, 4, 5, 6].map((n) => `heading-${n}`),
].map((t) => `--text-${t}-leading`);

const READABLE_FONT = '"Atkinson Hyperlegible", system-ui, -apple-system, "Segoe UI", sans-serif';

/** Vermilion for red, blue for green (Okabe and Ito's colour-blind-safe palette), in each mode. */
const COLOUR_BLIND_TOKENS = {
  '--color-text-red': 'light-dark(#8a3200, #ffb38a)',
  '--color-icon-red': 'light-dark(#d55e00, #ff8a4c)',
  '--color-border-red': 'light-dark(#d55e00, #ff8a4c)',
  '--color-background-red': 'light-dark(#d55e0033, #d55e0040)',
  '--color-text-error': 'light-dark(#8a3200, #ffb38a)',
  '--color-error': 'light-dark(#d55e00, #ff8a4c)',
  '--color-border-error': 'light-dark(#d55e00, #ff8a4c)',
  '--color-text-green': 'light-dark(#00466e, #9fd4ff)',
  '--color-icon-green': 'light-dark(#0072b2, #56b4e9)',
  '--color-border-green': 'light-dark(#0072b2, #56b4e9)',
  '--color-background-green': 'light-dark(#0072b233, #0072b240)',
  '--color-text-success': 'light-dark(#00466e, #9fd4ff)',
  '--color-success': 'light-dark(#0072b2, #56b4e9)',
  '--color-border-success': 'light-dark(#0072b2, #56b4e9)',
};

const declarations = (tokens: Record<string, string>) =>
  Object.entries(tokens)
    .map(([name, value]) => `${name}: ${value};`)
    .join(' ');

/**
 * The CSS the settings that are on add. Astryx's styles and the theme sit in cascade layers, so these rules (not
 * in a layer) win over them. Tokens go on every theme root (`[data-astryx-theme]`: the app's and the side nav's
 * own dark one), which is where the theme sets them.
 */
export function accessibilityCss(flags: AccessibilityFlags): string {
  const tokens: Record<string, string> = {};
  const rules: string[] = [];

  if (flags.highContrast) {
    Object.assign(tokens, {
      '--color-text-secondary': 'var(--color-text-primary)',
      '--color-text-disabled': 'light-dark(#4b5563, #a3a9b5)',
      '--color-border': 'light-dark(#4b5563, #a3a9b5)',
      '--color-border-emphasized': 'light-dark(#111827, #f3f4f6)',
    });
  }
  if (flags.reduceMotion) {
    for (const name of [...DURATIONS, '--duration-exit']) tokens[name] = '0ms';
    rules.push(
      '*, *::before, *::after { transition-duration: 0s !important; transition-delay: 0s !important; scroll-behavior: auto !important; }',
    );
  }
  if (flags.strongFocus) {
    Object.assign(tokens, {
      '--focus-outline-width': '4px',
      '--focus-outline-offset': '2px',
      '--focus-outline-style': 'solid',
      // Near-black in light mode, yellow in dark: each stands out from every surface in its mode.
      '--focus-outline-color': 'light-dark(#0b0c0c, #ffdd00)',
    });
  }
  if (flags.textSpacing) {
    // WCAG 1.4.12's spacing: line height 1.5, letters 0.12em and words 0.16em apart, paragraphs 2em apart.
    for (const name of LEADINGS) tokens[name] = '1.5';
    rules.push('[data-astryx-theme] * { letter-spacing: 0.12em; word-spacing: 0.16em; }');
    rules.push('[data-astryx-theme] p + p { margin-block-start: 2em; }');
  }
  if (flags.readableFont) {
    Object.assign(tokens, {'--font-family-body': READABLE_FONT, '--font-family-heading': READABLE_FONT});
  }
  if (flags.colourBlindPalette) Object.assign(tokens, COLOUR_BLIND_TOKENS);
  if (flags.underlineLinks) {
    rules.push('.astryx-link { text-decoration-line: underline; text-underline-offset: 0.2em; }');
  }
  if (flags.biggerTargets) {
    Object.assign(tokens, {'--size-element-sm': '44px', '--size-element-md': '44px', '--size-element-lg': '48px'});
  }

  const tokenRule = Object.keys(tokens).length > 0 ? `[data-astryx-theme] { ${declarations(tokens)} }` : '';
  return [tokenRule, ...rules].filter(Boolean).join('\n');
}

/**
 * Astryx's own animations (springs) follow a mark on `<html>`, which a theme sets when it reduces motion for
 * everyone. The theme sets it from the device's setting, so it's set here too while Reduce motion is on.
 */
const REDUCED_MOTION_MARK = 'data-astryx-reduced-motion';
const REDUCED_MOTION_PROPERTY = '--_astryx-reduced-motion';

interface AccessibilityContextValue {
  flags: AccessibilityFlags;
  set: (setting: AccessibilitySetting, isOn: boolean) => void;
}

const AccessibilityContext = createContext<AccessibilityContextValue | null>(null);

/** Applies the accessibility settings to the whole app (see `accessibilityCss`), and offers them to read and change. */
export function AccessibilityProvider({children}: {children: ReactNode}) {
  const {mode} = useColorMode();
  const [stored, store] = useAppSetting(STORAGE_KEY);
  const choices = useMemo(() => parseChoices(stored), [stored]);
  const flags = useMemo(
    () => Object.fromEntries(ACCESSIBILITY_SETTINGS.map((s) => [s.key, choices[s.key] ?? false])) as AccessibilityFlags,
    [choices],
  );

  const set = useCallback(
    (setting: AccessibilitySetting, isOn: boolean) => store(JSON.stringify({...choices, [setting]: isOn})),
    [choices, store],
  );

  // After the theme's own effect, which clears the mark when the mode changes (hence `mode`).
  useEffect(() => {
    if (!flags.reduceMotion) return;
    const root = document.documentElement;
    root.setAttribute(REDUCED_MOTION_MARK, '');
    root.style.setProperty(REDUCED_MOTION_PROPERTY, '1');
    return () => {
      root.removeAttribute(REDUCED_MOTION_MARK);
      root.style.removeProperty(REDUCED_MOTION_PROPERTY);
    };
  }, [flags.reduceMotion, mode]);

  const css = accessibilityCss(flags);
  const value = useMemo(() => ({flags, set}), [flags, set]);
  return (
    <AccessibilityContext.Provider value={value}>
      {css && <style data-medapp-accessibility="">{css}</style>}
      {children}
    </AccessibilityContext.Provider>
  );
}

const DEFAULTS: AccessibilityContextValue = {
  flags: Object.fromEntries(ACCESSIBILITY_SETTINGS.map((s) => [s.key, false])) as AccessibilityFlags,
  set: () => {},
};

/** The accessibility settings; outside the provider (tests of one part), all off. */
export function useAccessibility(): AccessibilityContextValue {
  return useContext(AccessibilityContext) ?? DEFAULTS;
}
