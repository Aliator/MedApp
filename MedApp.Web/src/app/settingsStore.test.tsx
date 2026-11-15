import {renderToString} from 'react-dom/server';
import {appSettings, defineSetting, ServerSettingsContext, useAppSetting} from './settingsStore';

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
