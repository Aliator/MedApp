import {QueryClient} from '@tanstack/react-query';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {createRoot} from 'react-dom/client';
import {AppProviders} from '../app/AppProviders';
import {StandalonePage} from '../components/pages/StandalonePage';
import {FullDemoProgress} from './FullDemoProgress';
import {whenReady} from './startup';

/** What shows while the backend starts again after a reload, signed in: the progress, then the app. */
export function FullDemoStarting() {
  return (
    <StandalonePage title="MedApp: full demo" maxWidth={480}>
      <VStack gap={4} hAlign="stretch">
        <Text type="body">MedApp runs in your browser: it’s starting again, and signing you back in.</Text>
        <FullDemoProgress />
      </VStack>
    </StandalonePage>
  );
}

/**
 * Shows the start screen in `#root` until the backend has started and signed in again, then takes it away, for the
 * app to render there (`installFullDemo`, after a reload while signed in). If the start fails it stays, saying so.
 */
export async function showStartScreen(element: HTMLElement | null = document.getElementById('root')): Promise<void> {
  if (!element) return whenReady().catch(() => new Promise<void>(() => {}));
  const root = createRoot(element);
  root.render(
    <AppProviders queryClient={new QueryClient()}>
      <FullDemoStarting />
    </AppProviders>,
  );
  try {
    await whenReady();
  } catch {
    // It says what went wrong, with Try again; the app never renders.
    return new Promise<void>(() => {});
  }
  root.unmount();
}
