import fs from 'node:fs';
import path from 'node:path';
import type {Plugin} from 'vite';

/**
 * The full demo's backend files (docs/full-browser-demo-plan.md, Phase 4): the .NET backend published for the browser
 * (`dotnet publish MedApp.Browser -c Release`, its `wwwroot`) and the demo database (`dotnet run --project
 * tools/MedApp.BrowserDb -c Release -- --out <folder>`), served beside the app under its base (`/MedApp/full/`) by
 * the dev server and copied into the build's output. Where they are comes from two environment variables, or the
 * folders those commands use by default:
 *   FULL_DEMO_BACKEND   the published wwwroot (default ../MedApp.Browser/bin/Release/net10.0/publish/wwwroot)
 *   FULL_DEMO_DATABASE  the database folder (default ../MedApp.Browser/bin/demo-db)
 */
export function fullDemoFolders(root: string) {
  return {
    backend: path.resolve(root, process.env.FULL_DEMO_BACKEND ?? '../MedApp.Browser/bin/Release/net10.0/publish/wwwroot'),
    database: path.resolve(root, process.env.FULL_DEMO_DATABASE ?? '../MedApp.Browser/bin/demo-db'),
  };
}

/** The database's two files, from the database folder. */
const DATABASE_FILES = ['medapp.db.gz', 'db-version.json'];

/**
 * The ICU data files the page won't load: the runtime picks one by the browser's language, and for English and the
 * other European languages it's `icudt_EFIGS`. They're still copied (another language loads its own).
 */
const OTHER_ICU = /^icudt_(CJK|no_CJK)\./;

/** What the runtime file's sizes add up to (`runtime-size.json`), for the login page's "12 of 20 MB". */
export interface RuntimeSize {
  /** The files the backend reads through its resource loader, as read (uncompressed). */
  bytes: number;
  /** The same files compressed (their published `.gz`), about what a server that compresses sends. */
  compressedBytes: number;
}

/** Every file to serve or copy: its path under the base, and where it comes from. */
export function fullDemoFiles(folders: {backend: string; database: string}): Map<string, string> {
  const files = new Map<string, string>();
  const framework = path.join(folders.backend, '_framework');
  if (fs.existsSync(framework))
    for (const name of fs.readdirSync(framework)) if (!/\.(br|gz)$/.test(name)) files.set(`_framework/${name}`, path.join(framework, name));
  files.set('backend.js', path.join(folders.backend, 'backend.js'));
  for (const name of DATABASE_FILES) files.set(name, path.join(folders.database, name));
  return files;
}

export function runtimeSize(files: Map<string, string>): RuntimeSize {
  const size = {bytes: 0, compressedBytes: 0};
  for (const [name, file] of files) {
    const base = path.basename(name);
    // The JavaScript modules aren't read through the loader, so they aren't counted.
    if (!name.startsWith('_framework/') || base.endsWith('.js') || OTHER_ICU.test(base) || !fs.existsSync(file)) continue;
    const raw = fs.statSync(file).size;
    size.bytes += raw;
    size.compressedBytes += fs.existsSync(`${file}.gz`) ? fs.statSync(`${file}.gz`).size : raw;
  }
  return size;
}

const TYPES: Record<string, string> = {
  '.js': 'text/javascript',
  '.json': 'application/json',
  '.wasm': 'application/wasm',
  '.gz': 'application/gzip',
};

const missing = (files: Map<string, string>) => [...files.values()].filter((file) => !fs.existsSync(file));

const HOW = 'Publish the backend (dotnet publish MedApp.Browser -c Release) and build the database (dotnet run --project tools/MedApp.BrowserDb -c Release -- --out MedApp.Browser/bin/demo-db), or set FULL_DEMO_BACKEND and FULL_DEMO_DATABASE (MedApp.Web/README.md).';

/** Serves the backend's files in `npm run dev:full-demo`, and copies them into `dist-full-demo/` in the build. */
export function fullDemoBackend(base: string): Plugin {
  let outDir = '';
  let root = process.cwd();
  return {
    name: 'medapp-full-demo-backend',
    configResolved(config) {
      outDir = path.resolve(config.root, config.build.outDir);
      root = config.root;
    },
    configureServer(server) {
      const folders = fullDemoFolders(root);
      const files = fullDemoFiles(folders);
      const absent = missing(files);
      if (absent.length) server.config.logger.warn(`[full demo] ${absent.length} of the backend's files aren't there (${absent[0]}). ${HOW}`);
      server.middlewares.use((req, res, next) => {
        const pathname = decodeURIComponent(new URL(req.url ?? '/', 'http://localhost').pathname);
        if (!pathname.startsWith(base)) return next();
        const name = pathname.slice(base.length);
        if (name === 'runtime-size.json') {
          res.writeHead(200, {'Content-Type': 'application/json', 'Cache-Control': 'no-cache'}).end(JSON.stringify(runtimeSize(files)));
          return;
        }
        const file = files.get(name);
        if (!file || !fs.existsSync(file)) return next();
        res.writeHead(200, {
          'Content-Type': TYPES[path.extname(file)] ?? 'application/octet-stream',
          'Content-Length': fs.statSync(file).size,
          'Cache-Control': 'no-cache',
        });
        fs.createReadStream(file).pipe(res);
      });
    },
    closeBundle() {
      const files = fullDemoFiles(fullDemoFolders(root));
      const absent = missing(files);
      if (absent.length) throw new Error(`The full demo's backend files aren't there (${absent.join(', ')}). ${HOW}`);
      for (const [name, file] of files) {
        const target = path.join(outDir, name);
        fs.mkdirSync(path.dirname(target), {recursive: true});
        fs.copyFileSync(file, target);
      }
      fs.writeFileSync(path.join(outDir, 'runtime-size.json'), JSON.stringify(runtimeSize(files)));
    },
  };
}
