import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { initials } from '@/utils/initials'

/**
 * Presentational: the photo block of the card "Hồ sơ nhân viên" of SCR-STF-03, its initials until gap 8 gives a photo,
 * hidden from screen readers since the name is the page's h1. 160px wide card, 96px narrow (the card's container, not
 * the window). STF-10 adds the photo and "Chọn ảnh" (`photoUrl`, `onChoose`) with the code that uses them; moves to
 * components/app/profile-photo.tsx with SCR-ACC-01.
 */
export function StaffPhoto({ name }: { name: string }) {
  return (
    <Avatar className="size-24 @xl:size-40" aria-hidden>
      <AvatarFallback className="text-3xl font-medium @xl:text-5xl">
        {initials(name)}
      </AvatarFallback>
    </Avatar>
  )
}
