import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { renderWithProviders } from '@/test/render'
import { usePendingStaffCount } from './use-pending-staff-count'

// The count of accounts waiting for approval (STF-09) that "Chờ duyệt" shows in the menu and on the status links.

const api = vi.hoisted(() => ({ fetch: vi.fn() }))
vi.mock('@/lib/api/api', () => ({ apiFetch: api.fetch }))
vi.mock('@/lib/auth/session', () => ({}))

function Count({ enabled }: { enabled: boolean }) {
  const count = usePendingStaffCount(enabled)
  return <output>{count === undefined ? 'none' : count}</output>
}

beforeEach(() => vi.resetAllMocks())

describe('usePendingStaffCount', () => {
  it('reads totalCount of one row of the PENDING accounts', async () => {
    api.fetch.mockResolvedValue({
      data: { items: [], totalCount: 3, page: 1, pageSize: 1 },
      status: 200,
      headers: new Headers(),
    })
    renderWithProviders(<Count enabled />)

    expect(await screen.findByText('3')).toBeVisible()
    expect(api.fetch).toHaveBeenCalledWith(
      '/api/v1/admin/staff?status=PENDING&pageSize=1',
      expect.anything(),
    )
  })

  it('asks nothing and shows nothing when disabled', () => {
    renderWithProviders(<Count enabled={false} />)

    expect(screen.getByText('none')).toBeVisible()
    expect(api.fetch).not.toHaveBeenCalled()
  })

  it('shows nothing when the count fails, so the entry never waits on it', async () => {
    api.fetch.mockRejectedValue(new TypeError('Failed to fetch'))
    renderWithProviders(<Count enabled />)

    await vi.waitFor(() => expect(api.fetch).toHaveBeenCalled())
    expect(screen.getByText('none')).toBeVisible()
  })
})
