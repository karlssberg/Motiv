// @vitest-environment node
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { sourceFiles, SRC_ROOT } from './sources.js';

/**
 * Every bare specifier a source reaches for, in any spelling that actually resolves a module:
 * `import`/`export … from`, a side-effect `import`, a dynamic `import()`, and `require()` — under
 * either quote — with `import type` covered by the first of those.
 *
 * Matching one spelling would make this a gate that a different quote character walks past, and a
 * gate with a known way through is worse than none: it reports a property nobody is checking.
 */
export function bareImports(source: string): string[] {
  return [...source.matchAll(/(?:\bfrom|\bimport|\brequire)\s*\(?\s*(['"])([^'"]+)\1/g)]
    .map((match) => match[2]!)
    .filter((specifier) => !specifier.startsWith('.') && !specifier.startsWith('/'));
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

/** The gate's own reading — what it would and would not have caught. */
describe('finding what a source reaches for', () => {
  it('reads every spelling that resolves a module', () => {
    const source = [
      "import { a } from 'vue';",
      'import type { B } from "@motiv-rules/core";',
      "import 'side-effect';",
      "export { c } from 'reexported';",
      "const d = await import('dynamic');",
      'const e = require("required");',
    ].join('\n');

    expect(bareImports(source)).toEqual([
      'vue', '@motiv-rules/core', 'side-effect', 'reexported', 'dynamic', 'required',
    ]);
  });

  it('ignores paths inside the package, which are not a dependency on anything', () => {
    expect(bareImports("import { a } from './observe.js';\nimport { b } from '../paths.js';")).toEqual([]);
  });

  it('is not fooled by a quote character', () => {
    expect(bareImports('import { createElement } from "react";')).toEqual(['react']);
  });
});
