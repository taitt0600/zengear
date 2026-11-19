import { readdirSync } from 'node:fs'
import path from 'node:path'
import { defineConfig, globalIgnores } from 'eslint/config'
import nextVitals from 'eslint-config-next/core-web-vitals'
import nextTs from 'eslint-config-next/typescript'
import betterTailwindcss from 'eslint-plugin-better-tailwindcss'

// The plugins eslint-config-next registers, taken from its own configs: their recommended sets are added without a
// package of their own and without registering a plugin twice (DEC-152 (7)).
const pluginOf = (configs, name) => configs.find((config) => config.plugins?.[name]).plugins[name]
const jsxA11y = pluginOf(nextVitals, 'jsx-a11y')
const typescriptEslint = pluginOf(nextTs, '@typescript-eslint')
// The hook eslint-check.ps1 sets it when the typed rules make it too slow (DEC-152 (7)); CI never sets it. Without
// the typed rules, their disable directives would count as unused, so that report is off too.
const TYPED = process.env.ZENGEAR_ESLINT_UNTYPED !== '1'
// The code the size and complexity rules judge: tests, the shadcn components and the generated client excepted.
const CODE = {
  files: ['src/**/*.{ts,tsx}'],
  ignores: ['src/**/*.test.{ts,tsx}', 'src/components/ui/**', 'src/lib/api/generated/**'],
}
// Colours of Tailwind's default palette and arbitrary colour values: a variant uses the theme's tokens of
// src/app/globals.css (DEC-116 (1), DEC-152 (9)). Variants and an opacity modifier are matched too.
const COLOUR_UTILITY =
  '(bg|text|border(-[xytrblse])?|ring|ring-offset|outline|fill|stroke|decoration|divide|from|via|to|shadow|accent|caret|placeholder|inset-shadow|inset-ring)'
const PALETTE =
  '(slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose|mauve|olive|mist|taupe)-[0-9]{2,3}'
const RESTRICTED_COLOURS = [
  {
    pattern: `^([a-zA-Z0-9:/_\\[\\]-]*:)?!?-?${COLOUR_UTILITY}-${PALETTE}(\\/[0-9]{1,3})?!?$`,
    message:
      'A colour of the default palette: use a token of the theme (src/app/globals.css, DEC-116 (1)).',
  },
  {
    pattern: `^([a-zA-Z0-9:/_\\[\\]-]*:)?!?${COLOUR_UTILITY}-\\[(#|rgb|hsl|oklch|oklab|lab|lch|color)[^\\]]*\\](\\/[0-9]{1,3})?!?$`,
    message: 'An arbitrary colour: use a token of the theme (src/app/globals.css, DEC-116 (1)).',
  },
]

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
  // A disable directive that hides nothing is an error (DEC-152 (7)).
  { linterOptions: { reportUnusedDisableDirectives: TYPED ? 'error' : 'off' } },
  // The recommended set of jsx-a11y, beyond the few rules eslint-config-next turns on (DEC-152 (3), DEC-104 (7)).
  { files: ['src/**/*.{ts,tsx}'], rules: jsxA11y.flatConfigs.recommended.rules },
  // Typed linting: the type-checked rules of typescript-eslint's recommended set (DEC-152 (7)).
  ...(TYPED
    ? [
        {
          files: ['src/**/*.{ts,tsx}'],
          languageOptions: {
            parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname },
          },
          rules: typescriptEslint.configs['recommended-type-checked-only'].rules,
        },
        // Tests stub with vi.fn() (typed any), fake async APIs without awaiting, patch jsdom's prototypes and reject
        // with the error shapes Firebase throws (not Error): these rules judge that on purpose and stay off there;
        // the rules on promises (no-floating-promises, no-misused-promises, await-thenable) still run.
        {
          files: ['src/**/*.test.{ts,tsx}', 'src/**/*.fixtures.{ts,tsx}', 'src/test/**'],
          rules: {
            '@typescript-eslint/no-unsafe-argument': 'off',
            '@typescript-eslint/no-unsafe-assignment': 'off',
            '@typescript-eslint/no-unsafe-call': 'off',
            '@typescript-eslint/no-unsafe-member-access': 'off',
            '@typescript-eslint/no-unsafe-return': 'off',
            '@typescript-eslint/require-await': 'off',
            '@typescript-eslint/unbound-method': 'off',
            '@typescript-eslint/prefer-promise-reject-errors': 'off',
          },
        },
      ]
    : []),
  // The classes of Tailwind: known to the theme, without conflicts, and colours from its tokens (DEC-152 (3)).
  {
    files: ['src/**/*.tsx'],
    ignores: ['src/**/*.test.tsx'],
    plugins: { 'better-tailwindcss': betterTailwindcss },
    settings: { 'better-tailwindcss': { entryPoint: 'src/app/globals.css' } },
    rules: {
      // toaster: the class sonner's own stylesheet styles, not a Tailwind class (src/components/ui/sonner.tsx).
      'better-tailwindcss/no-unknown-classes': ['error', { ignore: ['^toaster$'] }],
      'better-tailwindcss/no-conflicting-classes': 'error',
      'better-tailwindcss/no-restricted-classes': ['error', { restrict: RESTRICTED_COLOURS }],
    },
  },
  // Every text the user sees goes through next-intl, in the shadcn components too (web/AGENTS.md, DEC-104 (6),
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
  // Size and complexity, errors of the first tier (DEC-118 (3), DEC-152 (7)): at most 150 lines of code per file;
  // complexity 15 and depth 3, set over the code measured on 2026-11-19 (highest 13 and 3 once useStaffList was split).
  {
    ...CODE,
    rules: {
      'max-lines': ['error', { max: 150, skipBlankLines: true, skipComments: true }],
      complexity: ['error', 15],
      'max-depth': ['error', 3],
    },
  },
  // The defaults of eslint-config-next, plus generated and test output.
  globalIgnores([
    '.next/**',
    'out/**',
    'build/**',
    'next-env.d.ts',
    'src/lib/api/generated/**',
    'playwright-report/**',
    'test-results/**',
  ]),
])

export default eslintConfig
