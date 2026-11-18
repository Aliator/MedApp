/**
 * True only in the light demo's build (`npm run build:demo`, Vite's `demo` mode: vite.config.ts's `define`). Every
 * other build, and the tests, have it false, so the bundler drops whatever it guards: keep the demo's code behind it,
 * imported dynamically (docs/agents/light-demo.md).
 */
declare const __LIGHT_DEMO__: boolean;
