/**
 * True only in the full demo's build (`npm run build:full-demo`, Vite's `full-demo` mode: vite.config.ts's `define`):
 * the real backend in the browser (docs/full-browser-demo-plan.md). Every other build, and the tests, have it false, so
 * the bundler drops whatever it guards: keep the full demo's code behind it, imported dynamically.
 */
declare const __FULL_DEMO__: boolean;
