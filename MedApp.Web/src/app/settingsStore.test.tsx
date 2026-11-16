import {renderToString} from 'react-dom/server';
import {appSettings, clearStorageAtSignOut, defineSetting, ServerSettingsContext, useAppSetting} from './settingsStore';

function Shows({setting}: {setting: string}) {
  const [value] = useAppSetting(setting);
  return <output>{value ?? 'none'}</output>;
}

describe('useAppSetting, rendering on the server', () => {
  it("uses the account's settings given to it, not this browser's", () => {
    localStorage.setItem('medapp.colorMode', 'dark');

    const html = renderToString(
      <ServerSettingsContext.Provider value={{'medapp.colorMode': 'light'}}>
        <Shows setting="medapp.colorMode" />
      </ServerSettingsContext.Provider>,
    );

    expect(html).toContain('light');
  });

  it('has no value for a setting the account lacks, or one that is not text, or with no settings at all', () => {
    expect(renderToString(<Shows setting="medapp.colorMode" />)).toContain('none');
    expect(
      renderToString(
        <ServerSettingsContext.Provider value={{'medapp.pageSize': 20}}>
          <Shows setting="medapp.pageSize" />
        </ServerSettingsContext.Provider>,
      ),
    ).toContain('none');
  });
});

describe('appSettings', () => {
  it('has no value when this browser blocks storage, and keeps a change for this visit', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('Storage is blocked.');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('Storage is blocked.');
    });

    expect(appSettings.get('medapp.colorMode')).toBeNull();
    appSettings.set('medapp.colorMode', 'light');
    expect(appSettings.get('medapp.colorMode')).toBe('light');
  });

  it('forgets a setting set to nothing, in this browser too', () => {
    appSettings.set('medapp.colorMode', 'light');
    appSettings.set('medapp.colorMode', null);

    expect(localStorage.getItem('medapp.colorMode')).toBeNull();
  });

  it("tells nobody when the account's settings are the ones it already has", () => {
    const key = defineSetting('medapp.testSetting');
    appSettings.set(key, 'on');
    const listener = vi.fn();
    const unsubscribe = appSettings.subscribe(listener);

    appSettings.load({...appSettings.snapshot()});
    expect(listener).not.toHaveBeenCalled();

    appSettings.load({...appSettings.snapshot(), [key]: 'off'});
    expect(listener).toHaveBeenCalledOnce();
    expect(appSettings.get(key)).toBe('off');
    unsubscribe();
  });
});

describe('clearStorageAtSignOut', () => {
  const visual = defineSetting('medapp.testVisual', {keepOnSignOut: true});
  const other = defineSetting('medapp.testOther');

  afterEach(() => sessionStorage.clear());

  it("removes the app's keys from both stores, keeping only the visual settings and other apps' keys", () => {
    localStorage.setItem(visual, 'dark');
    localStorage.setItem(other, 'saved search');
    localStorage.setItem('medapp.pinnedPatients.test-user', '["p1"]');
    localStorage.setItem('another.app', 'theirs');
    sessionStorage.setItem('medapp.halfView', '/patients');
    sessionStorage.setItem('another.app', 'theirs');

    clearStorageAtSignOut();

    expect(localStorage.getItem(visual)).toBe('dark');
    expect(localStorage.getItem(other)).toBeNull();
    expect(localStorage.getItem('medapp.pinnedPatients.test-user')).toBeNull();
    expect(localStorage.getItem('another.app')).toBe('theirs');
    expect(sessionStorage.getItem('medapp.halfView')).toBeNull();
    expect(sessionStorage.getItem('another.app')).toBe('theirs');
  });

  it('puts settings in use back to their defaults at once, without saving that to the account', () => {
    appSettings.set(visual, 'dark');
    appSettings.set(other, 'saved search');
    const listener = vi.fn();
    const saver = vi.fn();
    const unsubscribe = appSettings.subscribe(listener);
    const stopSaving = appSettings.onLocalChange(saver);

    clearStorageAtSignOut();

    expect(appSettings.get(other)).toBeNull();
    expect(appSettings.get(visual)).toBe('dark');
    expect(listener).toHaveBeenCalledOnce();
    expect(saver).not.toHaveBeenCalled();

    // Nothing left to clear: nobody is told again.
    clearStorageAtSignOut();
    expect(listener).toHaveBeenCalledOnce();
    unsubscribe();
    stopSaving();
  });

  it('does nothing harmful when storage is blocked', () => {
    localStorage.setItem(other, 'saved search');
    vi.spyOn(Storage.prototype, 'key').mockImplementation(() => {
      throw new Error('Storage is blocked.');
    });

    expect(() => clearStorageAtSignOut()).not.toThrow();
  });

  it('carries on when a key cannot be removed', () => {
    localStorage.setItem(other, 'saved search');
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('Storage is blocked.');
    });

    expect(() => clearStorageAtSignOut()).not.toThrow();
    expect(appSettings.get(other)).toBeNull();
  });
});

describe('appSettings, with other tabs', () => {
  const key = defineSetting('medapp.testShared');

  it('shows a setting changed in another tab, without saving it again', () => {
    const listener = vi.fn();
    const saver = vi.fn();
    const unsubscribe = appSettings.subscribe(listener);
    const stopSaving = appSettings.onLocalChange(saver);

    window.dispatchEvent(new StorageEvent('storage', {key, newValue: 'from the other tab', storageArea: localStorage}));

    expect(appSettings.get(key)).toBe('from the other tab');
    expect(listener).toHaveBeenCalledOnce();
    expect(saver).not.toHaveBeenCalled();
    unsubscribe();
    stopSaving();
  });

  it("ignores keys that aren't settings, and the other store", () => {
    appSettings.set(key, 'here');
    const listener = vi.fn();
    const unsubscribe = appSettings.subscribe(listener);

    window.dispatchEvent(new StorageEvent('storage', {key: 'medapp.notASetting', newValue: 'x', storageArea: localStorage}));
    window.dispatchEvent(new StorageEvent('storage', {key, newValue: 'x', storageArea: sessionStorage}));

    expect(listener).not.toHaveBeenCalled();
    expect(appSettings.get(key)).toBe('here');
    unsubscribe();
  });

  it('reads everything again when another tab clears storage', () => {
    appSettings.set(key, 'here');
    const listener = vi.fn();
    const unsubscribe = appSettings.subscribe(listener);
    localStorage.clear();

    window.dispatchEvent(new StorageEvent('storage', {key: null, storageArea: localStorage}));

    expect(listener).toHaveBeenCalledOnce();
    expect(appSettings.get(key)).toBeNull();
    unsubscribe();
  });
});
