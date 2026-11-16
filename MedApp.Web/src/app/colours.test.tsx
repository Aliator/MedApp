import {act, render, renderHook, screen} from '@testing-library/react';
import {useEffect} from 'react';
import {medappTheme} from '../themes/medapp/medapp';
import {MIN_TEXT_CONTRAST} from '../utils/contrast';
import {COLOUR_THEMES, ColoursProvider, DEFAULT_COLOURS, headerBarColour, readabilityWarning, TEXT_COLOURS, textContrast, themeOf, useColours} from './colours';

let api: ReturnType<typeof useColours>;
/** Hands the colours API to the test (after rendering, as effects run). */
function Probe() {
  const value = useColours();
  useEffect(() => {
    api = value;
  });
  return <output>{JSON.stringify(value.colours)}</output>;
}

const wrapperStyle = () => screen.getByRole('status').parentElement!.style;

describe('colours', () => {
  beforeEach(() => localStorage.clear());

  it('starts from the Standard theme', () => {
    render(
      <ColoursProvider mode="dark">
        <Probe />
      </ColoursProvider>,
    );

    expect(api.colours).toEqual(DEFAULT_COLOURS);
    expect(themeOf('light', api.colours.light)?.slug).toBe('standard');
    expect(wrapperStyle().getPropertyValue('--color-background-card')).toBe('light-dark(#e4ebf8, #212634)');
  });

  it('applies a colour at once, remembers it, and calls hand-picked colours Custom', () => {
    render(
      <ColoursProvider mode="dark">
        <Probe />
      </ColoursProvider>,
    );

    act(() => api.set('dark', 'card', '#2a3550'));

    expect(wrapperStyle().getPropertyValue('--color-background-card')).toBe('light-dark(#e4ebf8, #2a3550)');
    expect(wrapperStyle().getPropertyValue('--color-background-muted')).toContain('#2a3550 90%, white');
    expect(themeOf('dark', api.colours.dark)).toBeNull();
    expect(JSON.parse(localStorage.getItem('medapp.colours')!).dark.card).toBe('#2a3550');
  });

  it("sets a mode's colours from a theme, and reads them back next time", () => {
    const chocolate = COLOUR_THEMES.find((t) => t.slug === 'chocolate')!;
    const {unmount} = render(
      <ColoursProvider mode="dark">
        <Probe />
      </ColoursProvider>,
    );
    act(() => api.setAll('dark', chocolate.dark!));
    unmount();

    render(
      <ColoursProvider mode="dark">
        <Probe />
      </ColoursProvider>,
    );
    expect(themeOf('dark', api.colours.dark)?.slug).toBe('chocolate');
    expect(themeOf('light', api.colours.light)?.slug).toBe('standard');
  });

  it("gives the side nav the current mode's colour (its own theme is always dark)", () => {
    const {rerender} = render(
      <ColoursProvider mode="light">
        <Probe />
      </ColoursProvider>,
    );
    act(() => api.set('light', 'nav', '#2a4d1f'));
    expect(wrapperStyle().getPropertyValue('--medapp-nav-background')).toBe('#2a4d1f');

    rerender(
      <ColoursProvider mode="dark">
        <Probe />
      </ColoursProvider>,
    );
    expect(wrapperStyle().getPropertyValue('--medapp-nav-background')).toBe(DEFAULT_COLOURS.dark.nav);
  });

  it('ignores stored values that are not colours', () => {
    localStorage.setItem('medapp.colours', JSON.stringify({light: {page: 'red; display: none'}}));
    render(
      <ColoursProvider mode="dark">
        <Probe />
      </ColoursProvider>,
    );

    expect(api.colours.light.page).toBe(DEFAULT_COLOURS.light.page);
  });
  it('starts from the Standard theme when what is stored cannot be read', () => {
    localStorage.setItem('medapp.colours', '{not json');
    render(
      <ColoursProvider mode="dark">
        <Probe />
      </ColoursProvider>,
    );

    expect(api.colours).toEqual(DEFAULT_COLOURS);
  });

  it('must be used inside its provider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(() => renderHook(() => useColours())).toThrow(/inside ColoursProvider/);
  });
});

describe('textContrast', () => {
  it("uses the theme's own text colours", () => {
    const tokens = (medappTheme as unknown as {tokens: Record<string, string>}).tokens;
    expect(tokens['--color-text-primary']).toBe(`light-dark(${TEXT_COLOURS.light}, ${TEXT_COLOURS.dark})`);
  });

  it('mixes header bars as the theme does', () => {
    expect(headerBarColour('light', '#ffffff')).toBe('#f0f0f0');
    expect(headerBarColour('dark', '#000000')).toBe('#1a1a1a');
    expect(headerBarColour('light', '#e4ebf8')).toBe('#d6dde9');
  });

  it('passes every colour theme, in both modes', () => {
    for (const theme of COLOUR_THEMES) {
      for (const mode of ['light', 'dark'] as const) {
        for (const part of ['page', 'card', 'nav'] as const) {
          expect(textContrast(mode, part, theme[mode][part]), `${theme.name} ${mode} ${part}`).toBeGreaterThanOrEqual(MIN_TEXT_CONTRAST);
        }
      }
    }
  });

  it("measures against the mode's text: dark text in light mode, light text in dark", () => {
    expect(textContrast('light', 'page', '#ffffff')).toBeGreaterThan(14);
    expect(textContrast('light', 'page', '#333333')).toBeLessThan(MIN_TEXT_CONTRAST);
    expect(textContrast('dark', 'page', '#333333')).toBeGreaterThan(MIN_TEXT_CONTRAST);
    expect(textContrast('dark', 'page', '#ffffff')).toBeLessThan(1.2);
  });

  it("takes a card's header bars into account, which are darker in light mode", () => {
    // A mid grey that passes behind the cards is under the line on a card's (darker) header bars.
    expect(textContrast('light', 'page', '#8e8e8e')).toBeGreaterThanOrEqual(MIN_TEXT_CONTRAST);
    expect(textContrast('light', 'card', '#8e8e8e')).toBeLessThan(MIN_TEXT_CONTRAST);
  });

  it('measures the side nav against its light text in both modes', () => {
    expect(textContrast('light', 'nav', '#0e1322')).toBe(textContrast('dark', 'nav', '#0e1322'));
    expect(textContrast('light', 'nav', '#f0f0f0')).toBeLessThan(MIN_TEXT_CONTRAST);
  });
});

describe('readabilityWarning', () => {
  it('says nothing for a readable colour', () => {
    expect(readabilityWarning('light', 'card', '#ffffff')).toBeUndefined();
    expect(readabilityWarning('dark', 'nav', '#0e1322')).toBeUndefined();
  });

  it('says which way to go: lighter for dark text, darker for light text', () => {
    expect(readabilityWarning('light', 'page', '#333333')).toBe('Text on this colour is hard to read (under 4.5:1 contrast). Try a lighter colour.');
    expect(readabilityWarning('dark', 'card', '#dddddd')).toMatch(/Try a darker colour\.$/);
    expect(readabilityWarning('light', 'nav', '#dddddd')).toMatch(/Try a darker colour\.$/);
  });
});
