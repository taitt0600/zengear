# shadcn CLI Reference

Copied from shadcn-ui/ui at commit `ba1503c64472b82256d9de95e2e62c5901a38e8b` (MIT, see `LICENSE.md`) and modified
for ZenGear: the project rules at the top of `SKILL.md` win over this file.

Configuration is read from `components.json`.

> **IMPORTANT:** Always run commands as `npm exec --no -- shadcn` from `web/`.

> **IMPORTANT:** Only use the flags documented below. Do not invent or guess flags — if a flag isn't listed here, it doesn't exist. The CLI auto-detects the package manager from the project's lockfile; there is no `--package-manager` flag.

## Contents

- Commands: add (dry-run), search, view, docs, info; `init` is not used (below)

---

## Commands

### `init` — not used

`web/` is set up. Web-shell did not run `init`: in shadcn 4.22.1 it offers only presets (the default `base-nova` is
Base UI, not approved) and no `new-york` style. `web/components.json` was written by hand with style `new-york` (on
Radix), then `npm exec --no -- shadcn add index <components> --yes` installed the registry style item `index`
(the theme variables and the packages of DEC-111 (1)) with the components. `init` is not run again: its presets
bring other styles, and `--force` and `--reinstall` overwrite configuration or components.

### `add` — Add components

> **IMPORTANT:** To compare local components against upstream or to preview changes, ALWAYS use `npm exec --no -- shadcn add <component> --dry-run`, `--diff`, or `--view`. NEVER fetch raw files from GitHub or other sources manually. The CLI handles registry resolution, file paths, and CSS diffing automatically.

```bash
npm exec --no -- shadcn add [components...] [options]
```

Accepts component names of the official registry (`button`, `@shadcn/button`). Registry-prefixed names of other
registries, GitHub item addresses, URLs and local paths need a Decision first (project rules).

| Flag            | Short | Description                                                                                                          | Default |
| --------------- | ----- | -------------------------------------------------------------------------------------------------------------------- | ------- |
| `--yes`         | `-y`  | Skip confirmation prompt                                                                                             | `false` |
| `--cwd <cwd>`   | `-c`  | Working directory                                                                                                    | current |
| `--silent`      | `-s`  | Mute output                                                                                                          | `false` |
| `--dry-run`     |       | Preview all changes without writing files                                                                            | `false` |
| `--diff [path]` |       | Show diffs. Without a path, shows the first 5 files. With a path, shows that file only (implies `--dry-run`)         | —       |
| `--view [path]` |       | Show file contents. Without a path, shows the first 5 files. With a path, shows that file only (implies `--dry-run`) | —       |

`--overwrite`, `--all` and `--path` are not used: an update of a component is a `chore` pull request of its own,
and components go only to the path of `web/components.json`.

#### Dry-Run Mode

Use `--dry-run` to preview what `add` would do without writing any files. `--diff` and `--view` both imply `--dry-run`.

```bash
# Preview all changes.
npm exec --no -- shadcn add button --dry-run

# Show diffs for all files (top 5).
npm exec --no -- shadcn add button --diff

# Show the diff for a specific file.
npm exec --no -- shadcn add button --diff button.tsx

# Show contents for all files (top 5).
npm exec --no -- shadcn add button --view

# Show the full content of a specific file.
npm exec --no -- shadcn add button --view button.tsx

# CSS diffs.
npm exec --no -- shadcn add button --diff globals.css
```

**When to use dry-run:**

- When the user asks "what files will this add?" or "what will this change?" — use `--dry-run`.
- When the user wants to inspect component source code without installing — use `--view`.
- When checking what CSS changes would be made to `globals.css` — use `--diff globals.css`.

> **`add --dry-run` vs `view`:** Prefer `npm exec --no -- shadcn add --dry-run/--diff/--view` over `npm exec --no -- shadcn view` when the user wants to preview changes to their project. `view` only shows raw registry metadata. `add --dry-run` shows exactly what would happen in the user's project: resolved file paths, diffs against existing files, and CSS updates.

### `search` — Search registries

```bash
npm exec --no -- shadcn search @shadcn [options]
```

Fuzzy search across registries. Also aliased as `npm exec --no -- shadcn list`. Without `-q`, lists all items.
Search the official registry `@shadcn` only.

| Flag                | Short | Description                                       | Default |
| ------------------- | ----- | ------------------------------------------------- | ------- |
| `--query <query>`   | `-q`  | Search query                                      | —       |
| `--type <type>`     | `-t`  | Filter by item type (e.g. `ui`, `block`, `hook`); comma-separated | —       |
| `--limit <number>`  | `-l`  | Max items to display                              | `100`   |
| `--offset <number>` | `-o`  | Items to skip                                     | `0`     |
| `--json`            |       | Output as JSON                                    | `false` |
| `--cwd <cwd>`       | `-c`  | Working directory                                 | current |

### `view` — View item details

```bash
npm exec --no -- shadcn view <items...> [options]
```

Displays item info including file contents. Example: `npm exec --no -- shadcn view @shadcn/button`.

### `docs` — Get component documentation URLs

```bash
npm exec --no -- shadcn docs <components...> [options]
```

Outputs resolved URLs for component documentation, examples, and API references. Accepts one or more component names. Fetch the URLs to get the actual content.

Example output for `npm exec --no -- shadcn docs input button`:

```
base  radix

input
  docs      https://ui.shadcn.com/docs/components/radix/input
  examples  https://raw.githubusercontent.com/.../examples/input-example.tsx

button
  docs      https://ui.shadcn.com/docs/components/radix/button
  examples  https://raw.githubusercontent.com/.../examples/button-example.tsx
```

Some components include an `api` link to the underlying library (e.g. `cmdk` for the command component).

### `diff` — Check for updates

Do not use this command. Use `npm exec --no -- shadcn add --diff` instead.

### `info` — Project information

```bash
npm exec --no -- shadcn info [options]
```

Displays project info and `components.json` configuration. Run this first to discover the project's framework, aliases, Tailwind version, and resolved paths.

| Flag          | Short | Description       | Default |
| ------------- | ----- | ----------------- | ------- |
| `--cwd <cwd>` | `-c`  | Working directory | current |

**Project Info fields:**

| Field                | Type      | Meaning                                                            |
| -------------------- | --------- | ------------------------------------------------------------------ |
| `framework`          | `string`  | Detected framework (`next`, `vite`, `react-router`, `start`, etc.) |
| `frameworkVersion`   | `string`  | Framework version (e.g. `15.2.4`)                                  |
| `isSrcDir`           | `boolean` | Whether the project uses a `src/` directory                        |
| `isRSC`              | `boolean` | Whether React Server Components are enabled                        |
| `isTsx`              | `boolean` | Whether the project uses TypeScript                                |
| `tailwindVersion`    | `string`  | `"v3"` or `"v4"`                                                   |
| `tailwindConfigFile` | `string`  | Path to the Tailwind config file                                   |
| `tailwindCssFile`    | `string`  | Path to the global CSS file                                        |
| `aliasPrefix`        | `string`  | Import alias prefix (e.g. `@`, `~`, `@/`)                          |
| `packageManager`     | `string`  | Detected package manager (`npm`, `pnpm`, `yarn`, `bun`)            |

**Components.json fields:**

| Field                | Type      | Meaning                                                                                    |
| -------------------- | --------- | ------------------------------------------------------------------------------------------ |
| `base`               | `string`  | Primitive library (`radix` or `base`) — determines component APIs and available props      |
| `style`              | `string`  | Visual style (e.g. `nova`, `vega`)                                                         |
| `rsc`                | `boolean` | RSC flag from config                                                                       |
| `tsx`                | `boolean` | TypeScript flag                                                                            |
| `tailwind.config`    | `string`  | Tailwind config path                                                                       |
| `tailwind.css`       | `string`  | Global CSS path — this is where custom CSS variables go                                    |
| `iconLibrary`        | `string`  | Icon library — determines icon import package (e.g. `lucide-react`, `@tabler/icons-react`) |
| `aliases.components` | `string`  | Component import alias (e.g. `@/components`)                                               |
| `aliases.utils`      | `string`  | Utils import alias (e.g. `@/lib/utils`)                                                    |
| `aliases.ui`         | `string`  | UI component alias (e.g. `@/components/ui`)                                                |
| `aliases.lib`        | `string`  | Lib alias (e.g. `@/lib`)                                                                   |
| `aliases.hooks`      | `string`  | Hooks alias (e.g. `@/hooks`)                                                               |
| `resolvedPaths`      | `object`  | Absolute file-system paths for each alias                                                  |
| `registries`         | `object`  | Configured custom registries                                                               |

**Links fields:**

The `info` output includes a **Links** section with templated URLs for component docs, source, and examples. For resolved URLs, use `npm exec --no -- shadcn docs <component>` instead.
