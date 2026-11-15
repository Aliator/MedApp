import {useQuery} from '@tanstack/react-query';
import {useEffect, useRef} from 'react';
import {saveSettings, settingsQuery} from '../api/settings';
import {appSettings} from './settingsStore';

/** How long after the last change the settings are saved, so a run of changes (a colour being picked) saves once. */
export const SAVE_DELAY_MS = 800;

/**
 * Keeps the app's settings with the signed-in user's account, so they're the same on every computer. Rendered once
 * in the signed-in app (it shows nothing).
 *
 * - **Signing in (or opening the app):** the account's settings replace this browser's. An account with none saved
 *   yet takes this browser's, so nobody loses the settings they had before.
 * - **Afterwards:** each change is saved with the account, a moment after the last one.
 *
 * If the account can't be reached, the settings stay in this browser as before, and nothing is lost there.
 */
export function SettingsSync() {
  const {data, isSuccess} = useQuery(settingsQuery);
  const isLoaded = useRef(false);

  useEffect(() => {
    if (!isSuccess || isLoaded.current) return;
    isLoaded.current = true;
    if (data) appSettings.load(data);
    else void saveSettings(appSettings.snapshot()).catch(() => {});
  }, [data, isSuccess]);

  useEffect(() => {
    let timer: number | undefined;
    const unsubscribe = appSettings.onLocalChange(() => {
      // Until the account's settings are in, a change would be overwritten by them: nothing to save yet.
      if (!isLoaded.current) return;
      window.clearTimeout(timer);
      timer = window.setTimeout(() => void saveSettings(appSettings.snapshot()).catch(() => {}), SAVE_DELAY_MS);
    });
    return () => {
      unsubscribe();
      window.clearTimeout(timer);
    };
  }, []);

  return null;
}
