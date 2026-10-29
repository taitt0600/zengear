// @vitest-environment node
import { fileURLToPath } from 'node:url'
import { ESLint } from 'eslint'
import { describe, expect, it } from 'vitest'

// The rule of DEC-104 (6): no text written in JSX, in the children or in an attribute the user reads.
const eslint = new ESLint({ cwd: fileURLToPath(new URL('..', import.meta.url)) })

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

// DEC-118 (3): at most 150 lines of code per file, a warning.
describe('file length (DEC-118 (3))', () => {
  const lines = (count: number) =>
    Array.from({ length: count }, (_, i) => `export const n${i} = ${i}`).join('\n') + '\n'

  // The severities of the max-lines messages: 1 is a warning, which DEC-118 (3) chose over an error.
  async function maxLines(code: string, filePath = 'src/features/sample/sample.ts') {
    const [result] = await eslint.lintText(code, { filePath })
    expect(result.fatalErrorCount).toBe(0)
    return result.messages
      .filter((message) => message.ruleId === 'max-lines')
      .map((message) => message.severity)
  }

  it('warns above 150 lines of code', async () => {
    expect(await maxLines(lines(151))).toEqual([1])
  })

  it('counts neither blank lines nor comments', async () => {
    expect(await maxLines(lines(150) + '\n// a comment\n/* another\n   one */\n\n')).toEqual([])
  })

  it.each([
    ['the shadcn components', 'src/components/ui/sample.tsx'],
    ['tests', 'src/sample.test.ts'],
  ])('does not check %s', async (_, filePath) => {
    expect(await maxLines(lines(151), filePath)).toEqual([])
  })
})
