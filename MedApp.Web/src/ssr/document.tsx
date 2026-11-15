import {HeadContent, Outlet, Scripts, useRouter} from '@tanstack/react-router';
import {createContext, useContext, type CSSProperties, type ReactNode} from 'react';
import {settingsQuery} from '../api/settings';
import {AppProviders} from '../app/AppProviders';
import {ServerSettingsContext} from '../app/settingsStore';
import {TEXT_SIZES} from '../app/textSize';
import type {RouterContext} from '../router';

/**
 * Whether the app is rendering the whole page, `<html>` and all: rendered on the server (`entry-server`), and taken
 * over by the browser (`entry-client`). Otherwise (the client-only fallback page, and tests) it renders into a
 * `<div>` on a page of its own, as before.
 */
export const DocumentContext = createContext(false);

/** Wraps the router's render when it renders the whole page (the router's `Wrap`). */
export function DocumentWrap({children}: {children: ReactNode}) {
  return <DocumentContext.Provider value>{children}</DocumentContext.Provider>;
}

/** The root route's component: the whole page when rendering one, otherwise just the matched route. */
export function RootComponent() {
  return useContext(DocumentContext) ? <RootDocument /> : <Outlet />;
}

/**
 * The page: its head (charset, viewport, title, and the app's stylesheets and scripts from the build's manifest),
 * and the app in its body, with its providers. It takes the signed-in account's saved settings (fetched by the
 * signed-in layout, `_app`), so the server renders the page as they'll see it: their light or dark mode and text
 * size are on `<html>` from the start, with no flash of the defaults.
 */
function RootDocument() {
  const {queryClient} = useRouter().options.context as RouterContext;
  const settings = queryClient.getQueryData(settingsQuery.queryKey) ?? null;
  const mode = settings?.['medapp.colorMode'] === 'light' ? 'light' : 'dark';
  const textSize = TEXT_SIZES.find((size) => size.value === settings?.['medapp.textSize']);
  const style: CSSProperties | undefined = textSize && textSize.value !== 'standard' ? {fontSize: textSize.scale} : undefined;

  return (
    <ServerSettingsContext.Provider value={settings}>
      {/* The theme and text size providers set these again as the app runs; they start right here. */}
      <html lang="en-GB" data-theme={mode} style={style} suppressHydrationWarning>
        <head>
          <meta charSet="UTF-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>MedApp</title>
          <HeadContent />
        </head>
        <body>
          <AppProviders queryClient={queryClient}>
            <Outlet />
          </AppProviders>
          <Scripts />
        </body>
      </html>
    </ServerSettingsContext.Provider>
  );
}
