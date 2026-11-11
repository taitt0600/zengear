import { DropdownMenuRadioGroup, DropdownMenuRadioItem } from '@/components/ui/dropdown-menu'

type ThemeLabels = { light: string; dark: string; system: string }

const THEMES = ['light', 'dark', 'system'] as const

/**
 * The three choices of "Giao diện" (DEC-123 (1)), shared by the header's switch and the item of the user menu on a
 * narrow screen. Presentational: the caller holds the theme.
 */
export function ThemeOptions({
  value,
  onValueChange,
  labels,
}: {
  value: string | undefined
  onValueChange: (theme: string) => void
  labels: ThemeLabels
}) {
  return (
    <DropdownMenuRadioGroup value={value} onValueChange={onValueChange}>
      {THEMES.map((theme) => (
        <DropdownMenuRadioItem key={theme} value={theme}>
          {labels[theme]}
        </DropdownMenuRadioItem>
      ))}
    </DropdownMenuRadioGroup>
  )
}
