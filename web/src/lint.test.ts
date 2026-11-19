// @vitest-environment node
import { fileURLToPath } from 'node:url'
import { ESLint, type Linter } from 'eslint'
import nextTs from 'eslint-config-next/typescript'
import { beforeAll, describe, expect, it } from 'vitest'

// The snippets below are not files of the TypeScript project, which the typed rules need: those rules stay off here,
// through the config typescript-eslint gives for that, and every other rule of eslint.config.mjs runs.
const typescriptEslint = nextTs.find((config) => config.plugins?.['@typescript-eslint'])?.plugins?.[
  '@typescript-eslint'
] as { configs: Record<string, Linter.Config> }
const eslint = new ESLint({
  cwd: fileURLToPath(new URL('..', import.meta.url)),
  overrideConfig: typescriptEslint.configs['flat/disable-type-checked'],
})

// The first lint loads the config, its plugins and the TypeScript parser: seconds while the other test files run
// beside this one, more than the 5 s of a test. It runs once here, with a time of its own.
beforeAll(async () => {
  await eslint.lintText('', { filePath: 'src/features/sample/sample.tsx' })
}, 60_000)

// Only the two rules of the text check: the snippets break others (unused declarations, <img>) on purpose.
const TEXT_RULES = ['react/jsx-no-literals', 'no-restricted-syntax']

async function ruleIds(
  code: string,
  filePath = 'src/features/sample/sample.tsx',
  rules = TEXT_RULES,
) {
  const [result] = await eslint.lintText(code, { filePath })
  // A snippet ESLint cannot parse would otherwise "pass" with no message of these rules.
  expect(result.fatalErrorCount).toBe(0)
  return result.messages
    .map((message) => message.ruleId)
    .filter((ruleId) => ruleId && rules.includes(ruleId))
}

const component = (jsx: string) =>
  `declare const open: boolean\ndeclare const n: number\ndeclare function t(key: string): string\n` +
  `export function A() { return ${jsx} }`

describe('text in JSX', () => {
  it.each([
    ['a child', '<p>Xin chào</p>', 'react/jsx-no-literals'],
    ['a string child', `<p>{'Xin chào'}</p>`, 'react/jsx-no-literals'],
    ['a template child', '<p>{`Xin chào`}</p>', 'no-restricted-syntax'],
    ['a child in a condition', `<p>{open ? 'Đóng' : t('a')}</p>`, 'no-restricted-syntax'],
    ['a child after &&', `<p>{open && 'Lỗi'}</p>`, 'no-restricted-syntax'],
    ['a child joined with +', `<p>{'Tổng: ' + n}</p>`, 'no-restricted-syntax'],
    ['an aria-label', '<button aria-label="Đóng" />', 'no-restricted-syntax'],
    [
      'an aria-label in a condition',
      `<button aria-label={open ? 'Đóng' : 'Mở'} />`,
      'no-restricted-syntax',
    ],
    ['a placeholder in braces', `<input placeholder={'Email'} />`, 'no-restricted-syntax'],
    ['a title as a template', '<p title={`Tên`} />', 'no-restricted-syntax'],
    ['an alt', '<img alt="Logo" src="/a.png" />', 'no-restricted-syntax'],
    [
      'an aria-valuetext',
      '<div role="slider" aria-valuenow={1} aria-valuetext="Một" />',
      'no-restricted-syntax',
    ],
    ['a label', '<option label="Một" value="1" />', 'no-restricted-syntax'],
  ])('refuses %s', async (_, jsx, ruleId) => {
    expect(await ruleIds(component(jsx))).toContain(ruleId)
  })

  it.each([
    ['texts from next-intl', `<p className="text-sm" title={t('a.title')}>{t('a.text')}</p>`],
    ['the space Prettier writes when it wraps JSX', `<p>{t('a')}{' '}{t('b')}</p>`],
    ['an empty alt of a decorative image', '<img alt="" src="/a.png" />'],
    ['props that are not text', `<li key={'row-' + n} className={open ? 'block' : 'hidden'} />`],
  ])('accepts %s', async (_, jsx) => {
    expect(await ruleIds(component(jsx))).toEqual([])
  })

  it.each([
    ['a child', '<span className="sr-only">Close</span>', 'react/jsx-no-literals'],
    ['an aria-label', '<button aria-label="Close" />', 'no-restricted-syntax'],
  ])('refuses %s in the shadcn components (DEC-116 (2))', async (_, jsx, ruleId) => {
    expect(await ruleIds(component(jsx), 'src/components/ui/sample.tsx')).toContain(ruleId)
  })

  it('does not check tests', async () => {
    expect(await ruleIds(component('<p>Xin chào</p>'), 'src/sample.test.tsx')).toEqual([])
  })
})

// DEC-118 (2). The snippets sit in the real folders and import real files: an import that does not resolve is not
// judged, and the modules are those of src/features.
describe('import boundaries (DEC-118 (2))', () => {
  const BOUNDARY = ['import/no-restricted-paths']
  const importing = (from: string) => `import * as m from '${from}'\nexport const n = m\n`
  const INTERNAL = '@/features/identity/access/utils/routes'
  const INDEX = '@/features/identity/access'

  it.each([
    ['app, the inside of a module', 'src/app/sample/page.tsx', INTERNAL],
    ['a feature, app', 'src/features/identity/access/sample.ts', '@/app/layout'],
    ['a module, the inside of another module', 'src/features/shell/admin/sample.ts', INTERNAL],
    ['a shared folder, a feature', 'src/hooks/sample.ts', INDEX],
    ['a shared folder, app', 'src/lib/sample.ts', '@/app/layout'],
    ['a root file, a feature', 'src/proxy.ts', INDEX],
  ])('refuses that %s imports', async (_, filePath, from) => {
    expect(await ruleIds(importing(from), filePath, BOUNDARY)).toEqual(BOUNDARY)
  })

  it.each([
    ['app, a module through its index', 'src/app/sample/page.tsx', INDEX],
    ['a feature, a shared folder', 'src/features/identity/access/sample.ts', '@/lib/utils'],
    ['a module, another module through its index', 'src/features/shell/admin/sample.ts', INDEX],
    ['a module, its own files', 'src/features/identity/access/sample.ts', './utils/routes'],
    ['a shared folder, another shared folder', 'src/hooks/sample.ts', '@/lib/utils'],
  ])('accepts that %s imports', async (_, filePath, from) => {
    expect(await ruleIds(importing(from), filePath, BOUNDARY)).toEqual([])
  })
})

// The severities of one rule's messages in a snippet: 2 is an error.
async function severities(ruleId: string, code: string, filePath: string) {
  const [result] = await eslint.lintText(code, { filePath })
  expect(result.fatalErrorCount).toBe(0)
  return result.messages
    .filter((message) => message.ruleId === ruleId)
    .map((message) => message.severity)
}

// DEC-118 (3), DEC-152 (7): at most 150 lines of code per file, an error.
describe('file length (DEC-118 (3))', () => {
  const lines = (count: number) =>
    Array.from({ length: count }, (_, i) => `export const n${i} = ${i}`).join('\n') + '\n'

  const maxLines = (code: string, filePath = 'src/features/sample/sample.ts') =>
    severities('max-lines', code, filePath)

  it('refuses more than 150 lines of code', async () => {
    expect(await maxLines(lines(151))).toEqual([2])
  })

  it('counts neither blank lines nor comments', async () => {
    expect(await maxLines(lines(150) + '\n// a comment\n/* another\n   one */\n\n')).toEqual([])
  })

  it.each([
    ['the shadcn components', 'src/components/ui/sample.tsx'],
    ['tests', 'src/sample.test.ts'],
    ['the generated API client', 'src/lib/api/generated/me/me.ts'],
  ])('does not check %s', async (_, filePath) => {
    expect(await maxLines(lines(151), filePath)).toEqual([])
  })
})

// DEC-152 (7): complexity 15 and depth 3, errors, in the same files as max-lines.
describe('complexity and depth (DEC-152 (7))', () => {
  const FILE = 'src/features/sample/sample.ts'
  // A function whose complexity is 1 + the number of `if`s.
  const branches = (count: number) =>
    `export function f(n: number) {\n${Array.from({ length: count }, (_, i) => `  if (n === ${i}) return ${i}\n`).join('')}  return -1\n}\n`
  const nested = (depth: number) =>
    `export function f(n: number) {\n${'  if (n > 0) {\n'.repeat(depth)}  return n\n${'  }\n'.repeat(depth)}  return 0\n}\n`

  it('refuses a complexity above 15', async () => {
    expect(await severities('complexity', branches(15), FILE)).toEqual([2])
    expect(await severities('complexity', branches(14), FILE)).toEqual([])
  })

  it('refuses blocks nested deeper than 3', async () => {
    expect(await severities('max-depth', nested(4), FILE)).toEqual([2])
    expect(await severities('max-depth', nested(3), FILE)).toEqual([])
  })

  it('does not check tests', async () => {
    expect(await severities('complexity', branches(15), 'src/sample.test.ts')).toEqual([])
  })
})

// DEC-152 (7): a disable directive that hides nothing is an error.
describe('unused disable directives (DEC-152 (7))', () => {
  it('are refused', async () => {
    const [result] = await eslint.lintText(
      '// eslint-disable-next-line no-console\nexport const n = 1\n',
      { filePath: 'src/features/sample/sample.ts' },
    )
    expect(result.messages.map((message) => [message.ruleId, message.severity])).toContainEqual([
      null,
      2,
    ])
  })
})

// DEC-152 (3): the recommended set of jsx-a11y, beyond the rules of eslint-config-next.
describe('accessibility (DEC-152 (3))', () => {
  it('refuses a click handler on a div without a key handler', async () => {
    const code = component(`<div onClick={() => {}}>{t('a')}</div>`)
    expect(
      await severities(
        'jsx-a11y/click-events-have-key-events',
        code,
        'src/features/sample/sample.tsx',
      ),
    ).toEqual([2])
  })
})

// DEC-116 (1), DEC-152 (3): the classes of Tailwind are known to the theme, and colours come from its tokens.
describe('Tailwind classes (DEC-152 (3))', () => {
  const FILE = 'src/features/sample/sample.tsx'
  const withClass = (className: string) => component(`<p className="${className}">{t('a')}</p>`)
  const RESTRICTED = 'better-tailwindcss/no-restricted-classes'

  it.each([
    ['a colour of the default palette', 'bg-red-500'],
    ['the same with a variant and an opacity', 'hover:text-blue-600/50'],
    ['a border colour of one side', 'border-t-zinc-200'],
    ['an arbitrary colour', 'bg-[#ff0000]'],
  ])('refuses %s', async (_, className) => {
    expect(await severities(RESTRICTED, withClass(className), FILE)).toEqual([2])
  })

  it.each([
    ['tokens of the theme', 'bg-primary text-primary-foreground border-border'],
    ['a token with an opacity', 'bg-destructive/10'],
    ['classes that are not colours', 'mt-2 grid-cols-2 text-sm'],
  ])('accepts %s', async (_, className) => {
    expect(await severities(RESTRICTED, withClass(className), FILE)).toEqual([])
  })

  it('refuses a class the theme does not know', async () => {
    expect(
      await severities('better-tailwindcss/no-unknown-classes', withClass('bg-brand'), FILE),
    ).toEqual([2])
  })
})
