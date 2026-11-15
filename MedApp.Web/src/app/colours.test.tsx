import {act, render, renderHook, screen} from '@testing-library/react';
import {useEffect} from 'react';
import {COLOUR_THEMES, ColoursProvider, DEFAULT_COLOURS, themeOf, useColours} from './colours';

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
