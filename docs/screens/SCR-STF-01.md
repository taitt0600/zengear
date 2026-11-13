# SCR-STF-01 Staff list

All staff accounts with their status and roles, to find the one to open, disable or restore (STF-05, DEC-105).

Loaded from `GET /api/v1/admin/staff`. The filters, sort and page are kept in the URL (`?status=&roleId=&sort=&page=`), so a
reload or a shared link shows the same list.

| Column | API | Shown as |
|---|---|---|
| STT | — | (page − 1) × 20 + the row's index in the page's `items`, 1 first; the search hides rows without renumbering |
| Họ tên | `name` | link to SCR-STF-03 |
| Mã nhân viên | `employeeCode` | "—" when empty |
| Email | `email` | |
| Vai trò | `roles[].name` | one badge per role |
| Trạng thái | `status` | badge "Đang hoạt động" (ACTIVE), "Đã khóa" (DISABLED) or "Chờ duyệt" (PENDING, STF-09) |
| Ngày vào làm | `joinedDate` | `dd/MM/yyyy`, "—" when empty |
| Ngày nghỉ việc | `leftDate` | `dd/MM/yyyy`, empty while employed |
| Thao tác | — | a menu of the row's actions (below) |

- **Filters**: "Trạng thái" — Tất cả (no `status`), Đang hoạt động (`ACTIVE`), Đã khóa (`DISABLED`), Chờ duyệt
  (`PENDING`, STF-09); "Vai trò" — every
  role (`roleId`), shown once the roles can be listed (gap 2).
- **Search box** "Lọc theo tên, mã hoặc email": filters the rows already loaded, in the browser; nothing is sent to
  the API, since names and emails stay out of the query string (DEC-105). With more than one page, the box says
  "Chỉ lọc trong trang đang xem."
- **Sort**: clicking "Họ tên", "Mã nhân viên" or "Ngày vào làm" sends `sort=name|employeeCode|joinedDate`, a second
  click the same with `-`; default `name` (Vietnamese order).
- **Paging**: 20 rows a page (`pageSize=20`), with the total from `totalCount`.
- **Button** "Thêm nhân viên" (SCR-STF-02), shown with `staff.create`.
- **Actions of a row** (chosen by the owner on the change of this Layout): "Xem chi tiết" (SCR-STF-03); with
  `staff.disable`, on another user's account, "Khóa tài khoản" (ACTIVE), or "Khôi phục tài khoản" and "Khóa lại"
  (DISABLED), each opening the same dialog or confirmation as on SCR-STF-03 with the same request; a field error shows
  in the dialog, every other problem in a toast, and a 404, a 409 `concurrency-conflict`, a 500 or no answer also
  reload the list (`hooks/use-staff-status.ts` invalidates the list then too); the dialog closes as the tree of
  SCR-STF-03 says; after a 200 the list reloads. A later action such as the profile update (gap 7) joins this menu.
- **Accounts waiting for approval** (STF-09, R-USR-17): with `staff.approve`, while the filter is not Chờ duyệt and
  `GET /api/v1/admin/staff?status=PENDING&pageSize=1` gives a `totalCount` above 0, a notice above the filters says
  "Có {count} tài khoản chờ duyệt" with a button that sets the filter to Chờ duyệt. On a PENDING row the menu offers,
  with `staff.approve`, "Duyệt tài khoản" and "Từ chối", each with its confirmation; on an account the user created
  (the row's `createdById`, a field STF-09 adds to the list item) the two items give way to the line "Bạn tạo tài
  khoản này nên không tự duyệt được."; without `staff.approve` only "Xem chi tiết". A PENDING row has no "Khóa tài
  khoản" (R-USR-17: 409). "Duyệt" sends `POST /api/v1/admin/staff/{id}/approve`, "Từ chối" sends
  `POST /api/v1/admin/staff/{id}/reject`; after a 200 or 204 a toast says "Đã duyệt tài khoản." or "Đã từ chối tài
  khoản.", the dialog closes and the list and the notice reload; a 409 (not PENDING any more, the user's own request,
  an email taken in Firebase, a role outside the user's scope) or a 404 shows its `detail` in a toast and also
  reloads them.
- **Empty**: no account at all → "Chưa có tài khoản nhân viên."; nothing matches the filters → "Không có nhân viên
  phù hợp với bộ lọc." with a button to clear the filters.

A 400 means the URL was edited by hand: the list falls back to the default filters.

## Layout

- **Design**: <https://claude.ai/artifact/J3zZ3ffqs7PTWEcu83nFfk>, drawn 2026-11-05 with the design system of
  2026-11-05; approved by the merge of the pull request that wrote this subsection (DEC-119); changed 2026-11-05
  (the columns STT and Thao tác, the soft badges), approved by the merge of the pull request of that change. The
  canvas shows the frame of SCR-ADM-01 for context only; this Layout does not approve it. Changed 2026-11-13 for
  STF-09 (the PENDING status, the notice for approvers, "Duyệt tài khoản" and "Từ chối" with their confirmations) on
  <https://claude.ai/artifact/VGfMXvw1CSvzx3ptVKWHbZ>, drawn with the design system of 2026-11-11; approved by the
  merge of the pull request of that change. Loading, empty and error keep the first canvas.
- **Artboards of the STF-09 change**: desktop (1440 px), signed in as the Admin, with the notice and the menu of a
  PENDING row open (in Play, the menu of each row opens); the confirmations "Duyệt tài khoản" and "Từ chối"; desktop
  signed in as the fixture IT Support (no `staff.approve`), filtered on Chờ duyệt, with the menu of a PENDING row
  holding only "Xem chi tiết"; narrow (390 px) with the notice and a PENDING badge. The line "Bạn tạo tài khoản này nên
  không tự duyệt được." is in Play on the second PENDING row of the IT Support artboard's source, not drawn open.
- **Artboards**: desktop (1440 px), signed in as the Admin, where "Họ tên" sorts and the menu "Thao tác" of a row
  opens in Play (open on the locked row); narrow (390 px), on the first of three pages, with the search hint and the
  tooltip of "Trang sau" open; states: loading, empty (no account), empty (nothing matches the filters), error. No
  permission is the page of SCR-ADM-01 (`RequirePermission` with `staff.read`). The fixture manager is drawn locked,
  so that both status badges show.
- **shadcn**: `shadcn add table badge select pagination` (the tree names them too); it must not overwrite the
  edited `button` that `pagination` depends on (DEC-116 (4)); `pagination` writes English texts, which go through
  `ui`. The change needs no `add`: `dropdown-menu` and `tooltip` are installed; nor does the STF-09 change:
  `alert`, `alert-dialog` and `badge` are installed.
- **Theme**: soft badges, as the **Theme** bullet of the Layout of SCR-STF-03 says (chosen by the owner): ACTIVE
  `success` with `--success-soft` and `--success-soft-foreground`, DISABLED `secondary` (grey). The run of STF-02
  builds them for both screens (it adds the tokens, changes the variant and `staff-status-badge.tsx`, and removes
  `--success` of commit d6b6032); the pull request that builds this change, `feat(web): add the row actions of the
  staff list (STF-05)`, adds only the two columns and comes after the web-only pull request of STF-02; it is a `feat`,
  not the `refactor(web)` of the paragraph under the [screen list](README.md#screen-list), since it adds behaviour (chosen by the owner).
- **Texts** (`messages/vi.json`), new ones marked *new*, under `staff.list` unless named: the card's description
  `description` "Tài khoản nhân viên của cửa hàng, cùng vai trò và trạng thái. Xem, khóa hoặc khôi phục một tài khoản
  từ cột Thao tác." (changed from "Mở một tài khoản để xem, khóa hoặc khôi phục."); `add` "Thêm nhân viên" (*new*);
  the columns `columns.name` "Họ tên", `columns.employeeCode` "Mã nhân viên", `columns.email` "Email", `columns.roles`
  "Vai trò", `columns.status` "Trạng thái", `columns.joinedDate` "Ngày vào làm", `columns.leftDate` "Ngày nghỉ việc"
  (*new*); `status.all` "Tất cả", `status.active` "Đang hoạt động", `status.disabled` "Đã khóa" (*new*; the status
  Field is labelled by `columns.status`); the search box's label `search` "Lọc theo tên, mã hoặc email" (*new*), its
  placeholder `searchPlaceholder` "an@example.com" (*new*) and its hint `searchHint` "Chỉ lọc trong trang đang xem."
  (*new*, shown with more than one page); `empty.title` "Chưa có tài khoản nhân viên." and `empty.description` "Tài
  khoản do quản trị viên hoặc quản lý cửa hàng tạo sẽ hiện ở đây." (*new*); `noMatch.title` "Không có nhân viên phù
  hợp với bộ lọc.", `noMatch.description` "Thử bỏ bớt điều kiện lọc để xem thêm tài khoản." and `clearFilters` "Xóa bộ
  lọc" (*new*); `total` "{count} nhân viên" and `page` "Trang {page} / {pages}" (*new*); the table box's name
  `tableLabel` "Bảng nhân viên" (*new*); `admin.retry` "Thử lại"; `ui.pagination.label` "Phân trang" (*new*; the nav's
  name), `ui.pagination.previous` "Trang trước" and `ui.pagination.next` "Trang sau" (*new*; each both the visible
  text and the `aria-label` of its link, and its tooltip on a narrow screen, where the text hides),
  `ui.pagination.more` "Các trang khác" (*new*); for the change: the columns `columns.number` "STT" with its sr-only
  `columns.numberFull` "Số thứ tự" and `columns.actions` "Thao tác" (*new*; the second is also the tooltip of each
  row's menu button), the button's `aria-label` `actions.label` "Thao tác cho {name}" and the item `actions.view` "Xem
  chi tiết" (*new*), the items `staff.account.disable`, `staff.account.restore` and `staff.account.disableAgain` and
  the texts of the dialog and the confirmation of SCR-STF-03. No cell is cut short: the cells do not wrap and the
  table box scrolls sideways, so no text of the table needs a tooltip. For the STF-09 change: the card's description
  `description` "Tài khoản nhân viên của cửa hàng, cùng vai trò và trạng thái. Xem, duyệt, khóa hoặc khôi phục một tài
  khoản từ cột Thao tác." (changed: "duyệt" added); `status.pending` "Chờ duyệt" (*new*; the filter item and the
  badge); the notice `pending.title` "Có {count} tài khoản chờ duyệt", its description `pending.description` "Tài
  khoản do người không có quyền duyệt tạo. Nhân viên chưa đăng nhập được cho tới khi bạn duyệt." and its button
  `pending.show` "Xem tài khoản chờ duyệt" (*new*); the items `staff.account.approve` "Duyệt tài khoản" and
  `staff.account.reject` "Từ chối" and the line `actions.ownRequest` "Bạn tạo tài khoản này nên không tự duyệt được."
  (*new*); the confirmations `staff.account.approveDialog.title` "Duyệt tài khoản {name}?", `.description` "Tài khoản
  được kích hoạt với mật khẩu mặc định. Nhân viên phải đổi mật khẩu ở lần đăng nhập đầu tiên." and `.confirm`
  "Duyệt", `staff.account.rejectDialog.title` "Từ chối tài khoản {name}?", `.description` "Tài khoản chờ duyệt bị xóa
  cùng hồ sơ và vai trò, không hoàn tác được. Muốn dùng lại email này, hãy tạo tài khoản mới." and `.confirm` "Từ
  chối", with `ui.cancel` "Hủy" (*new*, under `staff.account` since SCR-STF-03 opens the same confirmations); the toasts
  `staff.account.approved` "Đã duyệt tài khoản." and `staff.account.rejected` "Đã từ chối tài khoản." (*new*).
- **Component tree** (DEC-118 (3)), under `web/src/features/identity/staff-accounts/components/`; the route
  `app/(admin)/admin/(shell)/staff/page.tsx` composes the layout with the three sections inside
  `RequirePermission` (`staff.read`):

  ```text
  staff-list-layout.tsx              slots: header | filters | list — one Card (a one-card page: the h1 in
  │                                  CardTitle); narrow: the filters stack and the table's box gets a border
  ├── staff-list-header-section.tsx  CardHeader: CardTitle with the h1 "Nhân viên" (admin.menu.staff),
  │                                  CardDescription `description`; CardAction: the link "Thêm nhân viên" (PlusIcon,
  │                                  a Button asChild) to SCR-STF-02, shown with `staff.create`, full width on a
  │                                  narrow screen — card, button
  ├── staff-list-filters-section.tsx the Field `search` (InputGroup with SearchIcon and the placeholder
  │                                  `searchPlaceholder`, FieldDescription `searchHint` with more than one page,
  │                                  (STF-09) above them the notice of `staff-pending-notice.tsx`,
  │                                  tied by aria-describedby) and the Field `status`
  │                                  labelled `columns.status` (Select: Tất cả, Đang hoạt động, Đã khóa, and
  │                                  Chờ duyệt with STF-09); status,
  │                                  sort and page in the URL, a change of status drops `page` (back to 1); the
  │                                  search text stays in the page, never in the URL (DEC-105): the route calls
  │                                  `hooks/use-staff-list.ts` once and gives its state to this section and the
  │                                  table section; the role filter left out (gap 2) — field, input-group, select
  │   └── staff-pending-notice.tsx   (STF-09) shown with `staff.approve` while the filter is not Chờ duyệt and the
  │                                  query GET /api/v1/admin/staff?status=PENDING&pageSize=1 gives `totalCount` above
  │                                  0: an Alert (ClockIcon, role="status") with AlertTitle `pending.title`,
  │                                  AlertDescription `pending.description` and a Button outline sm `pending.show`
  │                                  that sets `status=PENDING` and drops `page`; full width on a narrow screen; the
  │                                  query is invalidated with the list after an approval or a rejection — alert
  └── staff-list-table-section.tsx   renders what `hooks/use-staff-list.ts` gives (the query GET /api/v1/admin/staff
      │                              with the URL's status, sort and page, pageSize 20, the search over the page's rows,
      │                              and the URL changes, so the section stays under 150 lines): the Table of the nine
      │                              columns, in a box with role="region", tabIndex 0 and aria-label `tableLabel` at
      │                              every width, its cells not wrapped; its rows `staff-list-row.tsx`, to which it
      │                              passes whether the user may act (`staff.disable` in `permissions`) and the user's
      │                              `id` (`useCurrentUser`), worked out once; "Họ tên" a link to SCR-STF-03; footer:
      │                              `total` and the Pagination named `ui.pagination.label` ("Trang trước", `page`,
      │                              "Trang sau"; on page 1 and on the last page the link that leads nowhere has no
      │                              href, aria-disabled and opacity 50%; on a narrow screen the two links show only
      │                              their icon, each in a Tooltip with its text); loading: three rows of Skeleton;
      │                              empty (`totalCount` 0 with the default filters): Empty `empty.*` (UsersIcon) with
      │                              "Thêm nhân viên" when `staff.create`; nothing matches (`totalCount` 0 with a
      │                              status, or the search hides every row of the page): Empty `noMatch.*` (SearchXIcon)
      │                              with "Xóa bộ lọc" (outline), which clears the search and `status`; a page past the
      │                              end (`items` empty, `totalCount` above 0) goes back to page 1; a 400 resets the URL
      │                              to the default filters; any other problem: Empty with the problem's text in
      │                              EmptyTitle, destructive, role="alert" (TriangleAlertIcon), and "Thử lại", disabled
      │                              while the retry is pending; each Empty in place of the rows, inside the card, so
      │                              that the filters stay — table, tooltip, empty, skeleton, pagination
      ├── staff-sort-header.tsx      presentational — the header cell of "Họ tên", "Mã nhân viên" or "Ngày vào
      │                              làm", whose button sorts, with aria-sort and ArrowUpIcon (ascending),
      │                              ArrowDownIcon (descending) or ArrowUpDownIcon (not sorted) — button
      ├── staff-roles-cell.tsx       presentational — one Badge (secondary) per role name — badge
      ├── staff-status-badge.tsx     presentational — Badge: "Đang hoạt động" (`success`, soft green), "Đã khóa"
      │                              (`secondary`, grey), built by the run of STF-02, and with STF-09 "Chờ duyệt"
      │                              (`outline` with ClockIcon 12px, aria-hidden) — badge
      └── staff-list-row.tsx         presentational — the nine cells of one account: STT first (right-aligned,
          │                          tabular-nums, its header "STT" `aria-hidden` with an sr-only `columns.numberFull`),
          │                          then the seven of the API and the actions; its skeleton row has nine bars
          └── staff-row-actions.tsx  holds behaviour (the menu's open state and the dialogs): a Button ghost size
                                     icon (MoreHorizontalIcon) named `actions.label`, in a Tooltip
                                     `columns.actions` (shorter than the name on purpose: the name contains it),
                                     opening a DropdownMenu (align end): "Xem chi tiết" (EyeIcon, a link to
                                     SCR-STF-03); when the row says the user may act and the account is not the
                                     user's own, a separator, then on ACTIVE "Khóa tài khoản" (variant
                                     destructive, LockIcon), on DISABLED "Khôi phục tài khoản" (RotateCcwIcon)
                                     and "Khóa lại" (variant destructive, LockIcon); they open
                                     `staff-disable-dialog.tsx` and `staff-restore-dialog.tsx` of SCR-STF-03
                                     with the row's account as props, whose requests come from
                                     `hooks/use-staff-status.ts`; so this change is built after the web-only pull
                                     request of STF-02 (`feat(web): … (STF-02)`) — dropdown-menu, tooltip, button;
                                     with STF-09, on PENDING (never "Khóa tài khoản"): when `staff.approve` is
                                     in `permissions` and the row's `createdById` is not the user's `id`, a
                                     separator, "Duyệt tài khoản" (CheckIcon) and "Từ chối" (variant
                                     destructive, XIcon), which open `staff-approve-dialog.tsx` and
                                     `staff-reject-dialog.tsx` (AlertDialog with AlertDialogDescription, the
                                     confirm button `default` or `destructive-soft`, disabled while the request
                                     runs; requests from `hooks/use-staff-approval.ts`; shared with SCR-STF-03);
                                     when it is the user's own request, a separator and the muted line
                                     `actions.ownRequest` instead; a 200 or 204 closes the dialog with the toast,
                                     a 404 or 409 closes it with the problem's `detail` in a toast; both reload
                                     the list and the notice
  ```
