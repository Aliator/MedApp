import '@astryxdesign/core/reset.css';
import '@astryxdesign/core/astryx.css';
// Scrollbars in the theme's colours (after Astryx's, outside its layers).
import './app/scrollbars.css';
// The formatted-text editor (RichTextField).
import '@astryxdesign/richtext/richtext.css';
// The easy-to-read font (Account Settings, Accessibility): declared here, downloaded only once it's used.
import '@fontsource/atkinson-hyperlegible/latin-400.css';
import '@fontsource/atkinson-hyperlegible/latin-700.css';
import {RouterClient} from '@tanstack/react-router/ssr/client';
import {StrictMode} from 'react';
import {createRoot, hydrateRoot} from 'react-dom/client';
import {App} from './app/App';
import {createSsrRouter} from './router';

/**
 * The browser's start. A page the server rendered (`entry-server`) is taken over where it is (hydrated), with the
 * data it came with. The plain `index.html` (if the server rendering isn't running) has an empty `#root` to render
 * the app into, as before server rendering.
 */
const root = document.getElementById('root');
if (root) {
  createRoot(root).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
} else {
  hydrateRoot(
    document,
    <StrictMode>
      <RouterClient router={createSsrRouter()} />
    </StrictMode>,
  );
}
