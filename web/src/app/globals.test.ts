// @vitest-environment node
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

// WCAG 2.2 AA (docs/FRONTEND.md) on the theme of :root, the only one the app sets: SC 1.4.11 asks 3:1 for the
// border of a field and of its focus, SC 1.4.3 asks 4.5:1 for text. The margin absorbs the rounding of a render.
const MARGIN = 0.05

const css = readFileSync(new URL('./globals.css', import.meta.url), 'utf8')
const root = css.slice(css.indexOf(':root {'), css.indexOf('}', css.indexOf(':root {')))
const tokens = new Map(
  [...root.matchAll(/--([\w-]+):\s*oklch\(([\d.]+) ([\d.]+) ([\d.]+)\)/g)].map(
    ([, name, l, c, h]) => [name, { l: Number(l), c: Number(c), h: Number(h) }],
  ),
)

function token(name: string) {
  const value = tokens.get(name)
  if (!value) throw new Error(`--${name} of :root is not written oklch(L C H)`)
  return value
}

// OKLCH → OKLab → linear sRGB (Björn Ottosson's matrices), then the relative luminance of WCAG, which weighs
// the linear channels: the conversion yields them directly, so no gamma step is needed.
function luminance(name: string) {
  const { l, c, h } = token(name)
  const a = c * Math.cos((h * Math.PI) / 180)
  const b = c * Math.sin((h * Math.PI) / 180)
  const lms = [
    l + 0.3963377774 * a + 0.2158037573 * b,
    l - 0.1055613458 * a - 0.0638541728 * b,
    l - 0.0894841775 * a - 1.291485548 * b,
  ].map((x) => x ** 3)
  const channel = (row: number[]) => row.reduce((sum, k, i) => sum + k * lms[i], 0)
  const [r, g, bl] = [
    [4.0767416621, -3.3077115913, 0.2309699292],
    [-1.2684380046, 2.6097574011, -0.3413193965],
    [-0.0041960863, -0.7034186147, 1.707614701],
  ].map((row) => Math.min(1, Math.max(0, channel(row))))
  return 0.2126 * r + 0.7152 * g + 0.0722 * bl
}

function contrast(one: string, other: string) {
  const [light, dark] = [luminance(one), luminance(other)].sort((x, y) => y - x)
  return (light + 0.05) / (dark + 0.05)
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
    expect(contrast(border, surface)).toBeGreaterThanOrEqual(3 + MARGIN)
  })

  it.each([['muted'], ['background'], ['card'], ['popover'], ['sidebar']])(
    '--muted-foreground reaches 4.5:1 on --%s (SC 1.4.3)',
    (surface) => {
      expect(contrast('muted-foreground', surface)).toBeGreaterThanOrEqual(4.5 + MARGIN)
    },
  )

  it('keeps a focused field darker than its resting border', () => {
    expect(luminance('ring')).toBeLessThan(luminance('input'))
  })

  it.each([['input'], ['ring'], ['sidebar-ring'], ['muted-foreground']])(
    'keeps --%s neutral',
    (name) => {
      expect(token(name).c).toBe(0)
    },
  )
})
