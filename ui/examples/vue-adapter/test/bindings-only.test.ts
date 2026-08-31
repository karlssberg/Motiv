// @vitest-environment node
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { sourceFiles, SRC_ROOT } from './sources.js';

/** Every bare specifier a source imports, `import type` included. */
function bareImports(source: string): string[] {
  return [...source.matchAll(/from\s+'([^']+)'/g)]
    .map((match) => match[1]!)
    .filter((specifier) => !specifier.startsWith('.'));
}

/**
 * The claim this file keeps honest is the one the tier table makes about a second runtime: that an
 * adapter is *bindings only*, and that everything else already sits in a core with no framework in
 * it. That is a property of the source, so it is read off the source.
 *
 * `@motiv-rules/core`'s own `test/framework-free.test.ts` is this test's mirror — it fails on any
 * bare import at all. Together they state the two halves of one sentence: the core reaches for
 * nothing, and the adapter reaches only for the framework it adapts.
 */
describe('the adapter', () => {
  const files = sourceFiles();
  const allowed = ['vue', '@motiv-rules/core', '@motiv-rules/core/workflow'];

  it('has source to check', () => {
    expect(files.length).toBeGreaterThan(0);
  });

  it.each(files.map((file) => [file.slice(SRC_ROOT.length), file] as const))(
    'src/%s imports nothing but vue and the core',
    (_name, file) => {
      const disallowed = bareImports(readFileSync(file, 'utf8'))
        .filter((specifier) => !allowed.includes(specifier));
      expect(disallowed).toEqual([]);
    },
  );
});
