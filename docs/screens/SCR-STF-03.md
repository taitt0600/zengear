# SCR-STF-03 Staff account

One staff account: its staff profile (Hồ sơ nhân viên) and its sign-in account, with the actions to approve or reject
it while it waits for approval (UC-140) and to disable or restore it (UC-005, UC-006).

Loaded from `GET /api/v1/admin/staff/{id}`: name, email, employee code, joined date, left date, work phone, status, roles,
and whether the default password is changed (`mustChangePassword`). No field is editable in Phase 1 (STF-01 creates,
STF-02 disables and restores, STF-09 approves and rejects); the action "Cập nhật thông tin" is designed ahead and
waits for gap 7, the photo for gap 8 (STF-10), the password reset link for gap 9 and the activity card for gap 10.

The page groups what it shows by where it belongs (the owner's request of 2026-11-13): the card "Hồ sơ nhân viên" holds
the staff profile (name, employee code, work phone, joined date, left date, and the photo of gap 8) and the card "Tài
khoản" the sign-in account (email, roles, status, the default password, and the reset link of gap 9). An action sits
in the row it changes: "Khóa tài khoản", "Khôi phục tài khoản" and "Khóa lại" in the row of the status.

**The HR profile** (Hồ sơ nhân sự, STF-10, DEC-130, R-USR-18, R-SEC-15): for a user whose `permissions` hold
`staff.pii.read`, four cards follow the card "Tài khoản": "Thông tin cá nhân" (the name on the identity document, the
birth date, the gender, the CCCD or passport with its date and place of issue, the permanent address), "Liên lạc và
liên hệ khẩn cấp" (personal phone, personal email, current address and at most 2 emergency contacts, which the staff
member edits on SCR-ACC-01 and this page only shows), "Công việc và hợp đồng" (job title, direct manager, contract) and
"Hồ sơ số" (the documents, with their status). The API sends the masked form of the document number, the birth date,
the phones, the personal email and the addresses; "Hiện" beside a value asks the API for that one field, which the API
logs and limits to 30 an hour, and the page masks it again after 30 seconds (DEC-130 (4), (5)); the full value is kept
in the page only, never in a cache, the URL or storage. With `staff.pii.update` the user also edits the personal
information in a sheet, where a masked field left empty keeps its value. Nobody without `staff.pii.read` sees these
cards. An account has no HR profile until the staff member has confirmed the data-processing notice on SCR-ACC-01
(DEC-130 (7)): until then one card says so. The profile loads apart from the account, so a failed load of the profile
leaves the rest of the page. Its endpoints, and the texts of the problems they answer, come with STF-10.

**A PENDING account** (STF-09, R-USR-17, UC-140 steps 2 and 3): the heading and the status row show the badge "Chờ
duyệt", and a notice under the heading, before the cards, says that the account waits for approval, who created it
and when (`createdBy`, `createdAt`, DEC-137 (1)) and that the staff member cannot sign in until then. Nothing disables
or restores it (the API answers 409, R-USR-17), so the status row has no action. The notice holds the actions "Từ
chối" and "Duyệt" only when `canApprove` is true: the API works out who may approve (a holder of `staff.approve` who
did not create the account and manages all its roles, R-USR-17, R-SEC-12). For anyone else the notice says that the
account waits for another person's approval. Each action opens the confirmation of [SCR-STF-01](SCR-STF-01.md) (the
same two dialogs, which repeat the name, the email, the roles and the creator). "Duyệt" sends
`POST /api/v1/admin/staff/{id}/approve`: after a 200 a toast says "Đã duyệt tài khoản {name}." with "Nhân viên đăng
nhập được bằng mật khẩu mặc định.", the notice goes and the page shows the ACTIVE account. "Từ chối" sends
`POST /api/v1/admin/staff/{id}/reject`: after a 200 or 204 the account no longer exists, so the page goes to
SCR-STF-01 on its tab Chờ duyệt (`/admin/staff?status=PENDING`, replacing the history entry) with the toast "Đã từ
chối tài khoản {name}.". "Gửi link đặt lại mật khẩu" (gap 9) shows on an ACTIVE account only.

Until gap 14 closes, the account of `GET /api/v1/admin/staff/{id}` has no `canApprove`, `createdBy` or `createdAt`:
the two actions show for a user whose `permissions` hold `staff.approve` (only to hide them, `docs/FRONTEND.md`) and
the sentence about another person for a user without it, the sentence about the creator and the creator row of the
confirmations are left out, and an approval the API refuses (the caller's own request, a role outside the caller's
scope) shows its 409 `detail`.

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
| 404 or 409 on "Duyệt" or "Từ chối" (not PENDING any more, the caller's own request, an email taken in Firebase, a role outside the caller's scope) | Its `detail` in a toast; the confirmation closes and the account reloads (an account rejected meanwhile then shows the 404 of the first row) |
| 500 or no answer on "Duyệt" or "Từ chối" | The web's texts of [*Conventions*](conventions.md) in a toast; the confirmation closes and the account reloads |

## Layout

- **Design**: <https://claude.ai/artifact/TqWKuUJDPivBNDfNJrosMP>, drawn 2026-11-13 with the design system of
  2026-11-13; approved by the merge of the pull request that wrote this section (DEC-119). At the owner's request
  (2026-11-13: usability, consistency, visual hierarchy, feedback, accessibility, the user first, and the information
  by the staff profile) it regroups the page, adds the PENDING account of STF-09 (UC-140) and the HR profile of
  STF-10 (DEC-130): the cards "Hồ sơ nhân viên" (the staff profile), "Tài khoản" (the sign-in account, which takes the
  rows of the status card and of the password card), the HR profile and "Hoạt động"; each action in the row or the
  card it changes, every card's edit action in its header; and, on a PENDING account, a notice with the approval
  before the cards. The four cards of the HR profile and what each shows, masks and lets edit follow the field
  classification the owner approved on <https://claude.ai/artifact/1YzxwMAsQ2gS2qL5MQ2tVi> (its artboard "Admin"). It keeps, from the Layout of 2026-11-11
  (<https://claude.ai/artifact/QjTnaq2rsB49xBeN3Wkuen>, pull request #131, not built yet): the centred column with the
  section navigation, the heading with its small Avatar, the photo in the first card, the
  activity card, the 404 and error artboards and the confirmation "Gửi link đặt lại mật khẩu?"; and, from
  <https://claude.ai/artifact/DuLX9hmqGWyyP9vmoUw6Dd> (pull request #111), the dialog "Khóa tài khoản", the
  confirmation "Khôi phục tài khoản?" and the sheet "Cập nhật thông tin". The two confirmations of the approval, the
  badge "Chờ duyệt" and the toasts are those of the Layout of [SCR-STF-01](SCR-STF-01.md)
  (<https://claude.ai/artifact/VGfMXvw1CSvzx3ptVKWHbZ>), drawn again only to show them on this page. The canvas shows
  the frame of SCR-ADM-01 for context only; this Layout does not approve it.
- **Builds**, each part by its feature, on the page as it is then:
  - `layout-scr-stf-03` (the Next row of `docs/STATUS.md`, a `feat(web): … (STF-02)` pull request): the section
    navigation, the width, the cards "Hồ sơ nhân viên" and "Tài khoản" with the status actions in the status row and
    the row of the default password, loading, 404 and error.
  - STF-09 (its web part, with its part of SCR-STF-01, since both screens share the two dialogs and
    `hooks/use-staff-approval.ts`): the notice, the PENDING badge, the status row of a PENDING account. Before
    `layout-scr-stf-03` is done it places the notice above the cards the page has and takes the actions out of the
    status card of a PENDING account. The creator and `canApprove` wait for gap 14.
  - STF-10: the four cards of the HR profile with their states, "Hiện" and the sheet "Sửa thông tin cá nhân", and
    the photo with "Chọn ảnh" (gap 8). The sheet of "Công việc và hợp đồng", the dialog of "Tải lên tài liệu" and
    the confirmations of "Duyệt" and "Từ chối" of a document are not drawn: their buttons are, and a Layout pull
    request of this screen draws them before the web part of STF-10 builds them.
  - STF-11: "Cập nhật thông tin" and its sheet (gap 7); the role editing and the email dialog need a Layout pull
    request first (gaps 5 and 7).
  - STF-12: the row of the last reset link, "Gửi link đặt lại mật khẩu" and its confirmation (gap 9), after the
    Layout pull request that changes its two texts.
  - STF-13: the activity card (gap 10).
- **Artboards**, desktop 1440 px unless named. Of the PENDING sample "Thu ngân mới (fixture)" (the sample of the
  canvas of SCR-STF-01, not a fixture yet): the page signed in as the fixture admin, whose `canApprove` is true, with
  "Từ chối", "Duyệt" and the section navigation working in Play, and the card of an account without an HR profile;
  the same page for the fixture IT Support, who created the account and holds no `staff.pii.read`: the notice without
  actions, no HR profile; the confirmations "Duyệt tài khoản này?" and "Từ chối tài khoản này?" over the page; after
  an approval, the ACTIVE account with "Khóa tài khoản" in the status row, "Gửi link đặt lại mật khẩu" in its row and
  the toast; the DISABLED account with "Khôi phục tài khoản" and "Khóa lại"; dark, of the PENDING account. Of the
  fixture cashier with a sample HR profile (not a real person's data), signed in as the fixture admin with
  `staff.pii.read` and `staff.pii.update`: the page with the four cards, the CCCD revealed with its countdown ("Ẩn"
  and "Hiện" work in Play); the sheet "Sửa thông tin cá nhân", with the tooltip of its close button open; dark
  (DEC-123 (5): the soft badges, `destructive-soft`, the revealed value on `accent`). States: loading of the account
  (the navigation shows, the cards as Skeleton); of the HR profile, one artboard with its loading card, its failed
  load, the contact card with nothing entered and the documents card without a document. Narrow (390 px): the
  PENDING account (no section navigation, the notice first with its two buttons side by side, the card's edit action
  under its description at full width) and the four cards of the HR profile (the label over the value, "Hiện" at the
  end of the value's line, the documents as a list). 404 and error of the account keep the canvas of 2026-11-11 (an
  Empty in the column, no navigation), and the wide screen its 2560 px artboard (the column keeps 1024 px and
  centres). No permission is the page of SCR-ADM-01 (`RequirePermission` with `staff.read`). After a rejection the
  page is SCR-STF-01, not drawn again. Drawn ahead and not built until their features: "Cập nhật thông tin" (gap 7),
  the row of the last reset link with "Gửi link đặt lại mật khẩu" (gap 9) and the activity card (gap 10). The only
  icon-only button is the corner close button of the sheet, drawn with its tooltip open.
- **shadcn**: no `add` for what the first build makes (the notice is the installed `alert`); `shadcn add toggle-group` comes with the activity card of gap
  10, the only part that needs a component not installed. The section navigation is plain links on the tokens (shadcn
  has no table of contents; `sidebar` is the app's menu and too heavy here). It lives in this module
  (`components/section-nav.tsx`, `hooks/use-active-section.ts`) while only this screen uses it (DEC-118 (1),
  `docs/FRONTEND.md`, *Modules and boundaries*); the pull request that builds the Layout of SCR-ACC-01, which takes
  the same layout, moves both to `components/app/` and `@/hooks`.
- **Theme**: unchanged and built: the soft status badges (`success` on `success-soft`, DISABLED `secondary`,
  ANONYMIZED `outline`) and the Button variant `destructive-soft` of the previous Layout of this screen are in
  `app/globals.css` and `components/ui/` since the pull request of STF-02, with their `.dark` values since AUT-04
  (DEC-123 (4), DEC-124). The PENDING badge is `outline` with ClockIcon, as on SCR-STF-01. On this page "Duyệt" is
  the Button `default` (the main action of the view, as the submit of its confirmation), where the rows of
  SCR-STF-01 use `outline`; "Từ chối" is `destructive-soft` on both. The current link of the section navigation is `accent` with `foreground` text at weight 500;
  the others `muted-foreground` (4.98:1 light, 6.72:1 dark on `background`), on hover `accent` (`muted-foreground` on
  `accent` 4.56:1 light, 4.83:1 dark).
- **Section navigation** (new behaviour, so the build is a `feat(web): … (STF-02)` pull request on
  `feat/stf-02-web-2`, the next free name since `feat/stf-02-web` has merged, CLAUDE.md *Workflow*; as the paragraph
  under the [screen list](README.md#screen-list) says): from `xl` (1280px) a `nav` labelled by its visible title "Trên trang này"
  (`admin.sectionNav.label`, `text-xs font-medium`, `muted-foreground`) sits left of the cards in a column of 200px,
  `sticky top-6` (24px, the page padding; the header of SCR-ADM-01 scrolls with the page) with `self-start` on its
  grid cell, and lists one link per card, in page order, with the card's title as its text
  (`staff.account.info.title`, `.accountCard.title`, with STF-10 the titles of the HR profile's cards for a holder
  of `staff.pii.read`, and `.activity.title`; the last only once gap 10 is built); a link text wraps, never cut short. It is navigation, not content, so it sits on the page ground like the
  menu of SCR-ADM-01, an exception to **Grouping**. Each card is a `section` with an `id` (`info`, `account`, `hr` or `personal`, `contact`, `work` and `documents`,
  `activity`), `aria-labelledby` its title and `scroll-margin-top` 24px; the titles are `h2` with the ids
  `<id>-title` and `tabindex="-1"`. A click scrolls to the card (smoothly, at once under `prefers-reduced-motion`),
  moves the focus to its `h2` and replaces the URL hash without a new history entry. While the page scrolls, the
  current link (`aria-current="location"`) is the last card whose top has passed a line 24px plus a third of the
  viewport from the top; when the document scrolls at all (`scrollHeight` above the viewport), the last card is
  current once the sentinel after it (`staff-account-end`, an empty `aria-hidden` div of the layout) comes into view;
  when it does not scroll, the first card stays current (from the build of SCR-ACC-01, a clicked card stays current
  until the next scroll moves the line, see its Layout). One IntersectionObserver watches the sections and the
  sentinel; no handler on every scroll frame. A hash on load (`#<id>`) is applied once the account has loaded, not
  while the Skeleton shows. Below `xl` the navigation is hidden with CSS (its section stays mounted, so a hash still
  scrolls) and the cards keep their order: on a PENDING account the notice, which holds the approval, comes before
  them at every width. The notice is no card of the navigation.
- **Width**: the page's content is one column of at most 1024px (`max-w-5xl`), centred in the page area of SCR-ADM-01
  (24px padding, 16px narrow); from `xl` it holds the heading over a grid `200px minmax(0, 1fr)` with a gap of 32px
  (navigation | cards), so a card is about 792px wide at most, whatever the screen. `xl` rather than `lg`, because at
  1024px with the menu expanded the card column would be about 488px.
- **Texts of the HR profile** (`messages/vi.json`, STF-10), all *new*, under `staff.profile`: the card of an account
  without a profile `hr.title` "Hồ sơ nhân sự", `hr.description` "Thông tin cá nhân, liên lạc, hợp đồng và giấy tờ của
  nhân viên, chỉ người có quyền xem hồ sơ mới thấy.", its Empty `hr.empty` "Chưa có hồ sơ nhân sự" with
  `hr.emptyDescription` "Hồ sơ được tạo sau khi nhân viên đăng nhập và xác nhận thông báo xử lý dữ liệu cá nhân trong
  Tài khoản của tôi."; `edit` "Sửa"; the reveal `reveal.show` "Hiện" and `reveal.hide` "Ẩn" with their `aria-label`s
  `reveal.showLabel` "Hiện {field}" and `reveal.hideLabel` "Ẩn {field}", `reveal.countdown` "Tự che sau {seconds}
  giây", the announcements `reveal.shown` "Đã hiện {field}." and `reveal.hidden` "Đã che {field}.", and `reveal.limit`
  "Bạn hiện được tối đa 30 lần mỗi giờ. Giá trị tự che lại sau 30 giây."; `personal.title` "Thông tin cá nhân",
  `personal.description` "Theo giấy tờ tùy thân. Giá trị được che; mỗi lần bấm Hiện được ghi lại và nhân viên xem
  được.", `personal.legalName` "Họ tên theo giấy tờ", `personal.birthDate` "Ngày sinh", `personal.gender` "Giới tính"
  with `personal.genderValue.*` (the values of STF-10), `personal.idType.cccd` "CCCD" and `.passport` "Hộ chiếu",
  `personal.idNumber` "số {type}" (the field's name in an `aria-label`), `personal.issued` "Ngày cấp, nơi cấp",
  `personal.permanentAddress` "Địa chỉ thường trú"; the sheet `personalSheet.title` "Sửa thông tin cá nhân",
  `personalSheet.description` "Nhập theo giấy tờ tùy thân của {name}. Ô đang che để trống thì giữ nguyên giá trị cũ.",
  the hint `personalSheet.legalNameHint` "Viết đủ dấu, đúng như trên giấy tờ. Tối đa 100 ký tự.", the date picker's
  text `personalSheet.pickDate` "Chọn ngày" with the hint `personalSheet.birthDateHint` "Đang lưu: {masked}. Chỉ chọn
  khi cần đổi.", `personalSheet.idType` "Loại giấy tờ", `personalSheet.idNumber` "Số giấy tờ" with its placeholder
  `personalSheet.idNumberPlaceholder` "012345678901" and its hint `personalSheet.idNumberHint` "Đang lưu: {masked}. CCCD
  gồm 12 chữ số. Để trống nếu không đổi.", `personalSheet.issuedDate` "Ngày cấp", `personalSheet.issuedPlace` "Nơi
  cấp", the address's hint `personalSheet.keepHint` "Đang lưu: {masked}. Để trống nếu không đổi.",
  `personalSheet.submit` "Lưu" and the toast `personalSheet.saved` "Đã lưu thông tin cá nhân." (the lengths and
  formats are those the endpoint of STF-10 checks; the run that builds it corrects a hint that differs); the selects
  and the date pickers have no placeholder; `contact.title` "Liên lạc và liên hệ khẩn cấp", `contact.description`
  "Nhân viên tự cập nhật trong Tài khoản của tôi; ở đây chỉ xem.", `contact.personalPhone` "SĐT cá nhân",
  `contact.personalEmail` "Email cá nhân", `contact.currentAddress` "Chỗ ở hiện tại", `contact.emergency` "Liên hệ
  khẩn cấp" with `contact.emergencyValue` "{name} · {relation}", `contact.emergencyPhoneLabel` "SĐT của {name}" and
  `contact.noEmergency` "Nhân viên chưa thêm người liên hệ khẩn cấp."; `work.title` "Công việc và hợp đồng",
  `work.description` "Chức danh, quản lý trực tiếp và hợp đồng hiện hành.", `work.jobTitle` "Chức danh",
  `work.manager` "Quản lý trực tiếp", `work.contract` "Hợp đồng" with `work.contractValue` "{type} · {start} – {end}"
  and the types `work.contractType.probation` "Thử việc", `.fixedTerm` "Xác định thời hạn" and `.indefinite` "Không
  xác định thời hạn"; `documents.title` "Hồ sơ số", `documents.description` "Hợp đồng, giấy tờ và bằng cấp. Mỗi lần mở
  tệp được ghi lại; đường dẫn mở tệp hết hạn sau 5 phút.", `documents.upload` "Tải lên tài liệu",
  `documents.tableLabel` "Hồ sơ số của nhân viên", the columns `documents.columns.name` "Tài liệu", `.type` "Loại",
  `.uploadedAt` "Ngày tải lên", `.status` "Trạng thái" and `.actions` "Thao tác", the types `documents.type.contract`
  "Hợp đồng", `.appendix` "Phụ lục", `.decision` "Quyết định", `.identity` "Giấy tờ tùy thân", `.resume` "Sơ yếu lý
  lịch" and `.certificate` "Bằng cấp, chứng chỉ", the Badges `documents.status.approved` "Đã duyệt" and `.pending`
  "Chờ duyệt", `documents.view` "Xem", `documents.approve` "Duyệt" and `documents.reject` "Từ chối" with their
  `aria-label`s `documents.viewLabel` "Xem {file}", `.approveLabel` "Duyệt {file}" and `.rejectLabel` "Từ chối
  {file}", and the Empty `documents.empty` "Chưa có tài liệu nào" with `documents.emptyDescription` "Tải lên hợp đồng,
  quyết định, giấy tờ tùy thân hoặc bằng cấp của nhân viên. PDF, JPG hoặc PNG, tối đa 10 MB."; `admin.retry` and
  `ui.close` (existing).
- **Texts** (`messages/vi.json`), new ones marked *new*, under `staff.account` unless named: for the PENDING account
  (STF-09): the notice's title `pendingNotice.title` "Tài khoản đang chờ duyệt" (*new*) and the sentences of its
  description, in this order: `pendingNotice.createdBy` "{name} tạo lúc {time}." (*new*; gap 14; the time `dd/MM/yyyy
  HH:mm`; left out for an account without a creator), `status.pendingDescription` "Nhân viên chưa đăng nhập được cho
  tới khi tài khoản được duyệt." (*new*; also the hint of the status row), then `pendingNotice.review` "Kiểm tra hồ sơ
  và vai trò bên dưới trước khi duyệt." (*new*) for a user who may approve, or `pendingNotice.other` "Tài khoản này
  chờ một người khác duyệt: người duyệt không phải là người tạo và phải quản lý được mọi vai trò của tài khoản."
  (*new*); and, from the Layout of SCR-STF-01, which lists them as new and whose build of STF-09 adds them: the badge
  `staff.list.status.pending` "Chờ duyệt", the buttons `approve` "Duyệt" and `reject` "Từ chối", the confirmations
  `approveDialog.title`, `.description` and `.submit`, `rejectDialog.title`, `.description` and `.submit` with the
  rows `summary.name`, `.email`, `.roles`, `.createdBy` and `.createdByValue`, and the toasts `approved` with
  `approvedHint` and `rejected`; `admin.sectionNav.label`
  "Trên trang này" (*new*; the visible title of the section navigation, which names the `nav`); for gap 8, not built
  yet: `info.photoAlt` "Ảnh của {name}" (*new*; the alt of the photo once there is one), `info.choosePhoto` "Chọn ảnh"
  and its hint `info.photoHint` "Ảnh chân dung, rõ mặt. JPG hoặc PNG, tối đa 2 MB." (*new*; the formats and the 2 MB
  the owner chose on 2026-11-11, which the Decision of gap 8 records); the password rows of the account card (the keys `passwordCard.title` and
  `passwordCard.description` of the Layout of 2026-11-11 are not added, since the password card is gone): the row
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
  viên" (*new*); the details card `info.title` "Hồ sơ nhân viên" (changed from "Thông tin nhân viên": the
  term of `docs/GLOSSARY.md`) and `info.description` "Thông tin của nhân viên tại cửa hàng, dùng để liên hệ trong
  công việc." (changed from "Thông tin ghi khi tạo tài khoản, dùng để liên hệ và phân quyền."); its
  labels `staff.list.columns.name`, `.employeeCode`, `.joinedDate`, `.leftDate`, with `workPhone` "SĐT công việc",
  and `staff.list.noValue` "—" for an empty value; the account card `accountCard.title` "Tài khoản" and
  `accountCard.description` "Email đăng nhập, vai trò, trạng thái và mật khẩu của tài khoản." (*new*), its labels
  `accountCard.email` "Email đăng nhập" (*new*), `staff.list.columns.roles` and `staff.list.columns.status`, the
  badges `staff.list.status.active` and `.disabled`, with `status.anonymized` "Đã ẩn danh", and the hint of the
  status row `status.description` "Tài khoản bị khóa không đăng nhập được; khôi phục để nhân viên dùng lại." (on
  ACTIVE and DISABLED; it was the description of the status card) or `status.pendingDescription` (PENDING); (the key
  `status.title` goes with the status card, and the keys `password` and `mustChangePassword` since the password row
  takes `passwordCard.default`); the actions
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
  *Data from the API*), whether the user may act (`staff.disable`, and not their own account: `id` of
  `useCurrentUser` from `identity/access`) and, with STF-09, whether the user may approve a PENDING account (its
  `canApprove`; until gap 14 closes, `staff.approve` in the user's `permissions`). The HR profile of STF-10 is a
  second request, held by its own section:

  ```text
  staff-account-layout.tsx               slots: heading | notice | nav | details | account | hr | activity — a page
  │                                      of cards, its heading above them (Grouping), in one column of at most
  │                                      1024px, centred (Width); the notice slot under the heading, at the column's
  │                                      width; from xl: a grid of the nav slot (200px, sticky top-6, self-start) and
  │                                      the cards stacked, details, account, hr, activity, then an empty
  │                                      `aria-hidden` sentinel div (id `staff-account-end`); below xl the nav slot
  │                                      is hidden with CSS (`hidden xl:block`, so its section stays mounted and the
  │                                      hash still applies), one column in the same order; it only places the slots
  ├── staff-account-nav-section.tsx      the nav slot: reads StaffAccountProvider; renders nothing on a 404 or a
  │                                      failed load; while loading and once loaded it builds the items, one per card that shows,
  │                                      in page order (id info with `staff.account.info.title`, id account with
  │                                      `.accountCard.title`; with STF-10, for a holder of `staff.pii.read`, id hr
  │                                      with `staff.profile.hr.title` while the account has no HR profile or its
  │                                      load failed, else ids personal, contact, work and documents with their
  │                                      titles; id activity with `.activity.title` once gap 10 is built), calls useActiveSection with them
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
  ├── staff-account-notice-section.tsx   (STF-09) the notice slot, only on a PENDING account: an Alert (ClockIcon;
  │                                      shadcn's `role="alert"`) with AlertTitle `pendingNotice.title` and an
  │                                      AlertDescription of sentences: `pendingNotice.createdBy` with the creator's
  │                                      name and the time (gap 14; none without a creator),
  │                                      `status.pendingDescription`, then `pendingNotice.review` when the user may
  │                                      approve (the provider) or `pendingNotice.other`; when the user may approve,
  │                                      at the end of the alert "Từ chối" (`reject`, destructive-soft) then "Duyệt"
  │                                      (`approve`, default, CheckIcon): from md beside the text, below md under it,
  │                                      side by side at half the width each; they open the two dialogs below; after
  │                                      a 200 of "Duyệt" the account is ACTIVE and the notice is gone; renders
  │                                      nothing while loading, on a 404 or on a failed load — alert, button
  │   ├── staff-approve-dialog.tsx       (SCR-STF-01, not a second version) opened with the account of this page:
  │   │                                  AlertDialog `approveDialog.*` with the `dl` `summary.*` (the creator row
  │   │                                  with gap 14) — alert-dialog
  │   ├── staff-reject-dialog.tsx        (SCR-STF-01) the same with `rejectDialog.*` — alert-dialog
  │   └── ../hooks/use-staff-approval.ts (SCR-STF-01) the two requests, their toasts and the invalidation of the
  │                                      list and of the count; for this page it also invalidates the account's
  │                                      query after an approval and on a 404, a 409, a 500 or no answer, and takes
  │                                      an optional `onRejected`: this section passes one that removes the
  │                                      account's query (no refetch, which would show the 404) and replaces the
  │                                      route with `/admin/staff?status=PENDING`, where the toast `rejected` shows
  ├── staff-account-details-section.tsx  the card "Hồ sơ nhân viên": Card as a `section` id info, aria-labelledby
  │                                      info-title; CardTitle `info.title` as an h2 with id info-title and tabindex
  │                                      -1, scroll-margin-top 24px on the section; CardDescription
  │                                      `info.description`; CardAction: the Button outline size sm "Cập nhật thông
  │                                      tin" (`update`, PencilIcon), which opens the sheet: not built until gap 7
  │                                      closes, so the first build shows no button (every card of this page has its
  │                                      edit action in CardAction; below `@xl` the CardAction goes under the
  │                                      description at full width). CardContent is a `@container` sized by the
  │                                      card, not the viewport (the menu changes the card's width): the photo block
  │                                      first in the DOM, then the information column; from `@xl` (36rem) a grid
  │                                      `minmax(0, 1fr) 200px` with a gap of 32px, the photo block placed in the
  │                                      second column (`col-start-2 row-start-1`) with `@xl:border-l @xl:pl-8
  │                                      @xl:pt-3`; below `@xl` one column, the photo block centred above the
  │                                      information, with no border or padding. The information column: a
  │                                      description list (`dl`) of rows divided by the `border` hairline
  │                                      (`divide-y`, each row 12px above and below), each row label 136px | value
  │                                      from `@sm` (24rem), the label over its value below it: Họ tên, Mã nhân
  │                                      viên, SĐT công việc, Ngày vào làm, Ngày nghỉ việc (`dd/MM/yyyy`; "—" when
  │                                      empty). The photo block shows "Chọn ảnh" only with `staff.pii.update`
  │                                      (DEC-130 (3)), from STF-10 — loading: the card with Skeleton of its title,
  │                                      five rows and a round photo; on a 404 or a failed load it renders nothing
  │                                      (the heading section shows the Empty) — card, skeleton, avatar, button
  │   ├── staff-photo.tsx                (moves to components/app/profile-photo.tsx with SCR-ACC-01, its texts
  │   │                                  then props) presentational — props: `name`, `photoUrl?`, `onChoose?`;
  │   │                                  an Avatar of 160px (96px below `@xl`): AvatarFallback with the
  │   │                                  initials (the first
  │   │                                  letter of the first and of the last word of `name` that start with a
  │   │                                  letter; one letter for a one-word name; `initials` of `@/utils/initials`),
  │   │                                  the Avatar `aria-hidden` while it shows them (the name is in the h1);
  │   │                                  AvatarImage with the alt `info.photoAlt` once gap 8 gives a photo; under
  │   │                                  it, when `onChoose` is given, built by STF-10 (gap 8: DEC-130 and DEC-131
  │   │                                  settle where the photo is stored, who may change it, its formats and size;
  │   │                                  the section holds the upload):
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
  ├── staff-account-login-section.tsx    the card "Tài khoản", in place of the status card and of the password card
  │                                      (`staff-account-status-section.tsx` becomes this file): Card as a `section`
  │                                      id account, aria-labelledby account-title; CardTitle `accountCard.title` as
  │                                      an h2 with id account-title and tabindex -1, scroll-margin-top 24px;
  │                                      CardDescription `accountCard.description`; a `dl` of rows divided like the
  │                                      details card, each row label 136px | value | action from `@sm`, below it the
  │                                      label, the value, then the action at full width: `accountCard.email` with
  │                                      the email (it wraps, never cut short); `staff.list.columns.roles` with one
  │                                      Badge secondary per role; `staff.list.columns.status` with the status badge
  │                                      and under it the hint `status.description` (ACTIVE, DISABLED) or
  │                                      `status.pendingDescription` (PENDING) and, with `staff.disable`, its
  │                                      actions at the end of the row: on ACTIVE "Khóa tài khoản" (destructive-soft,
  │                                      LockIcon), aria-describedby the hint, which opens the dialog; on DISABLED
  │                                      "Khôi phục tài khoản" (default, RotateCcwIcon), which opens the
  │                                      confirmation, then "Khóa lại" (destructive-soft, LockIcon) with its hint
  │                                      `disableAgainHint` under the status hint, tied by aria-describedby, which
  │                                      opens the dialog; on PENDING no action (the notice holds the approval); on
  │                                      the user's own account no action but the muted line `ownAccount` under the
  │                                      badge; on ANONYMIZED (Badge outline `status.anonymized`) no action but the
  │                                      muted line `anonymized`; `passwordCard.default` with Badge outline
  │                                      `passwordCard.unchanged` when `mustChangePassword`, else Badge secondary
  │                                      `passwordCard.changed`; for gap 9, not built until STF-12: the row
  │                                      `passwordCard.lastReset` (the time of the last link, `dd/MM/yyyy HH:mm`, or
  │                                      `passwordCard.neverReset`) and, with the permission of DEC-133, in the
  │                                      account's management scope (R-SEC-12), on an ACTIVE account that is not the
  │                                      user's own: at the end of that row the Button outline "Gửi link đặt lại mật
  │                                      khẩu" (KeyRoundIcon) with its hint `passwordCard.sendResetHint` under the
  │                                      value, tied by aria-describedby, which opens the confirmation; each 200
  │                                      shows the account returned and a toast `disabled` or `restored`; the 409s of
  │                                      the table above in a toast, `concurrency-conflict` also reloads; the 500 of
  │                                      "Khóa" in the toast of *Conventions*, then reloads; loading: the card with
  │                                      Skeleton of its title and four rows; on a 404 or a failed load it renders
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
  │   ├── staff-restore-dialog.tsx       props: the account (`id`, `name`), `open` and `onOpenChange`, as the dialog's;
  │   │                                  AlertDialog: AlertDialogTitle `restoreDialog.title`, AlertDialogDescription
  │   │                                  `restoreDialog.description`; AlertDialogCancel "Hủy" and "Khôi phục" (a Button
  │   │                                  default, not AlertDialogAction, which would close at once), disabled with
  │   │                                  LoaderCircleIcon until the answer comes; closes on 200, and on any problem
  │   │                                  after its toast (a 404, a 500 or no answer also reloads) — alert-dialog, button
  │   └── staff-reset-password-dialog.tsx gap 9, not built: AlertDialog: AlertDialogTitle `resetDialog.title`,
  │                                      AlertDialogDescription `resetDialog.description` with the account's email;
  │                                      AlertDialogCancel "Hủy" and "Gửi link" (a Button default, disabled with
  │                                      LoaderCircleIcon until the answer comes); the request carries an
  │                                      Idempotency-Key; 202 closes it, shows the toast `resetSent` and the new
  │                                      time of the last link; a 409 or 429 shows its `detail` in a toast —
  │                                      alert-dialog, button
  ├── staff-hr-section.tsx               (STF-10) the hr slot, only for a holder of `staff.pii.read` (the API sends
  │                                      nobody else this group, R-SEC-15); it holds the one call of
  │                                      `hooks/use-staff-hr-profile.ts`, the profile of STF-10 (its endpoints come
  │                                      with the feature), apart from the account's, so that the rest of the page
  │                                      shows without it. No profile yet (the staff member has not confirmed the
  │                                      data-processing notice, DEC-130 (7)): one Card as a `section` id hr, CardTitle
  │                                      `staff.profile.hr.title` (h2, id hr-title, tabindex -1), CardDescription
  │                                      `hr.description`, and an Empty (FileTextIcon) with EmptyTitle `hr.empty` and
  │                                      EmptyDescription `hr.emptyDescription`, no action. Loading: one card with
  │                                      Skeleton of its title and four rows, `aria-busy`. A failed load: the card
  │                                      `hr.title` with an Empty, the problem's text in EmptyTitle (destructive,
  │                                      role="alert", TriangleAlertIcon), no description, and "Thử lại"
  │                                      (`admin.retry`), disabled while the retry is pending. Else the four cards
  │                                      below, in this order — card, empty, skeleton, button
  │   ├── staff-hr-personal-card.tsx     Card as a `section` id personal: CardTitle `personal.title`,
  │   │                                  CardDescription `personal.description`; CardAction, with `staff.pii.update`:
  │   │                                  the Button outline sm "Sửa" (`staff.profile.edit`, PencilIcon), which opens
  │   │                                  the sheet; a `dl` of MaskedField rows: `personal.legalName`,
  │   │                                  `personal.birthDate` (masked: the year hidden), `personal.gender`, the
  │   │                                  document's type as the label ("CCCD" or "Hộ chiếu") with its number (masked:
  │   │                                  the last 3 characters), `personal.issued` (date · place),
  │   │                                  `personal.permanentAddress` (masked); under the list the muted line
  │   │                                  `reveal.limit`; an empty value shows `staff.list.noValue` and no button
  │   │                                  — card, button
  │   ├── staff-masked-field.tsx         presentational — props: `label`, `value` (masked, as the API sends it),
  │   │                                  `revealed?` (the full value), `secondsLeft?`, `pending`, `onReveal?`,
  │   │                                  `onHide`; one row, label 136px | value | action from `@sm`, below it the
  │   │                                  label over the value with the action at the end of the value's line; the
  │   │                                  value in tabular numbers; with `onReveal`, the Button ghost sm "Hiện"
  │   │                                  (`reveal.show`, EyeIcon; `aria-label` `reveal.showLabel` with the field's
  │   │                                  name), disabled with LoaderCircleIcon while the request runs; revealed: the
  │   │                                  full value on `accent` (radius-sm), the muted text `reveal.countdown`
  │   │                                  (not a live region) and "Ẩn" (`reveal.hide`, EyeOffIcon, `reveal.hideLabel`);
  │   │                                  the reveal and the re-mask are announced once in a polite live region
  │   │                                  (`reveal.shown`, `reveal.hidden`) — button
  │   ├── ../hooks/use-reveal-field.ts   one field at a time per row: sends the reveal of STF-10 with the field's
  │   │                                  name (DEC-130 (4)), keeps the answer in component state only (never in the
  │   │                                  query cache, the URL or storage), counts 30 seconds down and drops the value
  │   │                                  at 0, on "Ẩn", when the page is hidden (`visibilitychange`) and on unmount;
  │   │                                  a 429 (30 an hour) or a 403 shows its `detail` in a toast, a 500 or no
  │   │                                  answer the texts of *Conventions*; the value stays masked
  │   ├── staff-hr-personal-sheet.tsx    Sheet (right; bottom on a narrow screen): SheetTitle `personalSheet.title`,
  │   │                                  SheetDescription `personalSheet.description` with the account's name; the
  │   │                                  Fields `personal.legalName` (hint `personalSheet.legalNameHint`),
  │   │                                  `personal.birthDate` (a date picker like the disable dialog's, hint
  │   │                                  `personalSheet.keepHint` with the masked value), `personal.gender` (Select),
  │   │                                  `personalSheet.idType` (Select: CCCD, Hộ chiếu), `personalSheet.idNumber`
  │   │                                  (placeholder `personalSheet.idNumberPlaceholder`, `inputmode="numeric"` for
  │   │                                  a CCCD, hint `personalSheet.idNumberHint` with the masked value),
  │   │                                  `personalSheet.issuedDate` (date picker) and `personalSheet.issuedPlace`
  │   │                                  side by side, `personal.permanentAddress` (Textarea, hint
  │   │                                  `personalSheet.keepHint`); a masked field opens empty and an empty one keeps
  │   │                                  the stored value, so editing reveals nothing; `autocomplete="off"` on the
  │   │                                  masked fields; each hint tied by aria-describedby; a 400 or 409 on a field
  │   │                                  in its FieldError, any other item in an Alert at the top; the corner close
  │   │                                  button in a Tooltip `ui.close`; footer "Lưu" (default), disabled with
  │   │                                  LoaderCircleIcon until the answer comes, and "Hủy" (outline); 200 closes it,
  │   │                                  shows the toast `personalSheet.saved` and the card with the masked values
  │   │                                  returned — sheet, field, input, textarea, select, popover, calendar, button
  │   ├── staff-hr-contact-card.tsx      Card as a `section` id contact: CardTitle `contact.title`, CardDescription
  │   │                                  `contact.description`; no CardAction (the staff member edits these on
  │   │                                  SCR-ACC-01, R-USR-18); MaskedField rows `contact.personalPhone`,
  │   │                                  `contact.personalEmail`, `contact.currentAddress`, and one row
  │   │                                  `contact.emergency` per contact (at most 2): the name and relation
  │   │                                  (`contact.emergencyValue`) over the masked phone, its "Hiện" named
  │   │                                  `contact.emergencyPhoneLabel`; no contact: the muted text
  │   │                                  `contact.noEmergency` in that row — card
  │   ├── staff-hr-work-card.tsx         Card as a `section` id work: CardTitle `work.title`, CardDescription
  │   │                                  `work.description`; CardAction "Sửa" with `staff.pii.update` (its sheet needs
  │   │                                  a Layout pull request first, see Builds); rows `work.jobTitle`,
  │   │                                  `work.manager`, `work.contract` (`work.contractValue`: type · start – end;
  │   │                                  the type `work.contractType.*`) — card, button
  │   └── staff-hr-documents-card.tsx    Card as a `section` id documents: CardTitle `documents.title`,
  │                                      CardDescription `documents.description`; CardAction "Tải lên tài liệu"
  │                                      (`documents.upload`, UploadIcon) with `staff.pii.update` (its dialog needs a
  │                                      Layout pull request first); from `@xl` a Table (`documents.tableLabel`;
  │                                      columns `documents.columns.name`, `.type`, `.uploadedAt` `dd/MM/yyyy`,
  │                                      `.status`, `.actions`), below `@xl` a list (the name over type · date and
  │                                      the badge, the actions under them); status Badge `success`
  │                                      `documents.status.approved` or `outline` with ClockIcon
  │                                      `documents.status.pending`; each row the Button ghost sm "Xem"
  │                                      (`documents.view`, `aria-label` `documents.viewLabel` with the file name),
  │                                      which asks the API for the signed link (5 minutes, DEC-131 (3)) and opens it
  │                                      in a new tab; a refusal shows its `detail` in a toast; a pending row, with
  │                                      `staff.pii.update`, adds "Từ chối" (destructive-soft sm) and "Duyệt"
  │                                      (outline sm, CheckIcon), as the rows of SCR-STF-01, each with its
  │                                      `aria-label` (their confirmations need a Layout pull request first); no
  │                                      document: an Empty (FileIcon) with EmptyTitle `documents.empty` and
  │                                      EmptyDescription `documents.emptyDescription` in place of the table — card,
  │                                      table, badge, button, empty
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
                                         `secondary` (grey), ANONYMIZED `outline` with `status.anonymized`, and
                                         (STF-09) PENDING `outline` with ClockIcon (12px, aria-hidden) and
                                         `staff.list.status.pending`, in the heading and in the status row —
                                         badge
  ```
