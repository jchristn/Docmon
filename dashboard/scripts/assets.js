import { copyFileSync, mkdirSync } from 'node:fs';
mkdirSync('public/assets', { recursive: true });
for (const name of ['logo.png', 'logo.ico']) copyFileSync(`../assets/${name}`, `public/assets/${name}`);
