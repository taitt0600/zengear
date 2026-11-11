// @vitest-environment node
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

// WCAG 2.2 AA (docs/FRONTEND.md) on both themes, measured on the colours the components draw (DEC-123 (4)): SC 1.4.11
// asks 3:1 for the border of a field and of its focus, SC 1.4.3 asks 4.5:1 for text. A colour with an opacity
// (`bg-destructive/10`, a token written `/ 35%`) is blended over the surface it sits on, as the browser blends (Layout
// of SCR-STF-03, Theme). In :root: the white text of the badge `destructive` on --destructive (measured against
// --background, the same white), the soft badge `success`, the button `destructive-soft` on --destructive at 10 % and
// 20 % over --background, and the focused destructive item of a menu on --destructive at 10 % over --popover (DEC-124
// (3)). In .dark, whose --background is no longer white, each text on its dark ground, the white text on
// --destructive at 60 % over the surface (button, badge), the alpha classes read from the components (DEC-124 (2),
// (3)). The margin absorbs the rounding of a render.
const MARGIN = 0.05

const css = readFileSync(new URL('./globals.css', import.meta.url), 'utf8')
const source = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8')

type Oklch = { l: number; c: number; h: number; alpha: number }

function tokensOf(selector: ':root' | '.dark') {
  const start = css.indexOf(`\n${selector} {`)
  const block = css.slice(start, css.indexOf('}', start))
  return new Map<string, Oklch>(
    [
      ...block.matchAll(/--([\w-]+):\s*oklch\(([\d.]+) ([\d.]+) ([\d.]+)(?:\s*\/\s*([\d.]+)%)?\)/g),
    ].map(([, name, l, c, h, alpha]) => [
      name,
      { l: Number(l), c: Number(c), h: Number(h), alpha: alpha ? Number(alpha) / 100 : 1 },
    ]),
  )
}

// OKLCH → OKLab → linear sRGB (Björn Ottosson's matrices), clipped to the gamut.
function linearRgb({ l, c, h }: Oklch): number[] {
  const a = c * Math.cos((h * Math.PI) / 180)
  const b = c * Math.sin((h * Math.PI) / 180)
  const lms = [
    l + 0.3963377774 * a + 0.2158037573 * b,
    l - 0.1055613458 * a - 0.0638541728 * b,
    l - 0.0894841775 * a - 1.291485548 * b,
  ].map((x) => x ** 3)
  const channel = (row: number[]) => row.reduce((sum, k, i) => sum + k * lms[i], 0)
  return [
    [4.0767416621, -3.3077115913, 0.2309699292],
    [-1.2684380046, 2.6097574011, -0.3413193965],
    [-0.0041960863, -0.7034186147, 1.707614701],
  ].map((row) => Math.min(1, Math.max(0, channel(row))))
}

// The relative luminance of WCAG, which weighs the linear channels.
const luminanceOf = ([r, g, b]: number[]) => 0.2126 * r + 0.7152 * g + 0.0722 * b

// The sRGB transfer function and its inverse, for a channel in [0, 1].
const encode = (x: number) => (x <= 0.0031308 ? 12.92 * x : 1.055 * x ** (1 / 2.4) - 0.055)
const decode = (x: number) => (x <= 0.04045 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4)

const ratio = (one: number, other: number) => {
  const [light, dark] = [one, other].sort((x, y) => y - x)
  return (light + 0.05) / (dark + 0.05)
}

/** The measures of one theme: a token, its luminance, and the luminance of a colour with an opacity over a surface. */
function theme(selector: ':root' | '.dark') {
  const tokens = tokensOf(selector)
  const token = (name: string) => {
    const value = tokens.get(name)
    if (!value)
      throw new Error(`--${name} of ${selector} is not written oklch(L C H) or oklch(L C H / A%)`)
    return value
  }
  /**
   * The luminance of `fill` at `alpha` (times its own) over the opaque `surface`: the browser composites a colour
   * with opacity in gamma-encoded sRGB, so the channels are blended encoded, then decoded to weigh them.
   */
  const blended = (fill: string, alpha: number, surface: string) => {
    const opacity = alpha * token(fill).alpha
    const [top, bottom] = [linearRgb(token(fill)), linearRgb(token(surface))]
    return luminanceOf(
      top.map((x, i) => decode(opacity * encode(x) + (1 - opacity) * encode(bottom[i]))),
    )
  }
  const luminance = (name: string) => {
    if (token(name).alpha < 1)
      throw new Error(`--${name} has an opacity: blend it over its surface`)
    return luminanceOf(linearRgb(token(name)))
  }
  const contrast = (one: string, other: string) => ratio(luminance(one), luminance(other))
  return { tokens, token, luminance, blended, contrast }
}

const light = theme(':root')
const dark = theme('.dark')

// The opacity (`/NN`) of the first class that `pattern` captures in a component of components/ui/.
function opacityIn(file: string, pattern: RegExp) {
  const value = source(`../components/ui/${file}`).match(pattern)?.[1]
  expect(value, `${pattern} in ${file}`).toBeDefined()
  return Number(value) / 100
}

// The focused destructive item of a menu as dropdown-menu.tsx writes it: its text token and the opacity of
// --destructive under it in each theme; a `dark:` class overrides the light one. Radix focuses an item under the
// pointer too, so this is also its hover.
function focusedDestructiveItem(mode: 'light' | 'dark') {
  const item = source('../components/ui/dropdown-menu.tsx')
  const find = (property: string) => {
    const light = new RegExp(
      `(?<!dark:)data-\\[variant=destructive\\]:focus:${property}-([^\\s"]+)`,
    )
    const dark = new RegExp(`dark:data-\\[variant=destructive\\]:focus:${property}-([^\\s"]+)`)
    return ((mode === 'dark' && item.match(dark)?.[1]) || item.match(light)?.[1]) ?? undefined
  }
  const [text, ground] = [find('text'), find('bg')]
  expect([text, ground]).not.toContain(undefined)
  // A token at full opacity: a text colour with an opacity (`/80`) would be lighter than the token measured here.
  expect(text).toMatch(/^[\w-]+$/)
  const alpha = /^destructive\/(\d+)$/.exec(ground!)?.[1]
  expect(alpha, `the ground bg-${ground}`).toBeDefined()
  return { text: text!, alpha: Number(alpha) / 100 }
}

describe('theme tokens of :root', () => {
  it.each([
    ['input', 'background'],
    ['input', 'card'],
    ['input', 'popover'],
    ['ring', 'background'],
    ['ring', 'card'],
    ['ring', 'popover'],
    ['sidebar-ring', 'background'],
    ['sidebar-ring', 'sidebar'],
  ])('--%s reaches 3:1 on --%s (SC 1.4.11)', (border, surface) => {
    expect(light.contrast(border, surface)).toBeGreaterThanOrEqual(3 + MARGIN)
  })

  it.each([['muted'], ['background'], ['card'], ['popover'], ['sidebar']])(
    '--muted-foreground reaches 4.5:1 on --%s (SC 1.4.3)',
    (surface) => {
      expect(light.contrast('muted-foreground', surface)).toBeGreaterThanOrEqual(4.5 + MARGIN)
    },
  )

  it('--destructive carries white text, the white of --background, at 4.5:1 (SC 1.4.3)', () => {
    expect(light.contrast('destructive', 'background')).toBeGreaterThanOrEqual(4.5 + MARGIN)
  })

  it('--success-soft-foreground reaches 4.5:1 on --success-soft (SC 1.4.3)', () => {
    expect(light.contrast('success-soft-foreground', 'success-soft')).toBeGreaterThanOrEqual(
      4.5 + MARGIN,
    )
  })

  it.each([[0.1], [0.2]])(
    '--destructive-soft-foreground reaches 4.5:1 on --destructive at %s over --background (SC 1.4.3)',
    (alpha) => {
      const ground = light.blended('destructive', alpha, 'background')
      expect(ratio(light.luminance('destructive-soft-foreground'), ground)).toBeGreaterThanOrEqual(
        4.5 + MARGIN,
      )
    },
  )

  it('the focused destructive item of a menu reaches 4.5:1 on its ground over --popover (SC 1.4.3)', () => {
    const { text, alpha } = focusedDestructiveItem('light')
    const ground = light.blended('destructive', alpha, 'popover')
    expect(ratio(light.luminance(text), ground)).toBeGreaterThanOrEqual(4.5 + MARGIN)
  })

  it('the resting destructive item of a menu, --destructive on --popover, reaches 4.5:1 (SC 1.4.3)', () => {
    expect(light.contrast('destructive', 'popover')).toBeGreaterThanOrEqual(4.5 + MARGIN)
  })

  it('keeps a focused field darker than its resting border', () => {
    expect(light.luminance('ring')).toBeLessThan(light.luminance('input'))
  })

  it.each([['input'], ['ring'], ['sidebar-ring'], ['muted-foreground']])(
    'keeps --%s neutral',
    (name) => {
      expect(light.token(name).c).toBe(0)
    },
  )
})

describe('theme tokens of .dark', () => {
  it('gives every token of :root a value', () => {
    expect([...light.tokens.keys()].filter((name) => !dark.tokens.has(name))).toEqual([])
  })

  const surfaces = ['background', 'card', 'popover']

  it.each(surfaces)('--input, with its opacity, reaches 3:1 over --%s (SC 1.4.11)', (surface) => {
    expect(
      ratio(dark.blended('input', 1, surface), dark.luminance(surface)),
    ).toBeGreaterThanOrEqual(3 + MARGIN)
  })

  it.each([
    ...surfaces.map((surface) => ['ring', surface]),
    ['sidebar-ring', 'background'],
    ['sidebar-ring', 'sidebar'],
  ])('--%s reaches 3:1 on --%s (SC 1.4.11)', (border, surface) => {
    expect(dark.contrast(border, surface)).toBeGreaterThanOrEqual(3 + MARGIN)
  })

  it.each(surfaces)(
    'keeps a focused field more visible than its resting border over --%s',
    (surface) => {
      const resting = ratio(dark.blended('input', 1, surface), dark.luminance(surface))
      expect(dark.contrast('ring', surface)).toBeGreaterThan(resting)
    },
  )

  it.each([
    ['foreground', 'background'],
    ['foreground', 'card'],
    ['foreground', 'popover'],
    ['foreground', 'sidebar'],
    ['card-foreground', 'card'],
    ['popover-foreground', 'popover'],
    ['sidebar-foreground', 'sidebar'],
    ['muted-foreground', 'muted'],
    ['muted-foreground', 'background'],
    ['muted-foreground', 'card'],
    ['muted-foreground', 'popover'],
    ['muted-foreground', 'sidebar'],
    ['primary-foreground', 'primary'],
    ['secondary-foreground', 'secondary'],
    ['accent-foreground', 'accent'],
    ['sidebar-primary-foreground', 'sidebar-primary'],
    ['sidebar-accent-foreground', 'sidebar-accent'],
    ['destructive', 'background'],
    ['destructive', 'card'],
    ['destructive', 'popover'],
    ['success-soft-foreground', 'success-soft'],
  ])('--%s reaches 4.5:1 on --%s (SC 1.4.3)', (text, ground) => {
    expect(dark.contrast(text, ground)).toBeGreaterThanOrEqual(4.5 + MARGIN)
  })

  it.each(
    ['input.tsx', 'input-group.tsx', 'textarea.tsx'].flatMap((file) =>
      surfaces.map((surface) => [file, surface]),
    ),
  )(
    'the placeholder, --muted-foreground, reaches 4.5:1 on the ground of a field of %s over --%s (SC 1.4.3)',
    (file, surface) => {
      const field = opacityIn(file, /dark:bg-input\/(\d+)/)
      expect(
        ratio(dark.luminance('muted-foreground'), dark.blended('input', field, surface)),
      ).toBeGreaterThanOrEqual(4.5 + MARGIN)
    },
  )

  it('the description of a destructive Alert reaches 4.5:1 on --card (SC 1.4.3)', () => {
    const text = opacityIn('alert.tsx', /alert-description\]:text-destructive\/(\d+)/)
    expect(
      ratio(dark.blended('destructive', text, 'card'), dark.luminance('card')),
    ).toBeGreaterThanOrEqual(4.5 + MARGIN)
  })

  it.each([
    ['button.tsx', 'background'],
    ['button.tsx', 'card'],
    ['badge.tsx', 'background'],
    ['badge.tsx', 'card'],
  ])(
    'the white text of `destructive` in %s reaches 4.5:1 over --%s (SC 1.4.3)',
    (file, surface) => {
      const ground = opacityIn(file, /text-white[^"]*dark:bg-destructive\/(\d+)/)
      // White has the luminance 1.
      expect(ratio(1, dark.blended('destructive', ground, surface))).toBeGreaterThanOrEqual(
        4.5 + MARGIN,
      )
    },
  )

  it.each([
    ['at rest', /"destructive-soft":\s*"[^"]*?(?<!hover:)dark:bg-destructive\/(\d+)/],
    ['on hover', /"destructive-soft":\s*"[^"]*?dark:hover:bg-destructive\/(\d+)/],
  ])(
    'the button `destructive-soft` %s reaches 4.5:1 over --background and --card (SC 1.4.3)',
    (_, pattern) => {
      const alpha = opacityIn('button.tsx', pattern)
      for (const surface of ['background', 'card']) {
        const ground = dark.blended('destructive', alpha, surface)
        expect(
          ratio(dark.luminance('destructive-soft-foreground'), ground),
          surface,
        ).toBeGreaterThanOrEqual(4.5 + MARGIN)
      }
    },
  )

  it('the focused destructive item of a menu reaches 4.5:1 on its ground over --popover (SC 1.4.3)', () => {
    const { text, alpha } = focusedDestructiveItem('dark')
    const ground = dark.blended('destructive', alpha, 'popover')
    expect(ratio(dark.luminance(text), ground)).toBeGreaterThanOrEqual(4.5 + MARGIN)
  })
})
