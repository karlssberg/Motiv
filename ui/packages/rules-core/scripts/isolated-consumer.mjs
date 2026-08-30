#!/usr/bin/env node
/**
 * The "no React present" obligation, run against the artefact rather than the workspace.
 *
 * Bundle spec 4 asks that `rules-core` build and work with no React installed. Inside this
 * monorepo that is hard to prove: React is one `node_modules` away from every file here, so an
 * accidental import would resolve, typecheck and pass its tests. So this packs the package the
 * way a publish would, extracts the tarball into a scratch tree where *nothing else* is
 * installed, and drives it from plain Node — through both conditions of the exports map and both
 * entry points — asserting on the way that `react` does not resolve at all.
 *
 * It therefore also checks two things no unit test can see: that `files` publishes everything the
 * package needs at runtime, and that the exports map resolves for a consumer who has no bundler.
 * A consumer's first import is otherwise the first thing to find out when either is wrong.
 *
 * Usage: build the package first, then `pnpm --filter @motiv-rules/core verify:isolated`.
 * Set MOTIV_KEEP_SCRATCH=1 to leave the scratch tree behind for inspection.
 */
import { execFileSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const packageDir = dirname(fileURLToPath(new URL('.', import.meta.url)));
const fixtures = join(packageDir, 'scripts', 'consumer');

/** Thrown rather than exited, so the scratch tree is still cleaned up on the way out. */
function fail(message) {
  throw new Error(message);
}

const scratch = mkdtempSync(join(tmpdir(), 'motiv-isolated-'));
// Outside the repository on purpose: Node resolves `node_modules` by walking up, so a scratch tree
// inside `ui/` would find the workspace's React and the central assertion here would be vacuous.

try {
  if (!existsSync(join(packageDir, 'dist'))) {
    fail('dist/ is missing — run `pnpm --filter @motiv-rules/core build` first.');
  }

  // `npm pack` rather than `pnpm pack`: npm ships with Node, so this runs wherever the check does,
  // and the one thing pnpm's packer adds — rewriting `workspace:` dependency ranges — cannot matter
  // to a package that has no dependencies at all. `test/framework-free.test.ts` is what keeps that
  // true; if it ever stops being true, this line has to change with it.
  execFileSync('npm', ['pack', '--silent', '--pack-destination', scratch], {
    cwd: packageDir, stdio: 'inherit',
  });
  const tarball = readdirSync(scratch).find((entry) => entry.endsWith('.tgz'));
  if (!tarball) fail('npm pack produced no tarball.');

  const consumer = join(scratch, 'consumer');
  const installed = join(consumer, 'node_modules', '@motiv-rules', 'core');
  mkdirSync(installed, { recursive: true });
  // `--strip-components=1` drops the tarball's `package/` root, which is exactly what an install
  // does: what lands here is what a consumer would have in their node_modules, no more.
  execFileSync('tar', ['-xzf', join(scratch, tarball), '-C', installed, '--strip-components=1']);

  writeFileSync(
    join(consumer, 'package.json'),
    `${JSON.stringify({ name: 'isolated-consumer', private: true, version: '0.0.0', type: 'module' }, null, 2)}\n`,
  );
  cpSync(fixtures, consumer, { recursive: true });

  // NODE_PATH is cleared so the assertion that `react` is unresolvable cannot be defeated by the
  // environment the check happens to run in.
  const env = { ...process.env };
  delete env.NODE_PATH;

  for (const fixture of ['esm.mjs', 'cjs.cjs']) {
    try {
      execFileSync(process.execPath, [fixture], { cwd: consumer, env, stdio: 'inherit' });
    } catch {
      // The fixture has already printed its own assertion failure to the inherited stderr; a
      // second stack trace from this process would only bury it.
      fail(`${fixture} failed in the isolated tree (see the assertion above).`);
    }
  }

  console.log('isolated-consumer: @motiv-rules/core runs with nothing else installed.');
} catch (error) {
  console.error(`isolated-consumer: ${error.message}`);
  process.exitCode = 1;
} finally {
  if (!process.env.MOTIV_KEEP_SCRATCH) rmSync(scratch, { recursive: true, force: true });
  else console.log(`isolated-consumer: scratch tree kept at ${scratch}`);
}
