import { readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

/** The adapter's source root, with a trailing separator. */
export const SRC_ROOT = fileURLToPath(new URL('../src/', import.meta.url));

/** Every TypeScript file under a source root, recursively, in a stable order. */
export function sourceFiles(root: string = SRC_ROOT, dir: string = root): string[] {
  return readdirSync(dir).sort().flatMap((entry) => {
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) return sourceFiles(root, path);
    return /\.tsx?$/.test(path) ? [path] : [];
  });
}
