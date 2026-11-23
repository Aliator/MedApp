import {cpSync, existsSync, rmSync, writeFileSync} from 'node:fs';
import {join, resolve} from 'node:path';

const [light, full, out] = process.argv.slice(2).map((p) => resolve(p));
if (!light || !full || !out) {
  console.error('Usage: node scripts/assemble-pages.mjs <dist-demo> <dist-full-demo> <out>');
  process.exit(2);
}
for (const dir of [light, full]) {
  if (!existsSync(join(dir, 'index.html'))) {
    console.error(`No index.html in ${dir}: build it first.`);
    process.exit(1);
  }
}

rmSync(out, {recursive: true, force: true});
cpSync(light, out, {recursive: true});
cpSync(full, join(out, 'full'), {recursive: true});
rmSync(join(out, 'full', '404.html'), {force: true});

writeFileSync(
  join(out, '404.html'),
  `<!doctype html>
<html lang="en-GB">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>MedApp</title>
    <script src="/MedApp/404.js"></script>
  </head>
  <body></body>
</html>
`,
);

writeFileSync(
  join(out, '404.js'),
  `(function () {
  var app = location.pathname.indexOf('/MedApp/full/') === 0 || location.pathname === '/MedApp/full' ? '/MedApp/full/index.html' : '/MedApp/index.html';
  fetch(app, {cache: 'no-cache'})
    .then(function (response) { return response.text(); })
    .then(function (html) { document.open(); document.write(html); document.close(); });
})();
`,
);

console.log(`Assembled ${out}: the light demo at /MedApp/, the full demo at /MedApp/full/, 404.html loading the right one.`);
