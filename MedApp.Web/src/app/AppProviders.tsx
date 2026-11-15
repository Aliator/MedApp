import {InternationalizationProvider} from '@astryxdesign/core/i18n';
import {LinkProvider} from '@astryxdesign/core/Link';
import {Theme} from '@astryxdesign/core/theme';
import {QueryClientProvider, type QueryClient} from '@tanstack/react-query';
import type {ReactNode} from 'react';
import {defaultThemeSlug, themes} from '../astryx-themes';
import {RouterLink} from '../components/navigation/RouterLink';
import {AccessibilityProvider} from './accessibility';
import {AnnouncerProvider} from './announcements';
import {ColorModeProvider} from './colorMode';
import {ColoursProvider} from './colours';
import {ListSettingsProvider} from './listSettings';
import {NavSettingsProvider} from './navSettings';
import {TextSizeProvider} from './textSize';

/** Everything around the router: theme, light/dark mode, the chosen colours, the accessibility settings, text size and side nav settings, en-GB locale, router-aware links, and the query cache. */
export function AppProviders({queryClient, children}: {queryClient: QueryClient; children: ReactNode}) {
  return (
    <ColorModeProvider>
      {(mode) => (
        <Theme theme={themes[defaultThemeSlug]} mode={mode}>
          <InternationalizationProvider locale="en-GB">
            <LinkProvider component={RouterLink}>
              <QueryClientProvider client={queryClient}>
                <ColoursProvider mode={mode}>
                  <AccessibilityProvider>
                    <AnnouncerProvider>
                      <TextSizeProvider>
                        <NavSettingsProvider>
                          <ListSettingsProvider>{children}</ListSettingsProvider>
                        </NavSettingsProvider>
                      </TextSizeProvider>
                    </AnnouncerProvider>
                  </AccessibilityProvider>
                </ColoursProvider>
              </QueryClientProvider>
            </LinkProvider>
          </InternationalizationProvider>
        </Theme>
      )}
    </ColorModeProvider>
  );
}
