import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {ProgressBar} from '@astryxdesign/core/ProgressBar';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {useSyncExternalStore} from 'react';
import {AppIcon} from '../components/shared/icons';
import {progressWords} from './progress';
import {startupState, subscribeStartup} from './startup';

/** The full demo's start, as it happens (`startup.ts`). */
export const useStartup = () => useSyncExternalStore(subscribeStartup, startupState, startupState);

/** Reloads the page: Try again. Its own object, so tests can stand in for the browser's. */
export const startAgain = {
  reload: () => window.location.reload(),
};

/**
 * How far the full demo's start has got: a progress bar with its words ("Getting MedApp ready: 12 of 20 MB", then
 * "Starting…"), nothing once it's ready, or what went wrong with Try again (a reload, which picks up what was kept).
 */
export function FullDemoProgress() {
  const state = useStartup();
  if (state.phase === 'ready') return null;
  if (state.phase === 'failed')
    return (
      <VStack gap={2} hAlign="stretch">
        <Banner
          status="error"
          container="card"
          role="alert"
          title="MedApp couldn’t be downloaded"
          description="Check the connection, then try again. A browser without WebAssembly or IndexedDB can’t run it."
        />
        <HStack>
          <Button label="Try Again" variant="primary" icon={<Icon icon={AppIcon.Refresh} />} onClick={() => startAgain.reload()} />
        </HStack>
      </VStack>
    );
  const {label, value} = progressWords(state);
  return value === null ? <ProgressBar label={label} isIndeterminate /> : <ProgressBar label={label} value={value} />;
}
