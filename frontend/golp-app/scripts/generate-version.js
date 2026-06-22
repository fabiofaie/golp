const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

function run(cmd) {
  return execSync(cmd, { encoding: 'utf8' }).trim();
}

let version;
let buildHash;
try {
  version = 'v' + run('git rev-list --count HEAD');
  buildHash = run('git rev-parse --short HEAD');
} catch {
  version = 'v0-dev';
  buildHash = 'unknown';
}

const outPath = path.join(__dirname, '..', 'src', 'app', 'version.ts');
const content =
  `export const APP_VERSION = '${version}';\n` +
  `export const APP_BUILD_HASH = '${buildHash}';\n`;

fs.writeFileSync(outPath, content);
console.log(`Generated ${outPath}: ${version} (${buildHash})`);
