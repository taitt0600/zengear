import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { initials } from '@/utils/initials'

/**
 * Presentational: the photo block of a staff member's profile card (SCR-STF-03 "Hồ sơ nhân viên", SCR-ACC-01 "Thông
 * tin tài khoản"): their initials, hidden from screen readers since the name is on the page already. 160px in a wide
 * card, 96px in a narrow one (the card's container, not the window). Gap 8 adds the photo and "Chọn ảnh" with the code
 * that uses them; their texts (alt, button, hint) then come as props, since each screen words them its own way.
 */
export function ProfilePhoto({ name }: { name: string }) {
  return (
    <Avatar className="size-24 @xl:size-40" aria-hidden>
      <AvatarFallback className="text-3xl font-medium @xl:text-5xl">
        {initials(name)}
      </AvatarFallback>
    </Avatar>
  )
}
