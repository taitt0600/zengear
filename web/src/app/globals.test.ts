// @vitest-environment node
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

// WCAG 2.2 AA (docs/FRONTEND.md) on the theme of :root, the only one the app sets: SC 1.4.11 asks 3:1 for the
// border of a field and of its focus, SC 1.4.3 asks 4.5:1 for text: the white text of the badge `destructive` on
// --destructive (measured against --background, the same white), the soft badge `success` (its text token on its
// ground), and the button `destructive-soft`, whose text token sits on --destructive at 10 % (at rest) and 20 % (on
// hover) over --background, blended as the browser blends (Layout of SCR-STF-03, Theme). The margin absorbs the
// rounding of a render.
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

// OKLCH → OKLab → linear sRGB (Björn Ottosson's matrices), clipped to the gamut.
function linearRgb(name: string): number[] {
  const { l, c, h } = token(name)
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
const luminance = (name: string) => luminanceOf(linearRgb(name))

// The sRGB transfer function and its inverse, for a channel in [0, 1].
const encode = (x: number) => (x <= 0.0031308 ? 12.92 * x : 1.055 * x ** (1 / 2.4) - 0.055)
const decode = (x: number) => (x <= 0.04045 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4)

/**
 * The luminance of `fill` at `alpha` over `surface`: the browser composites a colour with opacity
 * (`bg-destructive/10`) in gamma-encoded sRGB, so the channels are blended encoded, then decoded to weigh them.
 */
function blendedLuminance(fill: string, alpha: number, surface: string) {
  const [top, bottom] = [linearRgb(fill), linearRgb(surface)]
  return luminanceOf(top.map((x, i) => decode(alpha * encode(x) + (1 - alpha) * encode(bottom[i]))))
}

const ratio = (one: number, other: number) => {
  const [light, dark] = [one, other].sort((x, y) => y - x)
  return (light + 0.05) / (dark + 0.05)
}

function contrast(one: string, other: string) {
  return ratio(luminance(one), luminance(other))
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

  it('--destructive carries white text, the white of --background, at 4.5:1 (SC 1.4.3)', () => {
    expect(contrast('destructive', 'background')).toBeGreaterThanOrEqual(4.5 + MARGIN)
  })

  it('--success-soft-foreground reaches 4.5:1 on --success-soft (SC 1.4.3)', () => {
    expect(contrast('success-soft-foreground', 'success-soft')).toBeGreaterThanOrEqual(4.5 + MARGIN)
  })

  it.each([[0.1], [0.2]])(
    '--destructive-soft-foreground reaches 4.5:1 on --destructive at %s over --background (SC 1.4.3)',
    (alpha) => {
      const ground = blendedLuminance('destructive', alpha, 'background')
      expect(ratio(luminance('destructive-soft-foreground'), ground)).toBeGreaterThanOrEqual(
        4.5 + MARGIN,
      )
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
