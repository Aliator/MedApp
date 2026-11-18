/// <reference types="vitest/config" />
import {tanstackRouter} from '@tanstack/router-plugin/vite';
import react from '@vitejs/plugin-react';
import {defineConfig} from 'vite';

// Under the Aspire AppHost, `WithReference(api)` passes the API's URL in; the fallback is the API's https
// launch-profile port, for running `npm run dev` on its own.
const apiUrl = process.env.API_HTTPS || process.env.services__api__https__0 || 'https://localhost:7038';

// `--mode demo` builds the light demo for GitHub Pages (docs/agents/light-demo.md): client only, under /MedApp/, with
// the stand-in API in the browser. Every other build has `__LIGHT_DEMO__` false, so the demo's code is left out of it.
export default defineConfig(({command, mode, isSsrBuild}) => {
  const isDemo = mode === 'demo';
  return {
    base: isDemo ? '/MedApp/' : '/',
    define: {__LIGHT_DEMO__: JSON.stringify(isDemo)},
    plugins: [
      // Must come before react(): it generates src/routeTree.gen.ts from src/routes.
      tanstackRouter({target: 'react', autoCodeSplitting: true}),
      react(),
    ],
    build: {
      // The server rendering reads the client build's manifest for the page's scripts and stylesheets (the build
      // copies it into dist/ssr/, as client-manifest.json). The server entry's own build needs none.
      manifest: !isSsrBuild && !isDemo,
      // The light demo is a static site of its own (the Pages workflow uploads it).
      ...(isDemo ? {outDir: 'dist-demo'} : {}),
    },
    // The built server entry (dist/ssr/entry-server.js) carries its packages in it: in the image's /app/ssr/, it has no
    // node_modules beside it.
    ssr: command === 'build' ? {noExternal: true} : undefined,
    server: {
      // The light demo's dev server (`npm run dev:demo`) needs no API, and keeps clear of the app's port.
      port: Number(process.env.PORT) || (isDemo ? 5174 : 5173),
      strictPort: true,
      // Same-origin /api, so the HttpOnly session cookie is first-party and CORS isn't involved (plan Q3).
      // /swagger too, so `npm run gen:api` can read the API's OpenAPI document without trusting its dev certificate.
      proxy: {
        // ws: the live notifications connection (/api/notifications/hub) is a WebSocket.
        '/api': {target: apiUrl, changeOrigin: true, secure: false, ws: true},
        '/swagger': {target: apiUrl, changeOrigin: true, secure: false},
      },
    },
    test: {
      environment: 'jsdom',
      // Tests run in a fixed time zone that is neither UTC nor UK time, in summer or winter, so a time read as the
      // browser's local time instead of UTC (or a UK day shifted by one) fails here, not only for some people.
      env: {TZ: 'America/New_York'},
      globals: true,
      setupFiles: ['./src/test/setup.ts'],
      css: false,
      coverage: {
        provider: 'v8',
        // json-summary feeds the PR coverage comment (see .github/workflows/main.yml).
        reporter: ['text-summary', 'json-summary'],
        // CI fails if coverage drops below these (just under where it is). What's left uncovered is defensive code:
        // fallbacks for fields the API always sends, and guards for browser states jsdom can't produce.
        thresholds: {lines: 99, statements: 99, functions: 99, branches: 92},
        include: ['src/**/*.{ts,tsx}'],
        // Generated code, the theme build, test helpers, and the thin route files and app entry, which wire
        // pages together and are covered through the route-guard and page tests.
        exclude: [
          'src/**/*.test.{ts,tsx}',
          'src/test/**',
          'src/routeTree.gen.ts',
          'src/api/schema.d.ts',
          'src/themes/**',
          'src/astryx-themes.ts',
          'src/entry-client.tsx',
          'src/entry-server.tsx',
          'src/routes/**',
          'src/app/App.tsx',
          // The light demo (docs/agents/light-demo.md): never in the production build, and checked by its own tests (the
          // tour's links, the stand-in's handlers and its pages) rather than held to the app's thresholds.
          'src/demo/**',
        ],
      },
    },
  };
});
