'use client'

import { MoonIcon, SunIcon } from 'lucide-react'
import { useTranslations } from 'next-intl'
import { useTheme } from 'next-themes'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { ThemeOptions } from './theme-options'

/**
 * "Giao diện" in the header of SCR-ADM-01 from md up (DEC-123 (3)): an icon button, the sun or the moon of the theme
 * now shown, that opens the three choices. The header renders after the session, so the theme is known by then.
 */
export function ThemeSwitch() {
  const t = useTranslations('admin.theme')
  const { theme, resolvedTheme, setTheme } = useTheme()
  const label = t('label')

  return (
    <DropdownMenu>
      <Tooltip>
        <TooltipTrigger asChild>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" aria-label={label}>
              {resolvedTheme === 'dark' ? <MoonIcon aria-hidden /> : <SunIcon aria-hidden />}
            </Button>
          </DropdownMenuTrigger>
        </TooltipTrigger>
        <TooltipContent>{label}</TooltipContent>
      </Tooltip>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel>{label}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <ThemeOptions
          value={theme}
          onValueChange={setTheme}
          labels={{ light: t('light'), dark: t('dark'), system: t('system') }}
        />
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
