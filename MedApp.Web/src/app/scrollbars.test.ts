import entry from '../entry-client.tsx?raw';

// The tests don't process CSS (vite.config.ts, css: false), so the rule itself is checked in the browser; here, that
// it's loaded on every page (sign-in too), after Astryx's styles.
describe('scrollbars', () => {
  it('load with the app, after Astryx', () => {
    expect(entry).toMatch(/import '\.\/app\/scrollbars\.css';/);
    expect(entry.indexOf('./app/scrollbars.css')).toBeGreaterThan(entry.indexOf('@astryxdesign/core/astryx.css'));
  });
});
