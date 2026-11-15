import {renderToString} from 'react-dom/server';
import {ServerSettingsContext, useAppSetting} from './settingsStore';

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
