import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, it, expect } from 'vitest';
import { CUSTOMER, ORDER } from './expression/fixture-schema.js';
import { analyseLeaf, printLeaf, type LeafScope } from '../src/expression/index.js';

interface CorpusProblem { code: string; contains?: string[]; range?: [number, number] }
interface CorpusFact { text: string; type: string; from?: string }
interface CorpusCase {
  name: string;
  scope: 'customer' | 'order';
  leaf: string;
  problems?: CorpusProblem[];
  warnings?: number;
  facts?: CorpusFact[];
  result?: string;
}
interface Corpus { parameters: Record<string, 'integer' | 'number' | 'string' | 'boolean'>; cases: CorpusCase[] }

// `with { type: 'json' }` import attributes need `resolveJsonModule`, which this package's
// tsconfig does not set — read and parse the file directly instead (see task-19-brief.md).
const corpusPath = fileURLToPath(new URL('./expression/corpus.json', import.meta.url));
const corpus = JSON.parse(readFileSync(corpusPath, 'utf-8')) as Corpus;

const parameters = Object.fromEntries(
  Object.entries(corpus.parameters).map(([name, type]) => [name, { type }]),
) as LeafScope['parameters'];

const scopes: Record<string, LeafScope> = {
  customer: { modelName: 'customer', model: CUSTOMER, parameters, vars: {} },
  order: { modelName: 'each of orders', model: ORDER, parameters, vars: {} },
};

describe('expression corpus', () => {
  for (const c of corpus.cases) {
    it(c.name, () => {
      const analysis = analyseLeaf(c.leaf, scopes[c.scope]!);
      const errors = analysis.problems.filter((p) => !p.warning);
      const warnings = analysis.problems.filter((p) => p.warning);
      expect(warnings).toHaveLength(c.warnings ?? 0);

      if (c.problems) {
        expect(errors.map((e) => e.code)).toEqual(c.problems.map((p) => p.code));
        c.problems.forEach((p, i) => {
          for (const fragment of p.contains ?? []) expect(errors[i]!.message).toContain(fragment);
          if (p.range) expect([errors[i]!.from, errors[i]!.to]).toEqual(p.range);
        });
        return;
      }

      expect(errors).toEqual([]);
      for (const f of c.facts ?? []) {
        const fact = analysis.facts.find((x) => x.node.kind !== 'binary' && printLeaf(x.node) === f.text);
        expect(fact, `fact for ${f.text}`).toBeDefined();
        expect(fact!.type).toBe(f.type);
        if (f.from !== undefined) expect(fact!.from).toBe(f.from);
      }
      if (c.result) expect(analysis.types.get(analysis.ast!)).toBe(c.result);
    });
  }
});
