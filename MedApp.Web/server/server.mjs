// The server that renders MedApp's pages (server-side rendering), a small Node HTTP server.
//
// - Development (`npm run dev`, which the AppHost runs): `--dev`. It hosts Vite as middleware, so it serves the
//   app's source with hot reload and proxies /api and /swagger to the API (vite.config.ts), and renders each page
//   from the source (src/entry-server.tsx) on http://localhost:5173.
// - Production: the API starts it (MedApp.API's SsrHost) and forwards page requests to it; the API serves the
//   app's files itself. It renders each page from the built server entry (`npm run build` puts it, and this file,
//   in dist/ssr/, which publishing copies into the API's wwwroot/ssr/).
//
// Settings, from the environment:
// - PORT: where it listens (5173 by default). HOST: the interface (localhost by default).
// - MEDAPP_API_URL: where it reaches the API for a page's data. In development, by default, this server itself:
//   its /api proxy (vite.config.ts) passes the calls on to the API, as it does the browser's, and accepts the API's
//   development certificate, which Node wouldn't.
// - MEDAPP_SSR_KEY: the key the API gave it, sent with its API calls (see SsrHost).
// - In development, the API itself (for /api WebSockets, see `proxyWebSocket`): API_HTTPS or the AppHost's
//   services__api__https__0, as vite.config.ts uses.
import {existsSync, readFileSync} from 'node:fs';
import {createServer, request as httpRequest} from 'node:http';
import {request as httpsRequest} from 'node:https';
import path from 'node:path';
import {pipeline, Readable} from 'node:stream';
import {fileURLToPath, pathToFileURL} from 'node:url';

const isDev = process.argv.includes('--dev');
if (!isDev) process.env.NODE_ENV ??= 'production';

const here = path.dirname(fileURLToPath(import.meta.url));
const port = Number(process.env.PORT) || 5173;
const host = process.env.HOST || 'localhost';
const apiUrl = process.env.MEDAPP_API_URL || (isDev ? `http://localhost:${port}` : 'https://localhost:7038');
const ssrKey = process.env.MEDAPP_SSR_KEY || undefined;
const devApiTarget = process.env.API_HTTPS || process.env.services__api__https__0 || 'https://localhost:7038';

const server = createServer();

/** In development: Vite, and how to get the page's assets and the server entry (fresh, from the source). */
async function createDevRenderer() {
  const {createServer: createViteServer} = await import('vite');
  const appRoot = path.resolve(here, '..');
  const vite = await createViteServer({
    root: appRoot,
    appType: 'custom',
    server: {middlewareMode: true, ws: {server}},
  });

  // The app's stylesheets (the global ones its entry and theme import), as links in the page's head, so the page
  // is styled from its first paint. The router drops them once the browser has taken over and Vite's own are on.
  const cssImporters = ['src/entry-client.tsx', 'src/astryx-themes.ts'];
  async function devCss() {
    const links = [];
    for (const importer of cssImporters) {
      const file = path.join(appRoot, importer);
      for (const [, spec] of readFileSync(file, 'utf8').matchAll(/^import\s+['"]([^'"]+\.css)['"]/gm)) {
        const resolved = await vite.pluginContainer.resolveId(spec, file);
        if (!resolved) continue;
        const id = resolved.id.replaceAll('\\', '/');
        const rootPosix = appRoot.replaceAll('\\', '/');
        const href = id.startsWith(rootPosix + '/') ? id.slice(rootPosix.length) : `/@fs/${id.replace(/^\//, '')}`;
        // ?direct: Vite serves the stylesheet itself, not the module that adds it.
        links.push({href: `${href}?direct`, 'data-tanstack-router-dev-styles': true});
      }
    }
    return links;
  }

  // Vite's own scripts (its client, and React Fast Refresh's preamble), then the app's entry.
  async function devScripts(url) {
    const html = await vite.transformIndexHtml(url, '<html><head></head><body></body></html>');
    const scripts = [];
    for (const [, rawAttrs, children] of html.matchAll(/<script([^>]*)>([\s\S]*?)<\/script>/g)) {
      const attrs = Object.fromEntries([...rawAttrs.matchAll(/([\w-]+)="([^"]*)"/g)].map(([, k, v]) => [k, v]));
      scripts.push(children.trim() ? {attrs, children} : {attrs});
    }
    scripts.push({attrs: {type: 'module', src: '/src/entry-client.tsx'}});
    return scripts;
  }

  return {
    middlewares: vite.middlewares,
    async render(request) {
      const {render} = await vite.ssrLoadModule('/src/entry-server.tsx');
      const url = new URL(request.url).pathname;
      const manifest = {routes: {__root__: {css: await devCss(), scripts: await devScripts(url)}}};
      return render(request, {manifest, context: requestContext(request)});
    },
    fixError: (error) => vite.ssrFixStacktrace(error),
  };
}

/** In production: the built server entry, and the page's assets from the client build's manifest. */
async function createProdRenderer() {
  // Published, this file sits in wwwroot/ssr/ beside the entry; in the repo, the build is in ../dist.
  const dist = existsSync(path.join(here, 'entry-server.js')) ? path.resolve(here, '..') : path.resolve(here, '../dist');
  const {render} = await import(pathToFileURL(path.join(dist, 'ssr/entry-server.js')).href);
  const viteManifest = JSON.parse(readFileSync(path.join(dist, '.vite/manifest.json'), 'utf8'));

  const entry = viteManifest['index.html'];
  const css = new Set();
  const preloads = new Set();
  const visit = (key, seen = new Set()) => {
    if (seen.has(key)) return;
    seen.add(key);
    const chunk = viteManifest[key];
    for (const file of chunk.css ?? []) css.add(`/${file}`);
    if (key !== 'index.html') preloads.add(`/${chunk.file}`);
    for (const imported of chunk.imports ?? []) visit(imported, seen);
  };
  visit('index.html');
  const manifest = {
    routes: {
      __root__: {
        css: [...css],
        preloads: [...preloads],
        scripts: [{attrs: {type: 'module', src: `/${entry.file}`}}],
      },
    },
  };

  return {
    middlewares: null,
    render: (request) => render(request, {manifest, context: requestContext(request)}),
    fixError: () => {},
  };
}

/** What the app's API calls need while rendering this request (see src/ssr/requestContext.ts). */
function requestContext(request) {
  return {apiUrl, cookie: request.headers.get('cookie') ?? undefined, ssrKey};
}

/**
 * The incoming request as a fetch Request, for the router. Its address is the one the browser used: behind the API
 * (production), that's the API's, which it passes on in X-Forwarded-Proto and X-Forwarded-Host.
 */
function toRequest(req) {
  const proto = req.headers['x-forwarded-proto'] || 'http';
  const host = req.headers['x-forwarded-host'] || req.headers.host || `localhost:${port}`;
  const origin = `${proto}://${host}`;
  const headers = new Headers();
  for (const [name, value] of Object.entries(req.headers)) {
    if (value === undefined) continue;
    for (const one of Array.isArray(value) ? value : [value]) headers.append(name, one);
  }
  const controller = new AbortController();
  req.on('close', () => !req.complete && controller.abort());
  return new Request(new URL(req.url ?? '/', origin), {method: req.method, headers, signal: controller.signal});
}

/** Writes a fetch Response back to the Node response, streaming its body. */
async function send(response, res) {
  const headers = {};
  response.headers.forEach((value, name) => {
    if (name === 'set-cookie') return;
    headers[name] = value;
  });
  const cookies = response.headers.getSetCookie?.() ?? [];
  if (cookies.length > 0) headers['set-cookie'] = cookies;
  res.writeHead(response.status, headers);
  if (!response.body) {
    res.end();
    return;
  }
  // If the page fails part-way, end only this response, cut short (so the API sees it fail), not the whole server.
  pipeline(Readable.fromWeb(response.body), res, (error) => {
    if (!error) return;
    // The browser going away isn't the page's fault.
    if (error.code !== 'ERR_STREAM_PREMATURE_CLOSE') console.error('Sending the page failed part-way:', error);
    res.destroy();
  });
}

const renderer = isDev ? await createDevRenderer() : await createProdRenderer();

server.on('request', (req, res) => {
  const renderPage = async () => {
    if (req.method !== 'GET' && req.method !== 'HEAD') {
      res.writeHead(405, {Allow: 'GET, HEAD'}).end();
      return;
    }
    try {
      await send(await renderer.render(toRequest(req)), res);
    } catch (error) {
      renderer.fixError(error);
      console.error(error);
      if (!res.headersSent) res.writeHead(500, {'Content-Type': 'text/plain; charset=utf-8'});
      res.end('The page could not be rendered.');
    }
  };
  if (renderer.middlewares) renderer.middlewares(req, res, renderPage);
  else void renderPage();
});

/**
 * In development, passes an /api WebSocket (the live notifications hub) on to the API. Vite's proxy does this only
 * when Vite owns the server; hosted as middleware here, it doesn't, so the hub's socket would open and never connect.
 * Like Vite's proxy, it accepts the API's development certificate. Vite's own (hot reload) socket isn't touched.
 */
const CRLF = '\r\n';

function proxyWebSocket(req, socket, head) {
  const target = new URL(req.url, devApiTarget);
  const send = target.protocol === 'https:' ? httpsRequest : httpRequest;
  const upstream = send(target, {method: req.method, headers: {...req.headers, host: target.host}, rejectUnauthorized: false});
  upstream.on('upgrade', (response, upstreamSocket, upstreamHead) => {
    const headers = [];
    for (let i = 0; i < response.rawHeaders.length; i += 2) headers.push(`${response.rawHeaders[i]}: ${response.rawHeaders[i + 1]}`);
    socket.write(['HTTP/1.1 101 Switching Protocols', ...headers, '', ''].join(CRLF));
    if (upstreamHead.length > 0) socket.write(upstreamHead);
    if (head.length > 0) upstreamSocket.write(head);
    upstreamSocket.on('error', () => socket.destroy());
    socket.on('error', () => upstreamSocket.destroy());
    upstreamSocket.pipe(socket).pipe(upstreamSocket);
  });
  // The API refused the upgrade (e.g. signed out): say so, and close.
  upstream.on('response', (response) => {
    socket.end([`HTTP/1.1 ${response.statusCode} ${response.statusMessage}`, 'Connection: close', '', ''].join(CRLF));
    response.resume();
  });
  upstream.on('error', () => socket.destroy());
  upstream.end();
}

if (isDev) {
  server.on('upgrade', (req, socket, head) => {
    if (req.url?.startsWith('/api/')) proxyWebSocket(req, socket, head);
  });
}

server.listen(port, host, () => {
  console.log(`MedApp pages: http://${host}:${port}/ (${isDev ? 'development' : 'production'}, API at ${apiUrl})`);
});
