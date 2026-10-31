import { readdirSync } from 'node:fs'
import path from 'node:path'
import { defineConfig, globalIgnores } from 'eslint/config'
import nextVitals from 'eslint-config-next/core-web-vitals'
import nextTs from 'eslint-config-next/typescript'

// The modules of DEC-118 (1), read from the disk so that a new module is covered without editing this file:
// every folder src/features/<area>/<module>.
const folders = (dir) =>
  readdirSync(dir, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
const FEATURES = path.join(import.meta.dirname, 'src/features')
const MODULES = folders(FEATURES).flatMap((area) =>
  folders(path.join(FEATURES, area)).map((module) => `${area}/${module}`),
)
const indexOf = (module) => `./${module}/index.ts`
// Everything else in src/ is shared: its folders (components, hooks, lib, utils, i18n, test) and its root files.
const SHARED = readdirSync(path.join(import.meta.dirname, 'src'))
  .filter((name) => name !== 'app' && name !== 'features')
  .map((name) => `src/${name}`)
// Plain paths, never globs: the rule then compares with path.relative, which ignores the case of a Windows drive
// letter, where a glob would silently match nothing. The imports must resolve, so ESLint runs from web/ (the
// resolver reads tsconfig.json from the cwd).
const BOUNDARIES = {
  basePath: import.meta.dirname,
  zones: [
    {
      target: 'src/app',
      from: 'src/features',
      except: MODULES.map(indexOf),
      message: 'app imports a feature module only through its index.ts (DEC-118 (2)).',
    },
    {
      target: 'src/features',
      from: 'src/app',
      message: 'A feature never imports app (DEC-118 (2)).',
    },
    ...MODULES.map((module) => ({
      target: `src/features/${module}`,
      from: 'src/features',
      except: [`./${module}`, ...MODULES.filter((other) => other !== module).map(indexOf)],
      message: 'A module imports another module only through its index.ts (DEC-118 (2)).',
    })),
    {
      target: SHARED,
      from: ['src/features', 'src/app'],
      message: 'Shared folders import neither features nor app (DEC-118 (2)).',
    },
  ],
}

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
  // Every text the user sees goes through next-intl, in the shadcn components too (web/CLAUDE.md, DEC-104 (6),
  // DEC-116 (2)); tests write the texts they expect.
  {
    files: ['src/**/*.tsx'],
    ignores: ['src/**/*.test.tsx'],
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
  { files: ['src/**/*.{ts,tsx}'], rules: { 'import/no-restricted-paths': ['error', BOUNDARIES] } },
  // A warning, not an error, so that the files written before DEC-118 (3) keep lint green; code-reviewer checks it.
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: [
      'src/**/*.test.{ts,tsx}',
      'src/components/ui/**',
      'src/lib/api/schema.d.ts',
      'src/lib/api/generated/**',
    ],
    rules: { 'max-lines': ['warn', { max: 150, skipBlankLines: true, skipComments: true }] },
  },
  // The defaults of eslint-config-next, plus generated and test output.
  globalIgnores([
    '.next/**',
    'out/**',
    'build/**',
    'next-env.d.ts',
    'src/lib/api/schema.d.ts',
    'src/lib/api/generated/**',
    'playwright-report/**',
    'test-results/**',
  ]),
])

export default eslintConfig
