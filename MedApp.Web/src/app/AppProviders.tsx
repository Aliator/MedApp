import {InternationalizationProvider} from '@astryxdesign/core/i18n';
import {LinkProvider} from '@astryxdesign/core/Link';
import {Theme} from '@astryxdesign/core/theme';
import {QueryClientProvider, type QueryClient} from '@tanstack/react-query';
import type {ReactNode} from 'react';
import {defaultThemeSlug, themes} from '../astryx-themes';
import {RouterLink} from '../components/navigation/RouterLink';
import {ColorModeProvider} from './colorMode';

/** Everything around the router: theme and light/dark mode, en-GB locale, router-aware links, and the query cache. */
export function AppProviders({queryClient, children}: {queryClient: QueryClient; children: ReactNode}) {
  return (
    <ColorModeProvider>
      {(mode) => (
        <Theme theme={themes[defaultThemeSlug]} mode={mode}>
          <InternationalizationProvider locale="en-GB">
            <LinkProvider component={RouterLink}>
              <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
            </LinkProvider>
          </InternationalizationProvider>
        </Theme>
      )}
    </ColorModeProvider>
  );
}
