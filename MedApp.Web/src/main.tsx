import '@astryxdesign/core/reset.css';
import '@astryxdesign/core/astryx.css';
// The easy-to-read font (Account Settings, Accessibility): declared here, downloaded only once it's used.
import '@fontsource/atkinson-hyperlegible/latin-400.css';
import '@fontsource/atkinson-hyperlegible/latin-700.css';
import {StrictMode} from 'react';
import {createRoot} from 'react-dom/client';
import {App} from './app/App';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
