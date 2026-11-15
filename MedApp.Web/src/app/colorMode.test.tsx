import {act, render, renderHook, screen} from '@testing-library/react';
import {useEffect} from 'react';
import {ColorModeProvider, useColorMode} from './colorMode';

let api: ReturnType<typeof useColorMode>;
function Probe() {
  const value = useColorMode();
  // Handed to the test after rendering, as effects run.
  useEffect(() => {
    api = value;
  });
  return <output>{value.mode}</output>;
}

function renderMode() {
  return render(
    <ColorModeProvider>
      {(mode) => (
        <main data-mode={mode}>
          <Probe />
        </main>
      )}
    </ColorModeProvider>,
  );
}

describe('colorMode', () => {
  it('starts dark, whatever the device prefers', () => {
    renderMode();

    expect(screen.getByRole('status')).toHaveTextContent('dark');
    expect(screen.getByRole('main')).toHaveAttribute('data-mode', 'dark');
  });

  it('switches mode, and remembers the choice', () => {
    renderMode();

    act(() => api.toggle());
    expect(screen.getByRole('status')).toHaveTextContent('light');
    expect(localStorage.getItem('medapp.colorMode')).toBe('light');

    act(() => api.toggle());
    expect(screen.getByRole('status')).toHaveTextContent('dark');

    act(() => api.setMode('light'));
    expect(screen.getByRole('main')).toHaveAttribute('data-mode', 'light');
  });

  it('reads the remembered mode', () => {
    localStorage.setItem('medapp.colorMode', 'light');
    renderMode();

    expect(screen.getByRole('status')).toHaveTextContent('light');
  });

  it('takes a remembered mode it does not know as dark', () => {
    localStorage.setItem('medapp.colorMode', 'sepia');
    renderMode();

    expect(screen.getByRole('status')).toHaveTextContent('dark');
  });

  it('must be used inside its provider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(() => renderHook(() => useColorMode())).toThrow(/inside ColorModeProvider/);
  });
});
