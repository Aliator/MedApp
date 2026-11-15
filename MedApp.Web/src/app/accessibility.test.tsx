import {renderHook, screen} from '@testing-library/react';
import {renderWithApp} from '../test/render';
import {accessibilityCss, ACCESSIBILITY_SETTINGS, useAccessibility, type AccessibilityFlags} from './accessibility';

const KEY = 'medapp.accessibility';
const allOff = Object.fromEntries(ACCESSIBILITY_SETTINGS.map((s) => [s.key, false])) as AccessibilityFlags;
const styleSheet = () => document.querySelector('style[data-medapp-accessibility]')?.textContent ?? '';

function Flags() {
  const {flags} = useAccessibility();
  return <output>{Object.entries(flags).filter(([, isOn]) => isOn).map(([key]) => key).join(' ')}</output>;
}

describe('accessibilityCss', () => {
  it('adds nothing with every setting off', () => {
    expect(accessibilityCss(allOff)).toBe('');
  });

  it('turns each setting into the tokens and rules it needs', () => {
    expect(accessibilityCss({...allOff, strongFocus: true})).toContain('--focus-outline-width: 4px;');
    expect(accessibilityCss({...allOff, reduceMotion: true})).toContain('--duration-fast: 0ms;');
    expect(accessibilityCss({...allOff, reduceMotion: true})).toContain('transition-duration: 0s !important');
    expect(accessibilityCss({...allOff, textSpacing: true})).toMatch(/--text-body-leading: 1\.5;[\s\S]*letter-spacing: 0\.12em; word-spacing: 0\.16em;/);
    expect(accessibilityCss({...allOff, readableFont: true})).toContain('--font-family-body: "Atkinson Hyperlegible"');
    expect(accessibilityCss({...allOff, highContrast: true})).toContain('--color-text-secondary: var(--color-text-primary);');
    expect(accessibilityCss({...allOff, colourBlindPalette: true})).toContain('--color-icon-green: light-dark(#0072b2, #56b4e9);');
    expect(accessibilityCss({...allOff, underlineLinks: true})).toContain('.astryx-link { text-decoration-line: underline;');
    expect(accessibilityCss({...allOff, biggerTargets: true})).toContain('--size-element-md: 44px;');
  });
});

describe('AccessibilityProvider', () => {
  it('starts with every setting off, adding nothing', async () => {
    renderWithApp(<Flags />);

    expect(await screen.findByRole('status', {hidden: true})).toBeInTheDocument();
    expect(document.querySelector('output')).toHaveTextContent(/^$/);
    expect(styleSheet()).toBe('');
    expect(document.documentElement).not.toHaveAttribute('data-astryx-reduced-motion');
  });

  it('applies the settings turned on', async () => {
    localStorage.setItem(KEY, JSON.stringify({reduceMotion: true, underlineLinks: true, shortcuts: false}));
    renderWithApp(<Flags />);

    expect(await screen.findByText('reduceMotion underlineLinks')).toBeInTheDocument();
    expect(styleSheet()).toContain('.astryx-link');
    expect(styleSheet()).not.toContain('--color-text-secondary');
    expect(document.documentElement).toHaveAttribute('data-astryx-reduced-motion');
  });

  it('ignores settings it no longer has, and anything that isn’t a switch', async () => {
    localStorage.setItem(KEY, JSON.stringify({retired: true, strongFocus: 'yes'}));
    renderWithApp(<Flags />);

    await screen.findByRole('status', {hidden: true});
    expect(document.querySelector('output')).toHaveTextContent(/^$/);
  });
  it('starts with every setting off when what is stored cannot be read', async () => {
    localStorage.setItem(KEY, '{not json');
    renderWithApp(<Flags />);

    await screen.findByRole('status', {hidden: true});
    expect(document.querySelector('output')).toHaveTextContent(/^$/);
  });

  it('starts with every setting off when what is stored is not a set of switches', async () => {
    localStorage.setItem(KEY, JSON.stringify([true]));
    renderWithApp(<Flags />);

    await screen.findByRole('status', {hidden: true});
    expect(document.querySelector('output')).toHaveTextContent(/^$/);
  });

  it('has every setting off outside its provider (a part tested on its own), and changes nothing there', () => {
    const {result} = renderHook(() => useAccessibility());

    result.current.set('highContrast', true);

    expect(Object.values(result.current.flags).every((isOn) => !isOn)).toBe(true);
    expect(localStorage.getItem(KEY)).toBeNull();
  });
});
