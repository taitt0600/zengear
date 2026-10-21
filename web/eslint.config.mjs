import { defineConfig, globalIgnores } from 'eslint/config'
import nextVitals from 'eslint-config-next/core-web-vitals'
import nextTs from 'eslint-config-next/typescript'

// Attributes whose value the user reads or hears: their text comes from the message files too (DEC-104 (6)).
const TEXT_ATTRIBUTE =
  'JSXAttribute[name.name=/^(alt|aria-description|aria-label|aria-placeholder|aria-roledescription|aria-valuetext|label|placeholder|title)$/]'
// A string with a visible character: alt="" (a decorative image) and title={null} stay allowed.
const TEXT = 'Literal[value=/\\S/]'
const TEMPLATE_TEXT = 'TemplateLiteral:has(TemplateElement[value.raw=/\\S/])'
// Where a text hides inside an expression: {open ? 'Đóng' : 'Mở'}, {error && 'Lỗi'}, {'Tổng: ' + n}. A string as
// the argument of a call (t('key')) is never matched.
const IN_EXPRESSION = [
  `ConditionalExpression > ${TEXT}`,
  `LogicalExpression > ${TEXT}`,
  `BinaryExpression[operator='+'] > ${TEXT}`,
]
const IN_CHILD = ':matches(JSXElement, JSXFragment) > JSXExpressionContainer'
// react/jsx-no-literals refuses a string written directly as a child; these catch the rest.
const TEXT_SELECTORS = [
  `${TEXT_ATTRIBUTE} > ${TEXT}`,
  ...[TEXT, TEMPLATE_TEXT, ...IN_EXPRESSION].map(
    (value) => `${TEXT_ATTRIBUTE} > JSXExpressionContainer > ${value}`,
  ),
  ...[TEMPLATE_TEXT, ...IN_EXPRESSION].map((value) => `${IN_CHILD} > ${value}`),
]

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  // Every text the user sees goes through next-intl (web/CLAUDE.md, DEC-104 (6)). The generated shadcn components
  // are left as written (DEC-104 (5)), and tests write the texts they expect.
  {
    files: ['src/**/*.tsx'],
    ignores: ['src/components/ui/**', 'src/**/*.test.tsx'],
    rules: {
      'react/jsx-no-literals': ['error', { noStrings: true, ignoreProps: true }],
      'no-restricted-syntax': [
        'error',
        ...TEXT_SELECTORS.map((selector) => ({
          selector,
          message: 'Text the user reads comes from messages/vi.json through next-intl (t("key")).',
        })),
      ],
    },
  },
  // The defaults of eslint-config-next, plus generated and test output.
  globalIgnores([
    '.next/**',
    'out/**',
    'build/**',
    'next-env.d.ts',
    'src/lib/api/schema.d.ts',
    'playwright-report/**',
    'test-results/**',
  ]),
])

export default eslintConfig
