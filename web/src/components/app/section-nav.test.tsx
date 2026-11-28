import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SectionNav } from './section-nav'

// The section navigation of SCR-STF-03 and SCR-ACC-01 (docs/screens/, Section navigation): one link per card, the
// current one marked, a plain click handed to the page so that it scrolls and moves the focus itself.

const items = [
  { id: 'info', label: 'Hồ sơ nhân viên' },
  { id: 'account', label: 'Tài khoản' },
]

function renderNav(onSelect = vi.fn()) {
  render(<SectionNav label="Trên trang này" items={items} currentId="info" onSelect={onSelect} />)
  return onSelect
}

describe('the section navigation', () => {
  it('is named by its visible title and lists one link per card, in page order', () => {
    renderNav()

    const nav = screen.getByRole('navigation', { name: 'Trên trang này' })
    expect(screen.getByText('Trên trang này')).toBeVisible()
    const links = within(nav).getAllByRole('link')
    expect(links.map((link) => [link.textContent, link.getAttribute('href')])).toEqual([
      ['Hồ sơ nhân viên', '#info'],
      ['Tài khoản', '#account'],
    ])
  })

  it('marks the current card only', () => {
    renderNav()

    expect(screen.getByRole('link', { name: 'Hồ sơ nhân viên' })).toHaveAttribute(
      'aria-current',
      'location',
    )
    expect(screen.getByRole('link', { name: 'Tài khoản' })).not.toHaveAttribute('aria-current')
  })

  it('hands a plain click to the page instead of following the link', async () => {
    const onSelect = renderNav()

    await userEvent.click(screen.getByRole('link', { name: 'Tài khoản' }))

    expect(onSelect).toHaveBeenCalledWith('account')
    expect(window.location.hash).toBe('')
  })

  it('leaves a modified click to the browser (a new tab, a new window)', () => {
    const onSelect = renderNav()

    const followed = fireEvent.click(screen.getByRole('link', { name: 'Tài khoản' }), {
      ctrlKey: true,
    })

    expect(onSelect).not.toHaveBeenCalled()
    expect(followed).toBe(true)
  })
})
