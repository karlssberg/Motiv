import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * The two properties the whole non-React story rests on: this package can *name* nothing outside
 * itself, and it declares nothing to install alongside it. Together they are what makes
 * "framework-free" a fact about the artefact rather than a claim about its authors — a Vue,
 * Svelte or plain-Node consumer installs `@motiv-rules/core` and gets exactly `@motiv-rules/core`.
 *
 * `scripts/isolated-consumer.mjs` proves the same thing end to end, against the packed tarball in
 * a tree where `react` does not resolve. This test is the fast half: it fails at the import that
 * breaks the property, rather than two steps later when a consumer's install cannot satisfy it.
 *
 * DOM-freeness is not checked here — it is compiler-enforced. `tsconfig.json` drops `DOM` from
 * `lib`, so `document`, `window` and `localStorage` do not resolve in this package at all.
 */

const srcDir = fileURLToPath(new URL('../src', import.meta.url));
const manifest = JSON.parse(
  readFileSync(fileURLToPath(new URL('../package.json', import.meta.url)), 'utf8'),
) as Record<string, unknown>;

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(path);
    return entry.isFile() && entry.name.endsWith('.ts') ? [path] : [];
  });
}

/**
 * Every way a module can name another one: static `import`/`export … from`, a side-effect
 * `import`, a dynamic `import()`, and `require()`. Type-only imports are deliberately included —
 * a `import type { … } from 'react'` still obliges the consumer to have React's types installed,
 * which is a dependency by any honest reading.
 */
const SPECIFIER_PATTERNS = [
  // `[^;]*?` rather than `[^;\n]*?`: nearly every import in this package spans several lines, so a
  // line-bound pattern would scan almost nothing while appearing to scan everything. A statement
  // terminator still bounds it, so the match cannot run on into the code below an import.
  /(?:^|\n)\s*(?:import|export)\b[^;]*?\bfrom\s*['"]([^'"]+)['"]/g,
  /(?:^|\n)\s*import\s*['"]([^'"]+)['"]/g,
  /\bimport\s*\(\s*['"]([^'"]+)['"]\s*\)/g,
  /\brequire\s*\(\s*['"]([^'"]+)['"]\s*\)/g,
];

function specifiersIn(source: string): string[] {
  return SPECIFIER_PATTERNS.flatMap((pattern) =>
    [...source.matchAll(pattern)].map((match) => match[1]!),
  );
}

describe('framework-freeness', () => {
  const modules = sourceFiles(srcDir).map(
    (file) => [file.slice(srcDir.length + 1), specifiersIn(readFileSync(file, 'utf8'))] as const,
  );

  it('has source to check', () => {
    expect(modules.length).toBeGreaterThan(10);
    // A pattern that matched nothing would pass every assertion below in silence.
    expect(modules.flatMap(([, specifiers]) => specifiers).length).toBeGreaterThan(modules.length);
  });

  it.each(modules)('src/%s imports nothing outside the package', (_name, specifiers) => {
    const bare = specifiers.filter((s) => !s.startsWith('./') && !s.startsWith('../'));
    expect(bare).toEqual([]);
  });

  it.each(modules)('src/%s uses the extension Node resolution needs', (_name, specifiers) => {
    expect(specifiers.filter((s) => !s.endsWith('.js'))).toEqual([]);
  });

  it('declares nothing to install alongside it', () => {
    expect(manifest.dependencies).toBeUndefined();
    expect(manifest.peerDependencies).toBeUndefined();
    expect(manifest.optionalDependencies).toBeUndefined();
  });

  it('publishes the build output the isolated consumer imports', () => {
    expect(manifest.files).toEqual(['dist']);
  });
});
