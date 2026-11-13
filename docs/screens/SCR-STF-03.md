# SCR-STF-03 Staff account

One staff account, with the actions to disable or restore it (UC-005, UC-006).

Loaded from `GET /api/v1/admin/staff/{id}`: name, email, employee code, joined date, left date, work phone, status, roles,
and whether the default password is changed (`mustChangePassword`). No field is editable in Phase 1 (STF-01 creates,
STF-02 disables and restores); the action "Cập nhật thông tin" is designed ahead and waits for gap 7, the photo for
gap 8, the password reset link for gap 9 and the activity card for gap 10.

**Action "Khóa tài khoản"** (with `staff.disable`, on an ACTIVE account; on a DISABLED account the same action is
named "Khóa lại", for the case of a 500 below; not shown on the user's own account — the API refuses it anyway, R-USR-12): a dialog

| Field | API | Rule (web) |
|---|---|---|
| Nhân viên nghỉ việc | — | checkbox; when ticked, the date field below is required |
| Ngày nghỉ việc | `leftDate` | date picker, not after today (Asia/Ho_Chi_Minh), not before the joined date |

with the warning "Nhân viên sẽ bị đăng xuất khỏi mọi thiết bị ngay lập tức." "Khóa" sends
`POST /api/v1/admin/staff/{id}/disable` `{ leftDate }` (null when not ticked); 200 shows the account as returned.

**Action "Khôi phục tài khoản"** (with `staff.disable`, on a DISABLED account): a confirmation, then
`POST /api/v1/admin/staff/{id}/restore`; 200 shows the account as returned.

| Answer | Shown |
|---|---|
| 404 | Its `detail` on the page, with a link to SCR-STF-01 |
| 400 with an item on `#/leftDate` | Under the date field |
| 409 `/problems/left-date-before-joined-date`, `/problems/left-date-in-future` (item on `#/leftDate`) | Under the date field |
| 409 `/problems/own-account-not-lockable`, `/problems/last-active-admin`, `/problems/admin-account-not-lockable`, `/problems/admin-account-not-restorable`, `/problems/account-out-of-scope`, `/problems/account-anonymized` | Toast |
| 409 `/problems/concurrency-conflict` | Toast, and reloads |
| 500 on "Khóa" | The account may be disabled with its sessions still open (R-SEC-04), or not disabled at all: the web's text of [*Conventions*](conventions.md) in a toast. The account is reloaded; "Khóa" (ACTIVE) or "Khóa lại" (DISABLED) sends the request again, which revokes the sessions again |

## Layout

- **Design**: <https://claude.ai/artifact/QjTnaq2rsB49xBeN3Wkuen>, drawn 2026-11-11 with the design system of
  2026-11-11; approved by the merge of the pull request that wrote this subsection (DEC-119). It changes the layout of
  the page (one centred column, with a section navigation from `xl`), adds the password card, whose default-password
  row is built now, and draws gaps 9 and 10 ahead; the 2560 px artboard shows the column centred on a 2K screen. The
  dialog "Khóa tài khoản", the confirmation "Khôi phục tài khoản?", the DISABLED account and the sheet "Cập nhật thông
  tin" keep their design of <https://claude.ai/artifact/DuLX9hmqGWyyP9vmoUw6Dd> (pull request #111). The canvas shows
  the frame of SCR-ADM-01 for context only; this Layout does not approve it.
- **Artboards**: desktop (1440 px) of an ACTIVE account, where the section navigation works in Play; wide (2560 px),
  where the column keeps 1024 px and centres; narrow (390 px) of an account whose default password is unchanged, with
  no section navigation; dark (1440 px, DEC-123 (5): soft badges and `destructive-soft` on the dark values of
  DEC-124), with "Trạng thái tài khoản" the current section; states: loading (the navigation shows, the cards as
  Skeleton), 404 and error (an Empty in the column, no navigation). No permission is the page of SCR-ADM-01
  (`RequirePermission` with `staff.read`). The sample is the fixture manager. At the owner's request the heading keeps
  its small Avatar, the details card shows the information as divided rows with "Cập nhật thông tin" under them (drawn
  ahead for gap 7, as in the previous Layout) and, beside the information, the photo: an Avatar of 160px with the
  initials, and "Chọn ảnh" under it, drawn ahead and built once gap 8 closes, like the sheet of gap 7; the 390 px
  artboard draws the photo at 96px (the heading Avatar at 56px) above the details, and "Chọn ảnh" and "Cập nhật thông
  tin" at full width. Below the status card come the password card (whether the default password is changed, and "Gửi
  link đặt lại mật khẩu" with its hint and the time of the last link, drawn ahead for gap 9) and the activity card
  (filters, a table of events, "Tải thêm"; on the narrow artboard a list and a "Bộ lọc" button; drawn ahead for gap
  10); the ResetPassword artboard draws the confirmation "Gửi link đặt lại mật khẩu?" (gap 9).
- **shadcn**: no `add` for what the first build makes; `shadcn add toggle-group` comes with the activity card of gap
  10, the only part that needs a component not installed. The section navigation is plain links on the tokens (shadcn
  has no table of contents; `sidebar` is the app's menu and too heavy here). It lives in this module
  (`components/section-nav.tsx`, `hooks/use-active-section.ts`) while only this screen uses it (DEC-118 (1),
  `docs/FRONTEND.md`, *Modules and boundaries*); the pull request that builds the Layout of SCR-ACC-01, which takes
  the same layout, moves both to `components/app/` and `@/hooks`.
- **Theme**: unchanged and built: the soft status badges (`success` on `success-soft`, DISABLED `secondary`,
  ANONYMIZED `outline`) and the Button variant `destructive-soft` of the previous Layout of this screen are in
  `app/globals.css` and `components/ui/` since the pull request of STF-02, with their `.dark` values since AUT-04
  (DEC-123 (4), DEC-124). The current link of the section navigation is `accent` with `foreground` text at weight 500;
  the others `muted-foreground` (4.98:1 light, 6.72:1 dark on `background`), on hover `accent` (`muted-foreground` on
  `accent` 4.56:1 light, 4.83:1 dark).
- **Section navigation** (new behaviour, so the build is a `feat(web): … (STF-02)` pull request on
  `feat/stf-02-web-2`, the next free name since `feat/stf-02-web` has merged, CLAUDE.md *Workflow*; as the paragraph
  under the [screen list](README.md#screen-list) says): from `xl` (1280px) a `nav` labelled by its visible title "Trên trang này"
  (`admin.sectionNav.label`, `text-xs font-medium`, `muted-foreground`) sits left of the cards in a column of 200px,
  `sticky top-6` (24px, the page padding; the header of SCR-ADM-01 scrolls with the page) with `self-start` on its
  grid cell, and lists one link per card, in page order, with the card's title as its text
  (`staff.account.info.title`, `.status.title`, `.password.title` and `.activity.title`; the last only once gap 10 is
  built); a link text wraps, never cut short. It is navigation, not content, so it sits on the page ground like the
  menu of SCR-ADM-01, an exception to **Grouping**. Each card is a `section` with an `id` (`info`, `status`,
  `password`, `activity`), `aria-labelledby` its title and `scroll-margin-top` 24px; the titles are `h2` with the ids
  `<id>-title` and `tabindex="-1"`. A click scrolls to the card (smoothly, at once under `prefers-reduced-motion`),
  moves the focus to its `h2` and replaces the URL hash without a new history entry. While the page scrolls, the
  current link (`aria-current="location"`) is the last card whose top has passed a line 24px plus a third of the
  viewport from the top; when the document scrolls at all (`scrollHeight` above the viewport), the last card is
  current once the sentinel after it (`staff-account-end`, an empty `aria-hidden` div of the layout) comes into view;
  when it does not scroll, the first card stays current (from the build of SCR-ACC-01, a clicked card stays current
  until the next scroll moves the line, see its Layout). One IntersectionObserver watches the sections and the
  sentinel; no handler on every scroll frame. A hash on load (`#<id>`) is applied once the account has loaded, not
  while the Skeleton shows. Below `xl` the navigation is hidden with CSS (its section stays mounted, so a hash still
  scrolls) and the status card comes first.
- **Width**: the page's content is one column of at most 1024px (`max-w-5xl`), centred in the page area of SCR-ADM-01
  (24px padding, 16px narrow); from `xl` it holds the heading over a grid `200px minmax(0, 1fr)` with a gap of 32px
  (navigation | cards), so a card is about 792px wide at most, whatever the screen. `xl` rather than `lg`, because at
  1024px with the menu expanded the card column would be about 488px.
- **Texts** (`messages/vi.json`), new ones marked *new*, under `staff.account` unless named: `admin.sectionNav.label`
  "Trên trang này" (*new*; the visible title of the section navigation, which names the `nav`); for gap 8, not built
  yet: `info.photoAlt` "Ảnh của {name}" (*new*; the alt of the photo once there is one), `info.choosePhoto` "Chọn ảnh"
  and its hint `info.photoHint` "Ảnh chân dung, rõ mặt. JPG hoặc PNG, tối đa 2 MB." (*new*; the formats and the 2 MB
  the owner chose on 2026-11-11, which the Decision of gap 8 records); the password card `passwordCard.title` "Mật
  khẩu", `passwordCard.description` "Khi nhân viên quên mật khẩu, gửi link để họ tự đặt mật khẩu mới.", the row
  `passwordCard.default` "Mật khẩu mặc định" with the Badges `passwordCard.unchanged` "Chưa đổi" and
  `passwordCard.changed` "Đã đổi" (*new*), and, for gap 9, not built yet: the row `passwordCard.lastReset` "Link đặt
  lại gần nhất" with `passwordCard.neverReset` "Chưa gửi lần nào" or the time, `passwordCard.sendReset` "Gửi link đặt
  lại mật khẩu" and its hint `passwordCard.sendResetHint` "Nhân viên nhận email có link đặt mật khẩu mới, hết hạn sau
  1 giờ, và bị đăng xuất khỏi mọi thiết bị.", the confirmation `resetDialog.title` "Gửi link đặt lại mật khẩu?",
  `resetDialog.description` "Hệ thống gửi email tới {email} với link đặt mật khẩu mới, hết hạn sau 1 giờ. Nhân viên bị
  đăng xuất khỏi mọi thiết bị ngay khi bạn xác nhận; mật khẩu cũ vẫn dùng được cho tới khi họ đặt mật khẩu mới.",
  `resetDialog.submit` "Gửi link" and the toast `resetSent` "Đã gửi link đặt lại mật khẩu." (*new*); for gap 10, not
  built yet: `activity.title` "Hoạt động", `activity.description` "Việc nhân viên đã làm trong hệ thống và những thay
  đổi trên tài khoản này, mới nhất trước.", the filters `activity.range` "Khoảng thời gian", `activity.type` "Loại
  hoạt động", `activity.area` "Khu vực", `activity.scope` "Liên quan" with `activity.scopeAll` "Tất cả",
  `activity.scopeBy` "Do nhân viên làm" and `activity.scopeOn` "Trên tài khoản", `activity.all` "Tất cả",
  `activity.clear` "Xóa bộ lọc", `activity.filters` "Bộ lọc ({count})" (narrow) and its Sheet
  `activity.filtersSheet.title` "Lọc hoạt động" and `activity.filtersSheet.description` "Chọn khoảng thời gian, loại,
  khu vực và phạm vi hoạt động cần xem.", the columns `activity.columns.time` "Thời gian", `.action` "Hoạt động",
  `.target` "Đối tượng", `.area` "Khu vực", `activity.tableLabel` "Hoạt động của tài khoản", `activity.shown` "Đang
  hiện {count} hoạt động mới nhất trong khoảng đã chọn.", `activity.loadMore` "Tải thêm", `activity.end` "Đã hiện hết
  hoạt động trong khoảng đã chọn.", the Empty `activity.empty` "Không có hoạt động nào trong khoảng đã chọn." with
  `activity.emptyDescription` "Thử chọn khoảng thời gian rộng hơn hoặc xóa bộ lọc.", and the names of the event types
  and areas, which the Decision of gap 10 lists (*new*); the page's title and breadcrumb entry `title` "Tài khoản nhân
  viên" (*new*); the details card `info.title` "Thông tin nhân viên" and `info.description` "Thông tin ghi khi tạo tài
  khoản, dùng để liên hệ và phân quyền." (*new*); its labels `staff.list.columns.email`, `.employeeCode`, `.roles`,
  `.joinedDate`, `.leftDate`, with `workPhone` "SĐT công việc" (*new*), and `staff.list.noValue` "—" for an empty
  value; the status card `status.title` "Trạng thái tài khoản" and `status.description` "Tài khoản bị khóa không đăng
  nhập được; khôi phục để nhân viên dùng lại." (*new*), its label `staff.list.columns.status`, the badges
  `staff.list.status.active` and `.disabled`, with `status.anonymized` "Đã ẩn danh" (*new*); (the keys `password` and
  `mustChangePassword` of the status card go, since its password row moves to the password card); the actions
  `disable` "Khóa tài khoản", `disableAgain` "Khóa lại" with its hint `disableAgainHint` "Gửi lại lệnh khóa để đăng
  xuất nhân viên khỏi mọi thiết bị, khi lần khóa trước báo lỗi.", `restore` "Khôi phục tài khoản", `ownAccount` "Bạn
  không thể khóa tài khoản của chính mình." and `anonymized` "Dữ liệu cá nhân của tài khoản này đã được ẩn danh, nên
  không khóa hay khôi phục được nữa." (*new*); the dialog `disableDialog.title` "Khóa tài khoản",
  `disableDialog.description` "Tài khoản bị khóa không đăng nhập được cho đến khi được khôi phục.",
  `disableDialog.warning` "Nhân viên sẽ bị đăng xuất khỏi mọi thiết bị ngay lập tức.", `disableDialog.leaving` "Nhân
  viên nghỉ việc" with its hint `disableDialog.leavingHint` "Đánh dấu khi nhân viên đã nghỉ, để ghi ngày làm việc cuối
  cùng.", the date's hint `disableDialog.leftDateHint` "Ngày làm việc cuối cùng: không sau hôm nay và không trước ngày
  vào làm ({joinedDate})." or, without a joined date, `disableDialog.leftDateHintToday` "Ngày làm việc cuối cùng:
  không sau hôm nay.", `disableDialog.submit` "Khóa" (*new*; the date's label is `staff.list.columns.leftDate`); the
  confirmation `restoreDialog.title` "Khôi phục tài khoản?", `restoreDialog.description` "Nhân viên đăng nhập lại được
  bằng mật khẩu đang có, và ngày nghỉ việc được xóa." and `restoreDialog.submit` "Khôi phục" (*new*); `cancel` "Hủy"
  (*new*); the toasts `disabled` "Đã khóa tài khoản." and `restored` "Đã khôi phục tài khoản." (*new*); `backToList`
  "Về danh sách nhân viên" (*new*); `admin.retry` "Thử lại"; `ui.calendar.previousMonth` "Tháng trước" and
  `ui.calendar.nextMonth` "Tháng sau" (*new*; each both the `aria-label` of its button and its tooltip),
  `ui.calendar.nav` "Chọn tháng" (the nav's name), `ui.calendar.today` "hôm nay" and `ui.calendar.selected` "đã chọn"
  (the parts a day button's name adds) (*new*), and a key of the same namespace for any other label the installed
  version reads; `ui.close` "Đóng" (the tooltip of the close button of the dialog and of the sheet);
  `disableDialog.leftDateFuture` "Ngày vào làm sau hôm nay, nên chưa ghi được ngày nghỉ việc." (*new*); for gap 7, not
  built yet: `update` "Cập nhật thông tin", `updateSheet.title` "Cập nhật thông tin nhân viên",
  `updateSheet.description` "Sửa họ tên, mã, số điện thoại và ngày vào làm. Email và vai trò chưa đổi được ở đây.",
  the labels `staff.list.columns.name`, `.employeeCode`, `workPhone`, `staff.list.columns.joinedDate`, the hints
  `updateSheet.nameHint` "Tối đa 100 ký tự.", `updateSheet.employeeCodeHint` "Tối đa 50 ký tự, không để khoảng trắng ở
  đầu hoặc cuối." and `updateSheet.workPhoneHint` "Không bắt buộc; 8 đến 15 chữ số, có thể bắt đầu bằng +.", the
  placeholder `updateSheet.workPhonePlaceholder` "0901234567", `updateSheet.submit` "Lưu" and the toast `updated` "Đã
  cập nhật thông tin." (*new*). The date pickers and the checkbox have no placeholder.
- **Component tree** (DEC-118 (3)), under `web/src/features/identity/staff-accounts/components/`; the route
  `app/(admin)/admin/(shell)/staff/[id]/page.tsx` (its metadata title `staff.account.title`) composes the layout with
  its sections inside `RequirePermission` (`staff.read`) and the client `StaffAccountProvider`
  (`hooks/staff-account-context.tsx`), which holds the one call of `hooks/use-staff-account.ts`: GET
  /api/v1/admin/staff/{id}, the disable and restore mutations of `hooks/use-staff-status.ts`, which the row actions of
  SCR-STF-01 share (by `id`; on 200, and on a 404, a 409 `concurrency-conflict`, a 500 or no answer, each invalidates
  that account's query and the queries of the staff list, so that SCR-STF-01 shows the new status, `docs/FRONTEND.md`,
  *Data from the API*), and whether the user may act (`staff.disable`, and not their own account: `id` of
  `useCurrentUser` from `identity/access`):

  ```text
  staff-account-layout.tsx               slots: heading | nav | details | status | password | activity — a page of
  │                                      up to four cards, its heading above them (Grouping), in one column of at
  │                                      most 1024px, centred (Width); from xl: the heading over a grid of the nav
  │                                      slot (200px, sticky top-6, self-start) and the cards stacked, details,
  │                                      status, password, activity, then an empty `aria-hidden` sentinel div (id
  │                                      `staff-account-end`); below xl the nav slot is hidden with CSS (`hidden
  │                                      xl:block`, so its section stays mounted and the hash still applies), one
  │                                      column, status first so that its action comes before the details, then
  │                                      password and activity; it only places the slots
  ├── staff-account-nav-section.tsx      the nav slot: reads StaffAccountProvider; renders nothing on a 404 or a
  │                                      failed load; while loading and once loaded it builds the items (id info
  │                                      with `staff.account.info.title`, id status with `.status.title`, id
  │                                      password with `staff.account.passwordCard.title`, and id activity with
  │                                      `.activity.title` once gap 10 is built), calls useActiveSection with them
  │                                      and renders SectionNav with the label `admin.sectionNav.label`, the current
  │                                      id and the select handler; scrolls to the hash once the account has loaded
  │   ├── section-nav.tsx                presentational — props: label, items (id, label), currentId, onSelect;
  │   │                                  `nav` aria-labelledby its visible title (`text-xs font-medium`,
  │   │                                  muted-foreground); one link per item to #id, 32px high, radius-md,
  │   │                                  muted-foreground, accent on hover, text wrapping, focus as the base ring of
  │   │                                  button.tsx (`outline-none focus-visible:ring-[3px]
  │   │                                  focus-visible:ring-ring/50`); the current one accent,
  │   │                                  foreground, weight 500, aria-current="location"; a click calls onSelect —
  │   │                                  no shadcn component (moves to components/app/ with SCR-ACC-01)
  │   └── ../hooks/use-active-section.ts the current id: one IntersectionObserver on the sections and the
  │                                      sentinel `staff-account-end` of the layout (the line 24px plus a third
  │                                      of the viewport from the top; the last id at the sentinel only when the
  │                                      document scrolls, else the first; a clicked id until the next scroll,
  │                                      from the build of SCR-ACC-01); select: scrolls to the section
  │                                      (smooth unless reduced motion), focuses its h2 (`<id>-title`) and
  │                                      replaces the hash (moves to @/hooks with SCR-ACC-01)
  ├── staff-account-heading-section.tsx  an Avatar of 64px (56px narrow), kept at the owner's request in addition to
  │                                      the photo of the details card, `aria-hidden` always: with the initials
  │                                      (`initials` of `@/utils/initials`, as `staff-photo.tsx`) and, once gap 8
  │                                      gives a photo, with AvatarImage `alt=""` (the photo of the details card
  │                                      carries `info.photoAlt`); beside it the h1 `name` with the status badge and
  │                                      a muted line `email · employeeCode` (the email alone when the code is
  │                                      empty; it wraps, never cut short); loading: Skeleton of the avatar and the
  │                                      heading; 404: Empty, the problem's `detail` in EmptyTitle as the page's h1
  │                                      (UserXIcon), no description, the link "Về danh sách nhân viên"
  │                                      (ArrowLeftIcon, a Button asChild) to SCR-STF-01; any other problem: Empty
  │                                      with the problem's text in EmptyTitle as the page's h1, destructive,
  │                                      role="alert" (TriangleAlertIcon), and "Thử lại", disabled while the retry
  │                                      is pending; 401 and 403 as *Conventions* says — avatar, badge, skeleton,
  │                                      empty, button
  ├── staff-account-details-section.tsx  Card as a `section` id info, aria-labelledby info-title; CardTitle
  │                                      `info.title` as an h2 with id info-title and tabindex -1, scroll-margin-top
  │                                      24px on the section; CardDescription `info.description`; no CardAction (the
  │                                      owner put the edit action under the information). CardContent is a
  │                                      `@container` sized by the card, not the viewport (the menu changes the
  │                                      card's width): the photo block first in the DOM, then the information
  │                                      column; from `@xl` (36rem) a grid `minmax(0, 1fr) 200px` with a gap of
  │                                      32px, the photo block placed in the second column (`col-start-2
  │                                      row-start-1`) with `@xl:border-l @xl:pl-8 @xl:pt-3` (a hairline, 32px of
  │                                      left padding and its top 12px down to line up with the first row); below
  │                                      `@xl` one column, the photo block centred above the information, with no
  │                                      border or padding. The photo block comes first in the DOM so that the
  │                                      narrow order (photo, then details) is the reading and focus order; at
  │                                      `@xl`, once gaps 7 and 8 are built, Tab reaches "Chọn ảnh" before "Cập nhật
  │                                      thông tin", which is accepted (WCAG 2.4.3 holds: the order keeps the
  │                                      meaning). The information column: a description list (`dl`) of rows divided
  │                                      by the `border` hairline (`divide-y`, each row 12px above and below), each
  │                                      row label 136px | value from `@sm` (24rem), the label over its value below
  │                                      it: Email, Mã nhân viên, SĐT công việc, Vai trò (one Badge secondary each),
  │                                      Ngày vào làm, Ngày nghỉ việc (`dd/MM/yyyy`; "—" when empty); a long email
  │                                      wraps, never cut short; under the list, 20px below, the Button outline "Cập
  │                                      nhật thông tin" (`update`, PencilIcon; start-aligned at its width, full
  │                                      width below `@xl`), which opens the sheet: not built until gap 7 closes, so
  │                                      the first build shows no button. The photo block shows "Chọn ảnh" only with
  │                                      the permission the Decision of gap 8 names — loading: the card with
  │                                      Skeleton of its title, six rows and a round photo; on a 404 or a failed
  │                                      load it renders nothing (the heading section shows the Empty) — card,
  │                                      badge, skeleton, avatar, button
  │   ├── staff-photo.tsx                (moves to components/app/profile-photo.tsx with SCR-ACC-01, its texts
  │   │                                  then props) presentational — props: `name`, `photoUrl?`, `onChoose?`;
  │   │                                  an Avatar of 160px (96px below `@xl`): AvatarFallback with the
  │   │                                  initials (the first
  │   │                                  letter of the first and of the last word of `name` that start with a
  │   │                                  letter; one letter for a one-word name; `initials` of `@/utils/initials`),
  │   │                                  the Avatar `aria-hidden` while it shows them (the name is in the h1);
  │   │                                  AvatarImage with the alt `info.photoAlt` once gap 8 gives a photo; under
  │   │                                  it, when `onChoose` is given, drawn ahead and not built until gap 8 closes
  │   │                                  (a Decision on where the photo is stored, who may change it, its formats
  │   │                                  and size, and its removal on anonymization; the section holds the upload):
  │   │                                  the Button outline size sm "Chọn ảnh" (`info.choosePhoto`, ImageUpIcon;
  │   │                                  full width below `@xl`) calling `onChoose`, with its FieldDescription
  │   │                                  `info.photoHint` tied by aria-describedby; the first build passes neither
  │   │                                  `photoUrl` nor `onChoose` and shows the Avatar alone — avatar, button
  │   └── staff-update-sheet.tsx         gap 7, not built: Sheet (right; bottom on a narrow screen): SheetTitle
  │                                      `updateSheet.title`, SheetDescription `updateSheet.description`; the
  │                                      Fields Họ tên (`nameHint`), Mã nhân viên (`employeeCodeHint`), SĐT công
  │                                      việc (`workPhoneHint`, placeholder `workPhonePlaceholder`, type tel) and
  │                                      Ngày vào làm (a date picker like the dialog's), each hint tied by
  │                                      aria-describedby; Email (gap 5) and Vai trò (gap 2) left out; the corner
  │                                      close button in a Tooltip `ui.close`; footer
  │                                      "Lưu" (default) and "Hủy" (outline); the rules of its endpoint come with
  │                                      the Decision of gap 7 — sheet, field, input, popover, calendar, button
  ├── staff-account-status-section.tsx   Card as a `section` id status, aria-labelledby status-title; CardTitle
  │                                      `status.title` as an h2 with id status-title and tabindex -1 (today's
  │                                      tabIndex stays), scroll-margin-top 24px; CardDescription
  │                                      `status.description`; the row "Trạng thái" with the status badge (the
  │                                      password row moves to the password card); with `staff.disable`, the actions
  │                                      in a row at the end of the card: from xl `justify-end`, each button at its
  │                                      own width, on DISABLED "Khôi phục tài khoản" then "Khóa lại", the hint
  │                                      `disableAgainHint` under the row aligned right; below xl each button full
  │                                      width, stacked; the muted lines `ownAccount` and `anonymized` start-aligned
  │                                      at every width; on ACTIVE "Khóa tài khoản" (destructive-soft, LockIcon),
  │                                      which opens the dialog; on DISABLED "Khôi phục tài khoản" (default,
  │                                      RotateCcwIcon), which opens the confirmation, and "Khóa lại"
  │                                      (destructive-soft, LockIcon) with its hint `disableAgainHint` tied by
  │                                      aria-describedby, which opens the dialog; on the user's own account no
  │                                      action but the muted line `ownAccount`; on ANONYMIZED (Badge outline
  │                                      `status.anonymized`) no action but the muted line `anonymized`; each 200
  │                                      shows the account returned and a toast `disabled` or `restored`; the 409s
  │                                      of the table above in a toast, `concurrency-conflict` also reloads; the 500
  │                                      of "Khóa" in the toast of *Conventions*, then reloads; loading: the card
  │                                      with Skeleton of its title and button; on a 404 or a failed load it renders
  │                                      nothing — card, badge, button, skeleton
  │   ├── staff-disable-dialog.tsx       props: the account (`id`, `name`, `joinedDate`, `leftDate`), `open` and
  │   │                                  `onOpenChange`, so that a row of SCR-STF-01 opens it too; Dialog: DialogTitle
  │   │                                  `disableDialog.title`, DialogDescription `disableDialog.description`, Alert
  │   │                                  destructive (CircleAlertIcon) `disableDialog.warning`; the Field (horizontal)
  │   │                                  Checkbox `disableDialog.leaving` with its FieldDescription `leavingHint`; when
  │   │                                  ticked, the Field `staff.list.columns.leftDate`: a date picker (Popover with a
  │   │                                  Button outline showing `dd/MM/yyyy` and CalendarIcon, holding a Calendar of one
  │   │                                  month, Monday first, `required` so that a second click never empties it), days
  │   │                                  after today in Asia/Ho_Chi_Minh and before the joined date disabled, its
  │   │                                  FieldDescription `leftDateHint` (or `leftDateHintToday`) tied by
  │   │                                  aria-describedby, a 400 or 409 on `#/leftDate` in its FieldError; an account
  │   │                                  that has a left date ("Khóa lại") opens ticked with that date, any other opens
  │   │                                  unticked and ticking it picks today in Asia/Ho_Chi_Minh; a joined date after
  │   │                                  today leaves no day to pick: the checkbox is disabled with the FieldDescription
  │   │                                  `leftDateFuture`; an error no field shows at the top in an Alert; footer "Hủy"
  │   │                                  (outline) and "Khóa" (destructive-soft), disabled with LoaderCircleIcon until
  │   │                                  the answer comes; sends `{ leftDate }`, null when not ticked; closes on 200, on
  │   │                                  a 409 shown in a toast, on a 404, on a 500 and on no answer (those three in
  │   │                                  their toast, then a reload); stays open with a field error — dialog, alert,
  │   │                                  field, checkbox, popover, calendar, button
  │   └── staff-restore-dialog.tsx       props: the account (`id`, `name`), `open` and `onOpenChange`, as the dialog's;
  │                                      AlertDialog: AlertDialogTitle `restoreDialog.title`, AlertDialogDescription
  │                                      `restoreDialog.description`; AlertDialogCancel "Hủy" and "Khôi phục" (a Button
  │                                      default, not AlertDialogAction, which would close at once), disabled with
  │                                      LoaderCircleIcon until the answer comes; closes on 200, and on any problem
  │                                      after its toast (a 404, a 500 or no answer also reloads) — alert-dialog, button
  ├── staff-account-password-section.tsx Card as a `section` id password, aria-labelledby password-title; CardTitle
  │                                      `passwordCard.title` as an h2 with id password-title and tabindex -1,
  │                                      scroll-margin-top 24px; CardDescription `passwordCard.description`; rows
  │                                      like the details card (divided, label 136px | value from `@sm`):
  │                                      `passwordCard.default` with Badge outline `passwordCard.unchanged` when
  │                                      `mustChangePassword`, else Badge secondary `passwordCard.changed`; for gap
  │                                      9, not built until its Decision: the row `passwordCard.lastReset` (the time
  │                                      of the last link, `dd/MM/yyyy HH:mm`, or `passwordCard.neverReset`) and,
  │                                      with the permission the Decision names, in the account's management scope
  │                                      (R-SEC-12), on an ACTIVE account that is not the user's own: the Button
  │                                      outline "Gửi link đặt lại mật khẩu" (KeyRoundIcon; end-aligned from `@xl`,
  │                                      full width below) with its FieldDescription `passwordCard.sendResetHint`
  │                                      tied by aria-describedby, which opens the confirmation; the first build
  │                                      shows the default-password row only; loading: Skeleton of its title and two
  │                                      rows; on a 404 or a failed load it renders nothing — card, badge, button,
  │                                      skeleton
  │   └── staff-reset-password-dialog.tsx gap 9, not built: AlertDialog: AlertDialogTitle `resetDialog.title`,
  │                                      AlertDialogDescription `resetDialog.description` with the account's email;
  │                                      AlertDialogCancel "Hủy" and "Gửi link" (a Button default, disabled with
  │                                      LoaderCircleIcon until the answer comes); the request carries an
  │                                      Idempotency-Key; 202 closes it, shows the toast `resetSent` and the new
  │                                      time of the last link; a 409 or 429 shows its `detail` in a toast —
  │                                      alert-dialog, button
  ├── staff-account-activity-section.tsx gap 10, not built until its Decision, and absent from the navigation until
  │                                      then: Card as a `section` id activity, aria-labelledby activity-title;
  │                                      CardTitle `activity.title` as an h2 (id activity-title, tabindex -1),
  │                                      CardDescription `activity.description`; from `@xl` a filter row that wraps:
  │                                      the date range picker `activity.range` (Popover with a Calendar of two
  │                                      months, default the last 30 days in Asia/Ho_Chi_Minh, a range always
  │                                      required), the Selects `activity.type` and `activity.area` (`activity.all`
  │                                      by default; their values from the Decision), the ToggleGroup
  │                                      `activity.scope` (`scopeAll`, `scopeBy`, `scopeOn`) and the Button ghost
  │                                      `activity.clear`; below `@xl` one Button outline `activity.filters` with
  │                                      the count of filters set, opening a Sheet (bottom) with SheetTitle
  │                                      `activity.filtersSheet.title`, SheetDescription
  │                                      `activity.filtersSheet.description` and the same fields; the filters live
  │                                      in the URL query, so a link keeps them; from `@xl` a Table
  │                                      (`activity.tableLabel`; columns time `dd/MM/yyyy HH:mm`, action, target,
  │                                      area), below `@xl` a list (action · target over time · area); a long target
  │                                      wraps, never cut short; under it the muted line `activity.shown` in an
  │                                      `aria-live="polite"` region and the Button outline `activity.loadMore`,
  │                                      which fetches the next page by cursor and appends it (no page numbers),
  │                                      disabled with LoaderCircleIcon while loading; after each load the focus
  │                                      moves to the first new row (rows take `tabindex="-1"`); at the end of the
  │                                      list the button is replaced by the muted line `activity.end`; no events: an
  │                                      Empty with EmptyTitle `activity.empty`, EmptyDescription
  │                                      `activity.emptyDescription` and the Button `activity.clear`, in place of
  │                                      the table so the filters stay; loading: Skeleton rows; a failed load: the
  │                                      problem's text in an Alert inside the card with "Thử lại" — card, popover,
  │                                      calendar, select, toggle-group, sheet, table, empty, alert, button,
  │                                      skeleton
  └── staff-status-badge.tsx             (SCR-STF-01) presentational — ACTIVE `success` (soft green), DISABLED
                                         `secondary` (grey), and ANONYMIZED `outline` with `status.anonymized` —
                                         badge
  ```
