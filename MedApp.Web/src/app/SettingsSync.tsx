import {useQuery} from '@tanstack/react-query';
import {useEffect, useRef} from 'react';
import {saveSettings, settingsQuery} from '../api/settings';
import {appSettings, onFlushSettings} from './settingsStore';

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
    let isPending = false;
    const save = () => {
      isPending = false;
      return saveSettings(appSettings.snapshot());
    };
    const unsubscribe = appSettings.onLocalChange(() => {
      // Until the account's settings are in, a change would be overwritten by them: nothing to save yet.
      if (!isLoaded.current) return;
      isPending = true;
      window.clearTimeout(timer);
      timer = window.setTimeout(() => void save().catch(() => {}), SAVE_DELAY_MS);
    });
    // Signing out saves a pending change at once, before the browser's copy is cleared.
    const unregister = onFlushSettings(async () => {
      if (!isPending) return;
      window.clearTimeout(timer);
      await save();
    });
    return () => {
      unsubscribe();
      unregister();
      window.clearTimeout(timer);
    };
  }, []);

  return null;
}
