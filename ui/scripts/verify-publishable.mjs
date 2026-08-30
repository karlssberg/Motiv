#!/usr/bin/env node
/**
 * The publish-readiness gate: every check in this repository sees the workspace, and a publish
 * ships a tarball.
 *
 * The two are not the same artefact, and the differences are exactly where the defects hide. In
 * the workspace `@motiv-rules/react` reaches `@motiv-rules/core` through a symlink, so its
 * `workspace:*` range is never read; TypeScript resolves both packages through `paths` and their
 * `src`, so the `exports` map is never read either; and `files` decides nothing because nothing is
 * copied. All three are read for the first time by a consumer, on the registry, after the version
 * is immutable.
 *
 * So this packs every publishable package the way a publish would — `pnpm pack`, not `npm pack`,
 * because only pnpm's packer rewrites `workspace:` ranges — extracts the tarballs into a scratch
 * tree, and asserts against *that*:
 *
 *   - the entry points the `exports` map advertises exist in the tarball;
 *   - they resolve, for both value and type, from an ESM consumer *and* a CommonJS one under
 *     `node16` resolution — the check that catches a `types` condition pointing at the wrong
 *     module system, which is invisible at runtime and fatal at compile time;
 *   - no `workspace:` range survives packing;
 *   - the scope is published publicly, the licence text ships, and the manifest says where the
 *     source is;
 *   - the publishable packages agree on one version, because they release together.
 *
 * It deliberately does not check what the packages *do*: `scripts/isolated-consumer.mjs` drives
 * `rules-core`'s behaviour out of a packed tarball with nothing else installed, and each package's
 * own `typecheck` checks its declarations. What is left over, and what this owns, is whether the
 * artefact is shaped like something a consumer can install.
 *
 * Usage: `pnpm -r build`, then `pnpm verify:publishable` from `ui/`.
 * Set MOTIV_KEEP_SCRATCH=1 to leave the scratch tree behind for inspection.
 */
import { execFileSync } from 'node:child_process';
import { createRequire } from 'node:module';
import {
  cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

// The `ui` workspace root — this file lives one directory below it, in `scripts/`.
const workspaceRoot = fileURLToPath(new URL('../', import.meta.url));
const repoRoot = dirname(workspaceRoot.replace(/\/$/, ''));
const require = createRequire(import.meta.url);

const failures = [];
/** Collected rather than thrown: one run should report every defect, not the first one. */
function check(condition, message) {
  if (!condition) failures.push(message);
  return condition;
}

/**
 * The workspace's publishable packages: `pnpm-workspace.yaml`'s globs, minus everything marked
 * private. Derived rather than listed so a package added later is gated by existing, and a package
 * made public by deleting one line cannot slip past by not being on a list here.
 */
function publishablePackages() {
  const globs = readFileSync(join(workspaceRoot, 'pnpm-workspace.yaml'), 'utf8')
    .split('\n')
    .map((line) => line.match(/^\s*-\s*'?([^'\s]+)'?\s*$/)?.[1])
    .filter(Boolean);

  const found = [];
  for (const glob of globs) {
    // The workspace uses one shape — `<dir>/*` — so a full glob implementation would be dead code.
    const parent = join(workspaceRoot, glob.replace(/\/\*$/, ''));
    if (!existsSync(parent)) continue;
    for (const entry of readdirSync(parent)) {
      const dir = join(parent, entry);
      const manifestPath = join(dir, 'package.json');
      if (!existsSync(manifestPath)) continue;
      const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
      if (manifest.private) continue;
      found.push({ dir, manifest });
    }
  }
  return found.sort((a, b) => a.manifest.name.localeCompare(b.manifest.name));
}

/** Every file an `exports` map (or a legacy `main`/`module`/`types` field) points at. */
function advertisedFiles(manifest) {
  const paths = new Set();
  const walk = (value) => {
    if (typeof value === 'string') paths.add(value);
    else if (value && typeof value === 'object') Object.values(value).forEach(walk);
  };
  walk(manifest.exports);
  for (const field of ['main', 'module', 'types']) {
    if (manifest[field]) paths.add(manifest[field]);
  }
  return [...paths];
}

/** The subpaths a consumer can import: `.` plus every `./…` key of the exports map. */
function entryPoints(manifest) {
  // A string `exports` is the whole package at its root, and no `exports` at all falls back to
  // `main` — both are one entry point, and only an object form names subpaths.
  if (!manifest.exports || typeof manifest.exports === 'string') return [''];
  return Object.keys(manifest.exports).map((key) => (key === '.' ? '' : key.replace(/^\./, '')));
}

const scratch = mkdtempSync(join(tmpdir(), 'motiv-publishable-'));

try {
  const packages = publishablePackages();
  check(packages.length > 0, 'no publishable packages found — the workspace globs did not match.');

  const extracted = [];
  for (const { dir, manifest } of packages) {
    const name = manifest.name;
    if (!existsSync(join(dir, 'dist'))) {
      failures.push(`${name}: dist/ is missing — run \`pnpm -r build\` first.`);
      continue;
    }

    const tarballDir = mkdtempSync(join(scratch, 'pack-'));
    execFileSync('pnpm', ['pack', '--pack-destination', tarballDir], { cwd: dir, stdio: 'pipe' });
    const tarball = readdirSync(tarballDir).find((entry) => entry.endsWith('.tgz'));
    if (!check(tarball, `${name}: pnpm pack produced no tarball.`)) continue;

    // `--strip-components=1` drops the tarball's `package/` root, so what lands here is exactly
    // what an install would leave in a consumer's node_modules.
    const installed = join(scratch, 'consumer', 'node_modules', ...name.split('/'));
    mkdirSync(installed, { recursive: true });
    execFileSync('tar', ['-xzf', join(tarballDir, tarball), '-C', installed, '--strip-components=1']);

    // The packed manifest, not the source one: pnpm rewrites fields on the way in, and what the
    // registry serves is this.
    const packed = JSON.parse(readFileSync(join(installed, 'package.json'), 'utf8'));
    extracted.push({ name, packed, installed });

    for (const file of advertisedFiles(packed)) {
      check(
        existsSync(join(installed, file)),
        `${name}: the manifest advertises ${file}, which the tarball does not contain.`,
      );
    }

    for (const field of ['dependencies', 'peerDependencies', 'optionalDependencies']) {
      for (const [dep, range] of Object.entries(packed[field] ?? {})) {
        check(
          !String(range).startsWith('workspace:'),
          `${name}: ${field}.${dep} is still "${range}" in the tarball — a workspace range on the `
          + 'registry is uninstallable. It should have been rewritten by the packer.',
        );
      }
    }

    if (name.startsWith('@')) {
      check(
        packed.publishConfig?.access === 'public',
        `${name}: publishConfig.access is not "public" — npm defaults a scoped package to `
        + 'restricted, so the first publish would fail or land private.',
      );
    }

    for (const file of ['LICENSE', 'README.md']) {
      check(existsSync(join(installed, file)), `${name}: the tarball ships no ${file}.`);
    }

    check(
      typeof packed.repository?.url === 'string',
      `${name}: no repository.url — npm shows no source link, and provenance, which attests that a `
      + 'tarball was built from a named repository, has nothing to name.',
    );
    // Checked against this checkout rather than against a hard-coded repository, so the gate still
    // means something in a fork. A `directory` that points nowhere is how a package moves and takes
    // its "browse the source" link with it into a 404.
    check(
      typeof packed.repository?.directory === 'string'
        && existsSync(join(repoRoot, packed.repository.directory, 'package.json')),
      `${name}: repository.directory is "${packed.repository?.directory}", which is not a package `
      + 'directory in this checkout.',
    );
    check(Boolean(packed.homepage), `${name}: no homepage field.`);
    check(Boolean(packed.bugs), `${name}: no bugs field.`);
  }

  const versions = new Set(extracted.map(({ packed }) => packed.version));
  check(
    versions.size <= 1,
    `the publishable packages disagree on a version (${[...versions].join(', ')}) — they release `
    + 'together, on one tag.',
  );

  // The resolution check. Two consumers over the same extracted tree, differing only in whether
  // their package.json says `"type": "module"` — which is the whole of what decides, under
  // `node16`, which condition of the exports map TypeScript reads. A `types` condition pointing at
  // an ESM declaration under `require` fails here and nowhere else.
  if (extracted.length > 0) {
    const consumer = join(scratch, 'consumer');
    // `@types/react` is what makes `rules-react`'s declarations resolvable; it is a devDependency
    // of that package, so it is already linked in the workspace and copying beats re-installing.
    const reactTypes = packages
      .map(({ dir }) => join(dir, 'node_modules', '@types', 'react'))
      .find((path) => existsSync(path));
    if (reactTypes) {
      cpSync(reactTypes, join(consumer, 'node_modules', '@types', 'react'), { recursive: true });
    }

    const imports = extracted
      .flatMap(({ name, packed }) => entryPoints(packed).map((subpath) => `${name}${subpath}`))
      .map((specifier, index) => `import * as m${index} from '${specifier}';\nexport const e${index} = m${index};`)
      .join('\n');

    for (const [kind, type] of [['esm', 'module'], ['cjs', 'commonjs']]) {
      const dir = join(consumer, kind);
      mkdirSync(dir, { recursive: true });
      writeFileSync(join(dir, 'package.json'), `${JSON.stringify({ name: `${kind}-consumer`, private: true, version: '0.0.0', type }, null, 2)}\n`);
      writeFileSync(join(dir, 'consumer.ts'), `${imports}\n`);
      writeFileSync(join(dir, 'tsconfig.json'), `${JSON.stringify({
        compilerOptions: {
          module: 'node16',
          moduleResolution: 'node16',
          target: 'es2022',
          strict: true,
          noEmit: true,
          // The claim here is that the entry points *resolve*, in both module systems. Whether the
          // declarations themselves type-check is each package's own `typecheck` script, and
          // leaving it on would only import third-party declaration noise into this verdict.
          skipLibCheck: true,
        },
        files: ['consumer.ts'],
      }, null, 2)}\n`);

      try {
        execFileSync(process.execPath, [require.resolve('typescript/bin/tsc'), '-p', dir], { stdio: 'pipe' });
      } catch (error) {
        const output = `${error.stdout ?? ''}${error.stderr ?? ''}`.trim();
        failures.push(`a ${type} consumer cannot import the packed entry points:\n${output}`);
      }
    }
  }

  if (failures.length > 0) {
    for (const failure of failures) console.error(`verify-publishable: ${failure}`);
    throw new Error(`${failures.length} problem(s) would ship with a publish.`);
  }

  const names = extracted.map(({ name, packed }) => `${name}@${packed.version}`).join(', ');
  console.log(`verify-publishable: ${names} are shaped like something a consumer can install.`);
} catch (error) {
  console.error(`verify-publishable: ${error instanceof Error ? error.message : String(error)}`);
  process.exitCode = 1;
} finally {
  if (!process.env.MOTIV_KEEP_SCRATCH) rmSync(scratch, { recursive: true, force: true });
  else console.log(`verify-publishable: scratch tree kept at ${scratch}`);
}
