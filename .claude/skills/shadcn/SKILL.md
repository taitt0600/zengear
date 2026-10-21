---
name: shadcn
description: Use when adding, searching or composing shadcn/ui components in web/ (components.json, web/src/components/ui/), or styling and fixing UI built from them.
user-invocable: false
---

# shadcn/ui

## Project rules (override everything below)

ZenGear rules, DEC-104 (5), DEC-111 (2), (5). Where this file or the files it links say otherwise, these win.

- Run the CLI only as `npm exec --no -- shadcn <command>` from `web/` (the version pinned in `web/package.json`;
  `--no` fails instead of downloading). Never `npx shadcn@latest`, `pnpm dlx` or `bunx`.
- `web/src/components/ui/` is written only by `npm exec --no -- shadcn add <item>` run from `web/`, then
  `python tools/shadcn_lock.py write` (`python3` off Windows) from the repository root. Never edit a file there by
  hand, not even to fix an import, an icon or a variant: a problem goes in the report, and a new variant or behaviour
  is a wrapper (see `web/CLAUDE.md`).
- No `--overwrite` and no `apply` while doing a feature: an update of a component is a `chore` pull request of its
  own that the owner merges. `--dry-run`, `--diff` and `--view` change nothing and may be used.
- Only items of the official registry (`@shadcn`, the default), in the style and base of `web/components.json`
  (radix).
- Packages, before `add` (DEC-111 (5)): read the item's `dependencies`, and those of each item in its
  `registryDependencies`, with `npm exec --no -- shadcn view @shadcn/<item>` and compare them with
  `web/approved-packages.json`. Both print them (measured with shadcn 4.22.1 in web-shell): `view` shows
  `dependencies`, `devDependencies` and `registryDependencies`, and `npm exec --no -- shadcn add <item> --dry-run`
  (which writes nothing) lists "Dependencies" and "Dev Dependencies" with their names. The check after `add`
  (workflow, step 7) stays: when it finds a package the file does not list, undo the `add` (the files it wrote and
  its lock entries) before the approval path. When all are listed, add the item. When the official item needs a
  package the file does not list, it goes through the package approval path of `/implement-feature`, step 4, before
  `add`. A package that a Decision ruled out (the date library
  of DEC-104 (1), such as `date-fns` for `Calendar`) or that no official installer adds (another registry, a URL, a
  GitHub item) needs a Decision of its own, drafted with `/decision-draft`.
- The rest (wrappers in `web/src/components/app/` or `web/src/features/<area>/`, theming in
  `web/src/app/globals.css` and `web/components.json`, `lucide-react` icons, next-intl for every text) is in
  `web/CLAUDE.md`.

## Source

Copied from https://github.com/shadcn-ui/ui/tree/ba1503c64472b82256d9de95e2e62c5901a38e8b/skills/shadcn
(commit `ba1503c64472b82256d9de95e2e62c5901a38e8b`, 2026-10-19), MIT License, Copyright (c) 2023 shadcn: see
`LICENSE.md`. Copied: `SKILL.md`, `cli.md`, `customization.md`, `rules/composition.md`, `rules/forms.md`,
`rules/icons.md`, `rules/styling.md`. Not copied: `scripts/`, `agents/`, `evals/`, `assets/`, `mcp.md`,
`registry.md`, `design-system.md`, `design-system-page.md`, `rules/chat.md`, `rules/base-vs-radix.md`.

Modified for ZenGear (2026-10-21):

- frontmatter: removed `allowed-tools`; narrowed the description; kept `name` and `user-invocable: false`;
- removed the dynamic context injection (an exclamation mark before a code span, which ran `shadcn info` through
  npx each time the skill loaded), replaced by the plain instruction under *Project Context*;
- added the two sections above; every `npx shadcn@latest` became `npm exec --no -- shadcn`;
- `SKILL.md`: removed presets and `apply`, community and GitHub registries, design systems, chat components, the
  base-ui variants, the smart merge of local changes and editing component source; the workflow checks packages
  before `add`; added the notes on packages under *Component Selection*;
- `cli.md`: removed `apply`, `build`, templates, monorepo, presets beyond the one-time `init` of web-shell, the
  `init` flags that overwrite or scaffold, and the `add` rows `--overwrite`, `--all`, `--path`;
- `customization.md`: removed presets and `apply`, the Tailwind v3 registration, the radius link to the design
  system and adding a variant to component source; added the note on dark mode;
- links to files not copied removed;
- after web-shell: the `init` section of `cli.md` says what web-shell did instead, and the package check before
  `add` gives what `view` and `add --dry-run` print.

---

A framework for building ui, components and design systems. Components are added as source code to the user's project via the CLI.

> **IMPORTANT:** Run all CLI commands as `npm exec --no -- shadcn` from `web/`.

## Project Context

Run `npm exec --no -- shadcn info` from `web/` to get the project config and installed components. Use `npm exec --no -- shadcn docs <component>` to get documentation and example URLs for any component.

## Principles

1. **Use existing components first.** Use `npm exec --no -- shadcn search @shadcn` to check the official registry before writing custom UI.
2. **Compose, don't reinvent.** Settings page = Tabs + Card + form controls. Dashboard = Sidebar + Card + Chart + Table.
3. **Use built-in variants before custom styles.** `variant="outline"`, `size="sm"`, etc.
4. **Use semantic colors.** `bg-primary`, `text-muted-foreground` — never raw values like `bg-blue-500`.

## Critical Rules

These rules are **always enforced**. Each links to a file with Incorrect/Correct code pairs.

### Styling & Tailwind → [styling.md](./rules/styling.md)

- **`className` for layout, not styling.** Never override component colors or typography.
- **No `space-x-*` or `space-y-*`.** Use `flex` with `gap-*`. For vertical stacks, `flex flex-col gap-*`.
- **Use `size-*` when width and height are equal.** `size-10` not `w-10 h-10`.
- **Use `truncate` shorthand.** Not `overflow-hidden text-ellipsis whitespace-nowrap`.
- **No manual `dark:` color overrides.** Use semantic tokens (`bg-background`, `text-muted-foreground`).
- **Use `cn()` for conditional classes.** Don't write manual template literal ternaries.
- **No manual `z-index` on overlay components.** Dialog, Sheet, Popover, etc. handle their own stacking.

### Forms & Inputs → [forms.md](./rules/forms.md)

- **Forms use `FieldGroup` + `Field`.** Never use raw `div` with `space-y-*` or `grid gap-*` for form layout.
- **`InputGroup` uses `InputGroupInput`/`InputGroupTextarea`.** Never raw `Input`/`Textarea` inside `InputGroup`.
- **Buttons inside inputs use `InputGroup` + `InputGroupAddon`.**
- **Option sets (2–7 choices) use `ToggleGroup`.** Don't loop `Button` with manual active state.
- **`FieldSet` + `FieldLegend` for grouping related checkboxes/radios.** Don't use a `div` with a heading.
- **Field validation uses `data-invalid` + `aria-invalid`.** `data-invalid` on `Field`, `aria-invalid` on the control. For disabled: `data-disabled` on `Field`, `disabled` on the control.

### Component Structure → [composition.md](./rules/composition.md)

- **Items always inside their Group.** `SelectItem` → `SelectGroup`. `DropdownMenuItem` → `DropdownMenuGroup`. `CommandItem` → `CommandGroup`.
- **Use `asChild` for custom triggers.** The project's base is radix.
- **Dialog, Sheet, and Drawer always need a Title.** `DialogTitle`, `SheetTitle`, `DrawerTitle` required for accessibility. Use `className="sr-only"` if visually hidden.
- **Use full Card composition.** `CardHeader`/`CardTitle`/`CardDescription`/`CardContent`/`CardFooter`. Don't dump everything in `CardContent`.
- **Button has no `isPending`/`isLoading`.** Compose with `Spinner` + `data-icon` + `disabled`.
- **`TabsTrigger` must be inside `TabsList`.** Never render triggers directly in `Tabs`.
- **`Avatar` always needs `AvatarFallback`.** For when the image fails to load.

### Use Components, Not Custom Markup → [composition.md](./rules/composition.md)

- **Use existing components before custom markup.** Check if a component exists before writing a styled `div`.
- **Callouts use `Alert`.** Don't build custom styled divs.
- **Empty states use `Empty`.** Don't build custom empty state markup.
- **Toast uses `toast()` from `sonner`** (radix base).
- **Use `Separator`** instead of `<hr>` or `<div className="border-t">`.
- **Use `Skeleton`** for loading placeholders. No custom `animate-pulse` divs.
- **Use `Badge`** instead of custom styled spans.

### Icons → [icons.md](./rules/icons.md)

- **Icons in `Button` use `data-icon`.** `data-icon="inline-start"` or `data-icon="inline-end"` on the icon.
- **No sizing classes on icons inside components.** Components handle icon sizing via CSS. No `size-4` or `w-4 h-4`.
- **Pass icons as objects, not string keys.** `icon={CheckIcon}`, not a string lookup.

## Key Patterns

These are the most common patterns that differentiate correct shadcn/ui code. For edge cases, see the linked rule files above.

```tsx
// Form layout: FieldGroup + Field, not div + Label.
<FieldGroup>
  <Field>
    <FieldLabel htmlFor="email">Email</FieldLabel>
    <Input id="email" />
  </Field>
</FieldGroup>

// Validation: data-invalid on Field, aria-invalid on the control.
<Field data-invalid>
  <FieldLabel>Email</FieldLabel>
  <Input aria-invalid />
  <FieldDescription>Invalid email.</FieldDescription>
</Field>

// Icons in buttons: data-icon, no sizing classes.
<Button>
  <SearchIcon data-icon="inline-start" />
  Search
</Button>

// Spacing: gap-*, not space-y-*.
<div className="flex flex-col gap-4">  // correct
<div className="space-y-4">           // wrong

// Equal dimensions: size-*, not w-* h-*.
<Avatar className="size-10">   // correct
<Avatar className="w-10 h-10"> // wrong

// Status colors: Badge variants or semantic tokens, not raw colors.
<Badge variant="secondary">+20.1%</Badge>    // correct
<span className="text-emerald-600">+20.1%</span> // wrong
```

## Component Selection

| Need                       | Use                                                                                                 |
| -------------------------- | --------------------------------------------------------------------------------------------------- |
| Button/action              | `Button` with appropriate variant                                                                   |
| Form inputs                | `Input`, `Select`, `Combobox`, `Switch`, `Checkbox`, `RadioGroup`, `Textarea`, `InputOTP`, `Slider` |
| Toggle between 2–5 options | `ToggleGroup` + `ToggleGroupItem`                                                                   |
| Data display               | `Table`, `Card`, `Badge`, `Avatar`                                                                  |
| Navigation                 | `Sidebar`, `NavigationMenu`, `Breadcrumb`, `Tabs`, `Pagination`                                     |
| Overlays                   | `Dialog` (modal), `Sheet` (side panel), `Drawer` (bottom sheet), `AlertDialog` (confirmation)       |
| Feedback                   | `sonner`, `Alert`, `Progress`, `Skeleton`, `Spinner`                                                |
| Command palette            | `Command` inside `Dialog`                                                                           |
| Charts                     | `Chart` (wraps Recharts)                                                                            |
| Layout                     | `Card`, `Separator`, `Resizable`, `ScrollArea`, `Accordion`, `Collapsible`                          |
| Empty states               | `Empty`                                                                                             |
| Menus                      | `DropdownMenu`, `ContextMenu`, `Menubar`                                                            |
| Tooltips/info              | `Tooltip`, `HoverCard`, `Popover`                                                                   |

Before adding a component, check its packages as the project rules say: `Calendar`, for example, needs a date
library that DEC-104 (1) rules out, so it needs a Decision of its own.

## Key Fields

The output of `npm exec --no -- shadcn info` contains these key fields:

- **`aliases`** → use the actual alias prefix for imports (e.g. `@/`, `~/`), never hardcode.
- **`isRSC`** → when `true`, components using `useState`, `useEffect`, event handlers, or browser APIs need `"use client"` at the top of the file. Always reference this field when advising on the directive.
- **`tailwindVersion`** → `"v4"` uses `@theme inline` blocks; `"v3"` uses `tailwind.config.js`.
- **`tailwindCssFile`** → the global CSS file where custom CSS variables are defined. Always edit this file, never create a new one.
- **`style`** → component visual treatment (e.g. `nova`, `vega`).
- **`base`** → primitive library (`radix` or `base`). Affects component APIs and available props.
- **`iconLibrary`** → determines icon imports. Use `lucide-react` for `lucide`, `@tabler/icons-react` for `tabler`, etc. Never assume `lucide-react`.
- **`resolvedPaths`** → exact file-system destinations for components, utils, hooks, etc.
- **`framework`** → routing and file conventions (e.g. Next.js App Router vs Vite SPA).
- **`packageManager`** → npm in this project. A package the component does not install itself is not added here:
  see the project rules above.

See [cli.md — `info` command](./cli.md) for the full field reference.

## Component Docs, Examples, and Usage

Run `npm exec --no -- shadcn docs <component>` from `web/` to get the URLs for a component's documentation, examples, and API reference. Fetch these URLs to get the actual content.

```bash
npm exec --no -- shadcn docs button dialog select
```

**When creating, fixing, debugging, or using a component, always run `npm exec --no -- shadcn docs` and fetch the URLs first.** This ensures you're working with the correct API and usage patterns rather than guessing.

## Workflow

1. **Get project context** — run `npm exec --no -- shadcn info` from `web/`.
2. **Check installed components first** — before running `add`, always check the `components` list from project context or list the `resolvedPaths.ui` directory. Don't import components that haven't been added, and don't re-add ones already installed.
3. **Find components** — `npm exec --no -- shadcn search @shadcn -q "<query>"`.
4. **Get docs and examples** — run `npm exec --no -- shadcn docs <component>` to get URLs, then fetch them. Use `npm exec --no -- shadcn view @shadcn/<item>` to browse registry items you haven't installed.
5. **Check packages first** — read the item's `dependencies` and `registryDependencies` with `npm exec --no -- shadcn view @shadcn/<item>` (or `add <item> --dry-run`, as the project rules say); compare them with `web/approved-packages.json` and follow the project rules above before anything is added.
6. **Install** — `npm exec --no -- shadcn add <item>`, then `python tools/shadcn_lock.py write` from the repository root. Updating an installed component is not part of a feature (project rules above).
7. **Review added components** — After adding a component, **always read the added files** and check again the packages it installed against `web/approved-packages.json`. Never edit the added files: a problem in them goes in the report, and composition, imports and icons are fixed in your own code that uses them, following the [Critical Rules](#critical-rules).
8. **Registry is the official one** — When the user asks for a block or component that `@shadcn` does not have, say so; do not pick another registry.

## Checking Upstream Changes

To see how an installed component differs from the registry without changing anything, use
`npm exec --no -- shadcn add <component> --dry-run` and `--diff <file>`. The update itself is a `chore` pull
request of its own, never part of a feature. **NEVER fetch a component's source from GitHub manually to compare
or install it — always use the CLI.** The documentation and example URLs that `docs` prints may be fetched to read.

## Quick Reference

Run from `web/`:

```bash
# Add components.
npm exec --no -- shadcn add button card dialog

# Preview changes before adding.
npm exec --no -- shadcn add button --dry-run
npm exec --no -- shadcn add button --diff button.tsx

# Search the official registry.
npm exec --no -- shadcn search @shadcn -q "sidebar"
npm exec --no -- shadcn search @shadcn -q "menu" -t ui  # filter by item type

# Get component docs and example URLs.
npm exec --no -- shadcn docs button dialog select

# View registry item details (for items not yet installed).
npm exec --no -- shadcn view @shadcn/button
```

## Detailed References

- [rules/forms.md](./rules/forms.md) — FieldGroup, Field, InputGroup, ToggleGroup, FieldSet, validation states
- [rules/composition.md](./rules/composition.md) — Groups, overlays, Card, Tabs, Avatar, Alert, Empty, Toast, Separator, Skeleton, Badge, Button loading
- [rules/icons.md](./rules/icons.md) — data-icon, icon sizing, passing icons as objects
- [rules/styling.md](./rules/styling.md) — Semantic colors, variants, className, spacing, size, truncate, dark mode, cn(), z-index
- [cli.md](./cli.md) — Commands and flags
- [customization.md](./customization.md) — Theming, CSS variables, wrapper components
