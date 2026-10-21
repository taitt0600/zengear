// @vitest-environment node
import { fileURLToPath } from 'node:url'
import { ESLint } from 'eslint'
import { describe, expect, it } from 'vitest'

// The rule of DEC-104 (6): no text written in JSX, in the children or in an attribute the user reads.
const eslint = new ESLint({ cwd: fileURLToPath(new URL('..', import.meta.url)) })

// Only the two rules of the text check: the snippets break others (unused declarations, <img>) on purpose.
const TEXT_RULES = ['react/jsx-no-literals', 'no-restricted-syntax']

async function ruleIds(code: string, filePath = 'src/features/sample/sample.tsx') {
  const [result] = await eslint.lintText(code, { filePath })
  // A snippet ESLint cannot parse would otherwise "pass" with no message of these rules.
  expect(result.fatalErrorCount).toBe(0)
  return result.messages
    .map((message) => message.ruleId)
    .filter((ruleId) => ruleId && TEXT_RULES.includes(ruleId))
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
    ['the generated shadcn components', 'src/components/ui/sample.tsx'],
    ['tests', 'src/sample.test.tsx'],
  ])('does not check %s', async (_, filePath) => {
    expect(await ruleIds(component('<p>Xin chào</p>'), filePath)).toEqual([])
  })
})
