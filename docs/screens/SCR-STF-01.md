# SCR-STF-01 Staff list

All staff accounts with their status and roles, to find the one to open, approve, disable or restore (STF-05,
STF-09, DEC-105).

Loaded from `GET /api/v1/admin/staff`. The filters, sort and page are kept in the URL (`?status=&roleId=&sort=&page=`), so a
reload or a shared link shows the same list.

| Column | API | Shown as |
|---|---|---|
| STT | — | (page − 1) × 20 + the row's index in the page's `items`, 1 first; the search hides rows without renumbering |
| Họ tên | `name` | link to SCR-STF-03, with `email` under it in muted text on the tab Chờ duyệt |
| Mã nhân viên | `employeeCode` | "—" when empty |
| Email | `email` | |
| Vai trò | `roles[].name` | one badge per role |
| Trạng thái | `status` | badge "Đang hoạt động" (ACTIVE), "Đã khóa" (DISABLED) or "Chờ duyệt" (PENDING, STF-09) |
| Ngày vào làm | `joinedDate` | `dd/MM/yyyy`, "—" when empty |
| Ngày nghỉ việc | `leftDate` | `dd/MM/yyyy`, empty while employed |
| Thao tác | — | a menu of the row's actions (below) |

- **Filters**: "Trạng thái" — since STF-09 a row of tabs instead of a select (one control that shows every status
  and the work waiting): Tất cả (no `status`), Đang hoạt động (`ACTIVE`), Đã khóa (`DISABLED`), Chờ duyệt (`PENDING`)
  with the number of accounts waiting (`totalCount` of `GET /api/v1/admin/staff?status=PENDING&pageSize=1`, hidden
  at 0); "Vai trò" — every
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
- **The tab Chờ duyệt** (STF-09, R-USR-17, UC-140): a line under the tabs says why the accounts wait, and the table
  shows STT, Họ tên with the email under it, Vai trò, Người tạo (`createdBy.name`), Ngày tạo (`createdAt`, `dd/MM/yyyy
  HH:mm`) and Thao tác: on a row whose `canApprove` is true, the buttons "Từ chối" and "Duyệt" side by side; on
  another row, "Xem chi tiết". `canApprove`, `createdBy` and `createdAt` are fields STF-09 adds to the list item of a
  PENDING account (null on the others): the API works out `canApprove` (the caller holds `staff.approve`, did not
  create the account and manages all its roles, R-SEC-12), so the web never decides who may approve. On the tab Tất
  cả a PENDING row has the badge and its menu offers "Duyệt tài khoản" and "Từ chối" under the same `canApprove`; it
  never offers "Khóa tài khoản" (R-USR-17: 409). Each action opens a confirmation that repeats the account (name,
  email, roles, creator and time), so the approver sees what is granted before choosing. "Duyệt" sends
  `POST /api/v1/admin/staff/{id}/approve`, "Từ chối" sends `POST /api/v1/admin/staff/{id}/reject`; the confirm button
  is disabled with a spinner while the request runs. After a 200 or 204 the dialog closes, a toast says "Đã duyệt tài
  khoản {name}." with "Nhân viên đăng nhập được bằng mật khẩu mặc định." or "Đã từ chối tài khoản {name}.", and the
  list and the count reload, so the row leaves the tab. A 404 or 409 (not PENDING any more, the caller's own request,
  an email taken in Firebase, a role outside the caller's scope) shows its `detail` in a toast; a 500 or no answer
  shows the texts of *Conventions*; each closes the dialog and reloads the list and the count. The tab empty:
  "Không còn tài khoản chờ duyệt." with "Xem tất cả nhân viên".
- **Several accounts at once** (drawn ahead for gap 12, at the owner's request): on the tab Chờ duyệt each row whose
  `canApprove` is true has a checkbox, and the header one selects or clears all of them on the page; a selected row is
  highlighted. With one or more selected, a bar above the table says "Đã chọn {count} tài khoản" with "Bỏ chọn", "Từ
  chối {count}" and "Duyệt {count}"; each opens a confirmation listing the accounts with their roles and creator, and
  then a result that lists each account as done or with its problem's text, the list and the count reloading. Changing
  the tab or the page clears the selection. Until gap 12 closes, the first build has no checkboxes and no bar.
- **Empty**: no account at all → "Chưa có tài khoản nhân viên."; nothing matches the filters → "Không có nhân viên
  phù hợp với bộ lọc." with a button to clear the filters.

A 400 means the URL was edited by hand: the list falls back to the default filters.

## Layout

- **Design**: <https://claude.ai/artifact/J3zZ3ffqs7PTWEcu83nFfk>, drawn 2026-11-05 with the design system of
  2026-11-05; approved by the merge of the pull request that wrote this subsection (DEC-119); changed 2026-11-05
  (the columns STT and Thao tác, the soft badges), approved by the merge of the pull request of that change. The
  canvas shows the frame of SCR-ADM-01 for context only; this Layout does not approve it. Changed 2026-11-13 for
  STF-09 on <https://claude.ai/artifact/VGfMXvw1CSvzx3ptVKWHbZ>, drawn with the design system of 2026-11-11 and
  redrawn at the owner's request for usability, consistency, visual hierarchy, feedback, accessibility and the
  approver's needs: the status tabs with the count of accounts waiting instead of the status select, the tab Chờ duyệt
  with its own columns and buttons on each row, confirmations that repeat the account, the feedback after an action,
  and the selection of several accounts (drawn ahead for gap 12); approved by the merge of the pull request of that
  change. Loading, empty and error keep the first canvas.
- **Artboards of the STF-09 change**: desktop (1440 px), signed in as the Admin, on the tab Chờ duyệt with two
  accounts selected and the bar of the selection (in Play, the tabs, the checkboxes and "Bỏ chọn" work); the
  confirmations "Duyệt" and "Từ chối" of one account; after an approval, the toast and the count down to 1; the tab
  Tất cả with the badge Chờ duyệt and the menu of a PENDING row; signed in as the fixture IT Support (no
  `staff.approve`), the tab Chờ duyệt with "Xem chi tiết" only and no checkbox; narrow (390 px), the tab Chờ duyệt as a
  list of blocks with full-width buttons; the tab Chờ duyệt empty; dark, the tab Chờ duyệt with the confirmation "Từ
  chối" (DEC-123 (5)); for gap 12, the confirmations of several accounts and a result where one account of two could
  not be approved.
- **Artboards**: desktop (1440 px), signed in as the Admin, where "Họ tên" sorts and the menu "Thao tác" of a row
  opens in Play (open on the locked row); narrow (390 px), on the first of three pages, with the search hint and the
  tooltip of "Trang sau" open; states: loading, empty (no account), empty (nothing matches the filters), error. No
  permission is the page of SCR-ADM-01 (`RequirePermission` with `staff.read`). The fixture manager is drawn locked,
  so that both status badges show.
- **shadcn**: `shadcn add table badge select pagination` (the tree names them too); it must not overwrite the
  edited `button` that `pagination` depends on (DEC-116 (4)); `pagination` writes English texts, which go through
  `ui`. The change needs no `add`: `dropdown-menu` and `tooltip` are installed. The STF-09 change: `shadcn add tabs`
  (on the approved `radix-ui`, no new package; committed alone first, DEC-116 (1)); `alert-dialog`, `badge`,
  `checkbox`, `dialog` and `sonner` are installed.
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
  table box scrolls sideways, so no text of the table needs a tooltip. For the STF-09 change, under `staff.list`
  unless named: `description` "Tài khoản nhân viên của cửa hàng, cùng vai trò và trạng thái. Xem, duyệt, khóa hoặc
  khôi phục một tài khoản từ cột Thao tác." (changed: "duyệt" added); `status.pending` "Chờ duyệt" (*new*; the tab and
  the badge) and the tabs' name `status.label` "Trạng thái" (*new*; `columns.status` stays the column's); the count's
  sr text `status.pendingCount` "{count} tài khoản" (*new*); the line of the tab `pending.description` "Nhân viên chưa
  đăng nhập được cho tới khi tài khoản được duyệt. Kiểm tra vai trò và người tạo trước khi duyệt." (*new*); the
  columns `columns.createdBy` "Người tạo" and `columns.createdAt` "Ngày tạo" (*new*); the row buttons
  `staff.account.approve` "Duyệt" and `staff.account.reject` "Từ chối" with their `aria-label`s
  `actions.approveLabel` "Duyệt {name}" and `actions.rejectLabel` "Từ chối {name}" (*new*), and the menu items
  `actions.approve` "Duyệt tài khoản" and `actions.reject` "Từ chối" (*new*); the confirmations
  `staff.account.approveDialog.title` "Duyệt tài khoản này?", `.description` "Tài khoản được kích hoạt với mật khẩu
  mặc định. Nhân viên phải đổi mật khẩu ở lần đăng nhập đầu tiên." and `.submit` "Duyệt",
  `staff.account.rejectDialog.title` "Từ chối tài khoản này?", `.description` "Tài khoản chờ duyệt bị xóa cùng hồ sơ
  và vai trò, không hoàn tác được. Muốn dùng lại email này, hãy tạo tài khoản mới." and `.submit` "Từ chối tài khoản"
  (*new*), their rows `staff.account.summary.name` "Họ tên", `.email` "Email", `.roles` "Vai trò" and `.createdBy`
  "Người tạo" (*new*), and `staff.account.cancel` "Hủy" (existing); the toasts `staff.account.approved` "Đã duyệt tài
  khoản {name}." with `staff.account.approvedHint` "Nhân viên đăng nhập được bằng mật khẩu mặc định." and
  `staff.account.rejected` "Đã từ chối tài khoản {name}." (*new*); the empty tab `pendingEmpty.title` "Không còn tài
  khoản chờ duyệt.", `pendingEmpty.description` "Tài khoản mới do người không có quyền duyệt tạo sẽ hiện ở đây." and
  `pendingEmpty.showAll` "Xem tất cả nhân viên" (*new*). For gap 12, not built yet: `bulk.label` "Thao tác với tài
  khoản đã chọn", `bulk.selected` "Đã chọn {count} tài khoản", `bulk.clear` "Bỏ chọn", `bulk.approve` "Duyệt {count}",
  `bulk.reject` "Từ chối {count}", `bulk.selectAll` "Chọn mọi tài khoản duyệt được trong trang", `bulk.select` "Chọn
  {name}", the confirmations `bulk.approveDialog.title` "Duyệt {count} tài khoản?", `.description` "Các tài khoản được
  kích hoạt với mật khẩu mặc định. Hệ thống duyệt từng tài khoản; tài khoản nào không duyệt được sẽ giữ trạng thái Chờ
  duyệt và có lý do trong kết quả.", `.submit` "Duyệt {count} tài khoản", `bulk.rejectDialog.title` "Từ chối {count}
  tài khoản?", `.description` "Các tài khoản chờ duyệt bị xóa cùng hồ sơ và vai trò, không hoàn tác được.",
  `.submit` "Từ chối {count} tài khoản", and the result `bulk.result.title` "Đã duyệt {done} trên {count} tài khoản",
  `bulk.result.description` "Tài khoản không duyệt được vẫn ở tab Chờ duyệt. Xem lý do bên dưới.", `bulk.result.done`
  "Đã duyệt", `bulk.result.close` "Đóng" (*new*).
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
  ├── staff-list-filters-section.tsx (STF-09) the Tabs of `status.label` first, on one line with the Field `search`
  │                                  on a wide screen (the line wraps): Tất cả, Đang hoạt động, Đã khóa, Chờ duyệt
  │                                  with the count of `staff-pending-count` (a Badge, `primary`, its number with the
  │                                  sr text `status.pendingCount`; hidden at 0); a tab sets `status` and drops
  │                                  `page`; the Tabs scroll sideways on a narrow screen; they replace the status
  │                                  Select below; then the Field `search` (InputGroup with SearchIcon and the
  │                                  placeholder
  │                                  `searchPlaceholder`, FieldDescription `searchHint` with more than one page,
  │                                  tied by aria-describedby) and the Field `status`
  │                                  labelled `columns.status` (Select: Tất cả, Đang hoạt động, Đã khóa); status,
  │                                  sort and page in the URL, a change of status drops `page` (back to 1); the
  │                                  search text stays in the page, never in the URL (DEC-105): the route calls
  │                                  `hooks/use-staff-list.ts` once and gives its state to this section and the
  │                                  table section; the role filter left out (gap 2) — tabs, field, input-group
  │   └── ../hooks/use-pending-count.ts (STF-09) the query GET /api/v1/admin/staff?status=PENDING&pageSize=1 and its
  │                                  `totalCount`, invalidated with the list after any approval or rejection
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
      │                              that the filters stay — table, tooltip, empty, skeleton, pagination. With
      │                              STF-09, on the tab Chờ duyệt: the line `pending.description` above the table,
      │                              the columns STT, Họ tên (`email` under it, muted), Vai trò, Người tạo, Ngày tạo
      │                              and Thao tác, its rows `staff-pending-row.tsx`; empty: Empty `pendingEmpty.*`
      │                              (CircleCheckIcon) with "Xem tất cả nhân viên" (outline), which sets the tab Tất
      │                              cả; on a narrow screen the tab is a list of blocks (`staff-pending-row.tsx` in
      │                              its block form), not a table, so that the buttons stay in view
      ├── staff-pending-row.tsx      (STF-09) presentational — one PENDING account: its cells, or its block on a
      │                              narrow screen (name and email, the role Badges and "Tạo bởi {createdBy} ·
      │                              {createdAt}", the two buttons side by side full width); Thao tác: when
      │                              `canApprove`, Button `destructive-soft` sm "Từ chối" and Button outline sm
      │                              "Duyệt" (CheckIcon), each with its `aria-label` naming the account, opening
      │                              `staff-reject-dialog.tsx` and `staff-approve-dialog.tsx`; otherwise the link
      │                              "Xem chi tiết" (outline sm) — button
      ├── staff-approve-dialog.tsx   (STF-09) AlertDialog: AlertDialogTitle `approveDialog.title`,
      │                              AlertDialogDescription `approveDialog.description`, a `dl` of the account
      │                              (`summary.*`: name, email, role Badges, creator and time), "Hủy" and the
      │                              submit (default), disabled with LoaderCircleIcon while the request of
      │                              `hooks/use-staff-approval.ts` runs; shared with SCR-STF-03 — alert-dialog
      ├── staff-reject-dialog.tsx    (STF-09) the same with `rejectDialog.*` and the submit `destructive-soft`
      │                              — alert-dialog
      ├── ../hooks/use-staff-approval.ts (STF-09) the two requests; on 200 or 204: closes the dialog, a toast
      │                              (`approved` with `approvedHint`, or `rejected`) and invalidates the list and the
      │                              count; on 404 or 409 the problem's `detail` in a toast, on 500 or no answer the
      │                              texts of *Conventions*, each closing the dialog and invalidating both
      ├── staff-bulk-bar.tsx         (gap 12, not built yet) a toolbar named `bulk.label` above the table while one
      │                              or more rows are selected: `bulk.selected` (aria-live polite), "Bỏ chọn"
      │                              (ghost), "Từ chối {count}" (`destructive-soft`) and "Duyệt {count}" (default),
      │                              opening the confirmations of several accounts (an AlertDialog with the list of
      │                              accounts, their roles and creator) and then `staff-bulk-result-dialog.tsx`;
      │                              the checkboxes (Checkbox, `bulk.select` and `bulk.selectAll`) sit in a first
      │                              column of the rows whose `canApprove` is true; a selected row is highlighted
      │                              (`bg-muted`); the tab or the page changing clears the selection — checkbox,
      │                              alert-dialog, button
      ├── staff-bulk-result-dialog.tsx (gap 12, not built yet) Dialog `bulk.result.*`: one line per account,
      │                              CircleCheckIcon and `bulk.result.done`, or CircleAlertIcon (destructive) and
      │                              the problem's text; "Đóng" — dialog
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
                                     with STF-09, on a PENDING row (never "Khóa tài khoản"): when the row's
                                     `canApprove` is true, a separator, `actions.approve` (CheckIcon) and
                                     `actions.reject` (variant destructive, XIcon), opening the two dialogs of
                                     the tab Chờ duyệt
  ```
