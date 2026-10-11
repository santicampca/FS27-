// Copia public/ a dist/ (sitio estático sin paso de compilación).
import { cpSync, rmSync, mkdirSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const base = resolve(fileURLToPath(new URL('..', import.meta.url)));
rmSync(resolve(base, 'dist'), { recursive: true, force: true });
mkdirSync(resolve(base, 'dist'), { recursive: true });
cpSync(resolve(base, 'public'), resolve(base, 'dist'), { recursive: true });
console.log('build OK -> dist/: ' + readdirSync(resolve(base, 'dist')).join(', '));
