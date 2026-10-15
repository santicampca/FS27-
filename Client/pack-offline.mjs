// Packs Client/dist into ONE html file (FS27-jugable.html) that opens by double click, with no server and no internet: the page, the game's engine
// (WebAssembly) and the game data travel inside the file. Usage: node Client/pack-offline.mjs [dist dir] [output file]
// Nothing is published or uploaded: the file is for sending to testers by hand (mail, chat, USB).
import fs from 'node:fs'; import path from 'node:path';
const dist = path.resolve(process.argv[2] || path.join(path.dirname(new URL(import.meta.url).pathname), 'dist'));
const out = path.resolve(process.argv[3] || path.join(path.dirname(new URL(import.meta.url).pathname), 'dist-offline', 'FS27-jugable.html'));
const files = {};
const add = (rel) => { files[rel] = fs.readFileSync(path.join(dist, rel)).toString('base64'); };
for (const f of fs.readdirSync(path.join(dist, '_framework'))) add('_framework/' + f);
// the engine loader builds absolute addresses from its own address, which inside a packed file is not an address: it keeps the plain names and the resource loader below serves them
{
  const marker = 'new URL(e,je.scriptDirectory).toString()';
  let loader = fs.readFileSync(path.join(dist, '_framework', 'dotnet.js'), 'utf8');
  if (!loader.includes(marker)) throw new Error('dotnet.js changed: the packer needs an update');
  files['_framework/dotnet.js'] = Buffer.from(loader.replace(marker, 'e'), 'utf8').toString('base64');
  // the same in the native module: it names its WebAssembly file relative to its own address
  const nativeName = fs.readdirSync(path.join(dist, '_framework')).find(f => /^dotnet\.native\..*\.js$/.test(f));
  const nativeMarker = 'new URL("dotnet.native.wasm",import.meta.url).href';
  const native = fs.readFileSync(path.join(dist, '_framework', nativeName), 'utf8');
  if (!native.includes(nativeMarker)) throw new Error('dotnet.native js changed: the packer needs an update');
  files['_framework/' + nativeName] = Buffer.from(native.replace(nativeMarker, '"dotnet.native.wasm"'), 'utf8').toString('base64');
}
for (const f of fs.readdirSync(path.join(dist, 'data'))) add('data/' + f);
let html = fs.readFileSync(path.join(dist, 'index.html'), 'utf8');
const css = fs.readFileSync(path.join(dist, 'style.css'), 'utf8');
let js = fs.readFileSync(path.join(dist, 'game.js'), 'utf8');

// 1) the engine loader comes from the packed bytes instead of the network
const importLine = "import { dotnet } from './_framework/dotnet.js';";
if (!js.includes(importLine)) throw new Error('game.js changed: the import line was not found');
js = js.replace(importLine, `
const FILES = window.FS27_FILES;
const bytesOf = name => { const s = atob(FILES[name]); const a = new Uint8Array(s.length); for (let i = 0; i < s.length; i++) a[i] = s.charCodeAt(i); return a; };
const blobUrl = (name, type) => URL.createObjectURL(new Blob([bytesOf(name)], { type }));
const { dotnet } = await import(blobUrl('_framework/dotnet.js', 'text/javascript'));
const MIME = { wasm: 'application/wasm', json: 'application/json', dat: 'application/octet-stream' };
const loader = (type, name, defaultUri) => {
  const file = '_framework/' + name;
  if (!(file in FILES)) return null;
  if (type === 'dotnetjs') return blobUrl(file, 'text/javascript');
  const ext = name.split('.').pop();
  return Promise.resolve(new Response(bytesOf(file), { headers: { 'Content-Type': MIME[ext] || 'application/octet-stream' } }));
};`);
// 2) the loader hook
js = js.replace('dotnet.withDiagnosticTracing(false).create()', 'dotnet.withDiagnosticTracing(false).withResourceLoader(loader).create()');
// 3) the data files come from the packed bytes
js = js.replace("const res = await fetch('data/' + f); if (!res.ok) throw new Error('data/' + f + ' ' + res.status);\n    B.AddFile(f, await res.text());", "if (!(('data/' + f) in FILES)) throw new Error('data/' + f + ' is missing');\n    B.AddFile(f, new TextDecoder('utf-8').decode(bytesOf('data/' + f)));");
if (!js.includes('withResourceLoader(loader)') || !js.includes("bytesOf('data/' + f)")) throw new Error('game.js changed: the packer needs an update');

html = html.replace('<link rel="stylesheet" href="style.css">', `<style>${css}</style>`);
html = html.replace('<script type="module" src="game.js"></script>', `<script>window.FS27_FILES = ${JSON.stringify(files)};</script>\n<script type="module">\n${js}\n</script>`);
fs.mkdirSync(path.dirname(out), { recursive: true }); fs.writeFileSync(out, html);
console.log('packed ' + Object.keys(files).length + ' files into ' + out + ' (' + (fs.statSync(out).size / 1048576).toFixed(1) + ' MB)');
