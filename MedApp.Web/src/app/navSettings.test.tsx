import {act, render, renderHook, screen} from '@testing-library/react';
import {useEffect} from 'react';
import {appSettings} from './settingsStore';
import {currentStartPagePath, NavSettingsProvider, useNavSettings} from './navSettings';

let api: ReturnType<typeof useNavSettings>;
function Probe() {
  const value = useNavSettings();
  // Handed to the test after rendering, as effects run.
  useEffect(() => {
    api = value;
  });
  return <output>{`${value.flyoutDelay} ${value.flyoutDelayMs} ${value.startPage} ${value.startPagePath}`}</output>;
}

const renderSettings = () =>
  render(
    <NavSettingsProvider>
      <Probe />
    </NavSettingsProvider>,
  );

describe('navSettings', () => {
  it('starts with a half-second flyout, opening on Home', () => {
    renderSettings();

    expect(screen.getByRole('status')).toHaveTextContent('standard 500 home /');
  });

  it('changes and remembers each setting', () => {
    renderSettings();

    act(() => api.setFlyoutDelay('long'));
    act(() => api.setStartPage('tasks'));

    expect(screen.getByRole('status')).toHaveTextContent('long 1000 tasks /tasks');
    expect(localStorage.getItem('medapp.navFlyoutDelay')).toBe('long');
    expect(localStorage.getItem('medapp.startPage')).toBe('tasks');
  });

  it('takes stored values it does not know as the defaults', () => {
    localStorage.setItem('medapp.navFlyoutDelay', 'forever');
    localStorage.setItem('medapp.startPage', 'nowhere');
    renderSettings();

    expect(screen.getByRole('status')).toHaveTextContent('standard 500 home /');
  });

  it('has the defaults outside its provider (a part tested on its own), and changes nothing there', () => {
    const {result} = renderHook(() => useNavSettings());

    result.current.setFlyoutDelay('none');
    result.current.setStartPage('patients');

    expect(result.current).toMatchObject({flyoutDelayMs: 500, startPagePath: '/'});
    expect(localStorage.length).toBe(0);
  });
});

describe('currentStartPagePath', () => {
  it('reads the start page as the settings are now, Home unless one is chosen', () => {
    expect(currentStartPagePath()).toBe('/');
    appSettings.set('medapp.startPage', 'tasks');
    expect(currentStartPagePath()).toBe('/tasks');
    appSettings.set('medapp.startPage', 'nowhere');
    expect(currentStartPagePath()).toBe('/');
  });
});
