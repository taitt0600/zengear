# SCR-STF-01 Staff list

All staff accounts with their status and roles, to find the one to open, approve, disable or restore, and to act on
several at once (STF-05 to STF-09, DEC-105, DEC-127, DEC-128).

Loaded from `GET /api/v1/admin/staff` until STF-06 merges, then from `POST /api/v1/admin/staff/search` with the
filters in the body (DEC-127 (1)): the same paged list, whose items STF-06 adds `lastLoginAt` and `mustChangePassword`
to. The status, the role, the ranges of dates, `mustChangePassword`, the sort, the page and the page size are kept in
the URL (`status`, `roleId`, `joinedFrom`, `joinedTo`, `leftFrom`, `leftTo`, `lastLoginFrom`, `lastLoginTo`,
`mustChangePassword`, `sort`, `page`, `pageSize`), so a reload or a shared link shows the same list; the search text
`q` never goes in the URL (personal data, R-USR-14) and stays in the page.

| Column | API | Shown as |
|---|---|---|
| (checkbox) | — | with STF-07 or STF-08, for a user who holds `staff.disable` or `staff.export`; none on the user's own row (nobody disables or restores their own account, R-USR-07; their own row is exported with "Xuất CSV") and on a PENDING row of the other tabs; on the tab Chờ duyệt, only on a row whose `canApprove` is true (gaps 12 and 13) |
| STT | — | (page − 1) × the page size + the row's index in the page's `items`, 1 first |
| Họ tên | `name`, `email` | link to SCR-STF-03, with `email` under it in muted text (the column Email goes, from the STF-09 build) |
| Mã nhân viên | `employeeCode` | "—" when empty |
| Vai trò | `roles[].name` | one badge per role |
| Trạng thái | `status`, `mustChangePassword` | badge "Đang hoạt động" (ACTIVE), "Đã khóa" (DISABLED) or "Chờ duyệt" (PENDING, STF-09); with STF-06, "Chưa đổi mật khẩu" under the badge of an ACTIVE account whose `mustChangePassword` is true |
| Ngày vào làm | `joinedDate` | `dd/MM/yyyy`, "—" when empty |
| Đăng nhập gần nhất | `lastLoginAt` | STF-06: `dd/MM/yyyy HH:mm`, "Chưa đăng nhập" (muted) when null |
| Ngày nghỉ việc | `leftDate` | `dd/MM/yyyy`, empty while employed |
| Thao tác | — | a menu of the row's actions (below) |

- **Status** "Trạng thái" (STF-09): a row of links with a visible label, one control that shows every status and the
  work waiting: Tất cả (no `status`), Đang hoạt động (`ACTIVE`), Đã khóa (`DISABLED`), Chờ duyệt (`PENDING`) with the
  number of accounts waiting (`totalCount` of the list with `status=PENDING` and `pageSize=1`, hidden at 0). Each link
  is a URL: the current one has `aria-current="page"`, and following one drops `page` and `sort` (the tab Chờ duyệt has
  no column that sorts) and clears the selection. It replaces the status select of the STF-05 build.
- **Search** "Tìm nhân viên" (STF-06): `q`, 2 to 100 characters, on the name, email and employee code, without case or
  diacritics, over the whole list; sent 300 ms after the last key, never with one character. Until STF-06, the box
  filters the rows already loaded, as the STF-05 build does.
- **Filters** (STF-06): "Vai trò" (`roleId`, from `GET /api/v1/admin/staff/role-options`, DEC-136; "Tất cả vai trò"
  first; no Admin for a user who does not hold it) beside the search; "Bộ lọc khác" opens a popover with the ranges
  Ngày vào làm, Ngày nghỉ việc and Đăng nhập gần nhất (two date pickers each, "Không giới hạn" when empty) and
  "Chưa đổi mật khẩu mặc định" (`mustChangePassword=true`); "Áp dụng" sets them all at once and drops `page`. The
  button shows the number of these filters in use; each one in use shows as a chip under the filters, with a button
  that removes it, and "Xóa bộ lọc" removes them all (not the status). A range whose start is after its end is refused
  in the popover, before any request; the API still answers 400 (R-USR-14).
- **Sort**: clicking "Họ tên", "Mã nhân viên", "Ngày vào làm" or, with STF-06, "Đăng nhập gần nhất" sends
  `sort=name|employeeCode|joinedDate|lastLoginAt`, a second click the same with `-`; default `name` (Vietnamese
  order); accounts never signed in come last (R-USR-14). The tab Chờ duyệt has no sort.
- **Paging**: 20 rows a page; with STF-06, "Số dòng mỗi trang" 20, 50 or 100 (`pageSize`, a change drops `page`), the
  numbered pages, and "Hiển thị {from}–{to} trên {total} nhân viên". A page holds at most 100 rows, the limit of a bulk
  action, so a selection never spans pages.
- **Buttons of the header**: "Thêm nhân viên" (SCR-STF-02) with `staff.create`; with STF-08, "Xuất CSV" with
  `staff.export`, which exports every account that matches the filters (below).
- **Actions of a row**: "Xem chi tiết" (SCR-STF-03); with `staff.disable`, on another user's account, "Khóa tài khoản"
  (ACTIVE), or "Khôi phục tài khoản" and "Khóa lại" (DISABLED), each opening the same dialog or confirmation as on
  SCR-STF-03 with the same request; a field error shows in the dialog, every other problem in a toast, and a 404, a 409
  `concurrency-conflict`, a 500 or no answer also reload the list (`hooks/use-staff-status.ts` invalidates the list
  then too); after a 200 the list reloads. On a PENDING row, "Duyệt tài khoản" and "Từ chối" by the rule of the tab Chờ
  duyệt, never "Khóa tài khoản" (R-USR-17: 409). Drawn ahead for gap 7, at the owner's request: "Sửa vai trò" on an
  ACTIVE or DISABLED row of another user, with `staff.update`, which only opens SCR-STF-03 with its role dialog open;
  the dialog is drawn by the Layout of SCR-STF-03 for STF-11, never a second time here, and nothing edits roles in bulk.
- **Several accounts at once** (STF-07, STF-08, DEC-127 (5)): a selected row is highlighted; the header checkbox
  selects or clears every selectable row of the page. With one or more selected, a bar above the table says "Đã chọn
  {count} tài khoản" with "Bỏ chọn" and, by tab and permission: "Xuất {count}" (`staff.export`), "Khôi phục {count}"
  (`staff.disable`, tabs Tất cả and Đã khóa) and "Khóa {count}" (`staff.disable`, tabs Tất cả and Đang hoạt động); a
  change of tab, filter, sort, page or page size clears the selection. The web never checks which selected accounts
  may change: the job does, account by account (R-USR-15).
  - "Khóa {count}" opens a confirmation listing the accounts with their roles, with the checkbox "Nhân viên nghỉ
    việc" and its date picker "Ngày nghỉ việc" (not after today) as on SCR-STF-03, the date shared by all
    (`leftDate`, null when not ticked); it sends `POST /api/v1/admin/staff/bulk-disable` `{ ids, leftDate }`.
    "Khôi phục {count}" opens a confirmation with the list and sends `POST /api/v1/admin/staff/bulk-restore`
    `{ ids }`.
  - Each request carries an `Idempotency-Key` made when the confirmation opens, so a second click or a retry of the
    same confirmation never makes a second job (R-JOB-01). A 202 opens the progress.
- **Progress of a job** (STF-07, STF-08, R-JOB-01): a dialog polls `GET /api/v1/admin/jobs/{id}` every 1.5 s and
  shows "Đã xử lý {done} / {count}", a progress bar and one line per account as its result comes (done, or the
  problem's `detail`); "Chạy nền" closes it while the polling goes on as long as the page is open, and the end shows a
  toast "Đã khóa {done} trên {count} tài khoản." (or its restore form) with "Xem kết quả", which opens the result. At
  the end (Completed or Failed) the dialog becomes the result, and the list reloads. A 429 (two jobs already running)
  or a 400 shows the problem's `detail` in a toast and makes no job. The running jobs (id, kind and count, nothing
  personal) are kept in `sessionStorage` of the tab, so leaving the page for SCR-STF-03 and coming back, or a reload,
  resumes the polling and the toast; a job finished while away shows its toast on return, and an export stays
  downloadable from it until its file expires. A job that answers 404 (another user's tab, or deleted after 30 days,
  R-JOB-01) is dropped from the storage. If the storage cannot be read, only the open page follows the job.
- **Export** (STF-08, R-USR-16): "Xuất CSV" of the header opens a confirmation "Xuất {count} nhân viên khớp bộ lọc?"
  with the count (`totalCount`), the filters in use and what the file holds, then sends
  `POST /api/v1/admin/staff/export` `{ criteria }` with the current filters and search; "Xuất {count}" of the bar sends
  `{ ids }` without a confirmation of the count. Both carry an `Idempotency-Key`, then show the progress. A 409 (more
  than 50,000 rows) shows its `detail`. The confirmation repeats the search text too, since the count depends on it.
  Done: "File đã sẵn sàng" with the file's name and "Tải file CSV", which downloads `GET /api/v1/admin/jobs/{id}/file`
  through the API client as bytes (the BOM kept, R-USR-16; never a public link); the file can be downloaded until 24
  hours after the job finished, shown as a time. A refused download (the file expired, `staff.export` removed) shows
  the problem's `detail` in a toast. An export that ended while its dialog was closed shows the toast "Đã xuất {count}
  nhân viên." with "Tải file", which opens the file dialog; a failed export says so in the dialog and the toast.
- **Withdrawing on the list** (STF-09, DEC-140 (1), (2)): on a PENDING row whose `canWithdraw` is true (the user
  created it), the tab Chờ duyệt shows "Thu hồi" and "Xem chi tiết" in place of "Từ chối" and "Duyệt", and the row
  menu has "Thu hồi" (variant destructive); both open the confirmation of [SCR-STF-03](SCR-STF-03.md), whose 204 shows
  the toast "Đã thu hồi tài khoản {name}." with "Tạo tài khoản mới" and reloads the list and the count. "Duyệt", "Từ
  chối" and "Thu hồi" of a row send the item's `version` as `If-Match`; a 412 shows its `detail` in a toast, closes the
  dialog and reloads the list and the count. The approval of several accounts sends `items` of `{id, version}`, and an
  item refused with 412 shows in the result like any other refusal.
- **The tab Chờ duyệt** (STF-09, R-USR-17, UC-140): a line under the filters says why the accounts wait, and the table
  shows the checkbox (gap 12), STT, Họ tên with the email under it, Vai trò, Người tạo, Ngày tạo (gap 13) and Thao tác:
  the buttons "Từ chối" and "Duyệt" side by side, or "Xem chi tiết". Until gap 13 closes, the buttons show for a user
  whose `permissions` hold `staff.approve` (only to hide them, `docs/FRONTEND.md`), the columns Người tạo and Ngày tạo
  and the creator row of the confirmations are left out, and an approval the API refuses (the caller's own request, a
  role outside the caller's scope) shows its 409 `detail`. Once gap 13 closes, the list item of a PENDING account
  carries `canApprove`, `createdBy` and `createdAt`, as drawn: the buttons show only on a row whose `canApprove` is
  true, the API working out who may approve (R-USR-17, R-SEC-12), and the columns and the creator row show. Each action
  opens a confirmation that repeats the account (name, email, roles, and with gap 13 the creator and time). "Duyệt"
  sends `POST /api/v1/admin/staff/{id}/approve`, "Từ chối" sends `POST /api/v1/admin/staff/{id}/reject`; the confirm
  button is disabled with a spinner while the request runs. After a 200 or 204 the dialog closes, a toast says "Đã duyệt
  tài khoản {name}." with "Nhân viên đăng nhập được bằng mật khẩu mặc định." or "Đã từ chối tài khoản {name}.", and the
  list and the count reload, so the row leaves the tab. A 404 or 409 (not PENDING any more, the caller's own request, an
  email taken in Firebase, a role outside the caller's scope) shows its `detail` in a toast; a 500 or no answer shows
  the texts of *Conventions*; each closes the dialog and reloads the list and the count. The tab empty: "Không còn tài
  khoản chờ duyệt." with "Xem tất cả nhân viên".
- **Approving or rejecting several accounts** (drawn ahead for gap 12, at the owner's request): on the tab Chờ duyệt
  the bar offers "Từ chối {count}" and "Duyệt {count}", each with a confirmation listing the accounts with their roles,
  then the same progress and result as a job. Until gap 12 closes, the tab has no checkboxes and no bar.
- **Accounts with the Admin role** (STF-06, R-USR-14): a user who does not hold the Admin role never sees them: not in
  the list, the search, the role filter or an export; their page answers 404. Until STF-06 merges, the list still shows
  them and the API refuses every change on them (R-TX-11).
- **Empty**: no account at all → "Chưa có tài khoản nhân viên."; nothing matches the filters or the search → "Không
  có nhân viên phù hợp với bộ lọc." with a button to clear the filters and the search.

A 400 on a value of the URL means the URL was edited by hand: the list falls back to the default filters.

## Layout

- **Design**: <https://claude.ai/artifact/J3zZ3ffqs7PTWEcu83nFfk>, drawn 2026-11-05 with the design system of
  2026-11-05; approved by the merge of the pull request that wrote this subsection (DEC-119); changed 2026-11-05
  (the columns STT and Thao tác, the soft badges), approved by the merge of the pull request of that change. The
  canvas shows the frame of SCR-ADM-01 for context only; this Layout does not approve it. Changed 2026-11-13 on
  <https://claude.ai/artifact/VGfMXvw1CSvzx3ptVKWHbZ>, drawn with the design system of 2026-11-13, for the whole page
  at the owner's request (usability, consistency, visual hierarchy, feedback, accessibility, the approver's needs):
  the status links with the count waiting, the search and filters of STF-06, the selection, bar, progress and result
  of STF-07 and STF-08, the export, the tab Chờ duyệt of STF-09, the role action and the approval of several accounts
  drawn ahead (gaps 7 and 12); approved by the merge of the pull request of that change. Loading and error keep the
  first canvas.
- **Change of 2026-11-14** (DEC-140): "Thu hồi" on the creator's PENDING rows, drawn on the canvas of the change of
  SCR-STF-03 (<https://claude.ai/artifact/M9wKyy8fLC34Rh1jzHXHF6>, artboard "ListPending"); approved by the merge of
  the pull request that wrote this bullet, built by STF-09. Tree additions: `staff-pending-row.tsx` shows, when
  `canWithdraw` is true, Button `destructive-soft` sm `staff.account.withdraw` (Undo2Icon) with its `aria-label`
  `actions.withdrawLabel` "Thu hồi {name}" (*new*) and the link "Xem chi tiết"; `staff-pending-actions.tsx` adds
  `actions.withdraw` "Thu hồi" (*new*, variant destructive, Undo2Icon) in place of the two items of the approval;
  both open `staff-withdraw-dialog.tsx` of SCR-STF-03; `hooks/use-staff-approval.ts` sends the item's `version` as
  `If-Match` and handles 412 as above.
- **Builds**: the screen exists in `web/`, so each feature's run builds its part of this Layout in its `feat` pull
  request, in the order of `docs/STATUS.md`: STF-09 the status links, the tab Chờ duyệt, the column Email moved under
  the name, the badge Chờ duyệt and the PENDING items of the row menu; STF-06 the search over the API, the filters, the
  chips, the column Đăng nhập gần nhất, "Chưa đổi mật khẩu", the page size and the numbered pages; STF-07 the checkbox
  column, the bar with "Khóa" and "Khôi phục", the progress and the result; STF-08 "Xuất CSV", "Xuất {count}" and the
  file dialog. The creator columns and `canApprove` wait for gap 13. "Sửa vai trò" waits for STF-11 and the Layout of
  SCR-STF-03 that draws its dialog; the approval of several accounts waits for gap 12.
- **Artboards of the 2026-11-13 change**: desktop (1440 px), signed in as the Admin: the tab Tất cả with two rows
  selected and the bar (in Play, the status links, the checkboxes, "Bỏ chọn" and "Bộ lọc khác" work); the popover
  "Bộ lọc khác" open; the confirmation of "Khóa 2"; the progress at 2 of 3 with one refusal; the result with the
  toast of a job that ran in the background; the confirmation of an export by the filters with two chips in use; the
  file ready; the menu of an ACTIVE row with "Sửa vai trò"; the menu of a PENDING row; the tab Chờ duyệt with two
  selected; the confirmations "Duyệt" and "Từ chối" of one account; after an approval, the toast and the count down to
  1; for gap 12 the confirmations of several accounts and a result where one of two could not be approved. Signed in
  as the fixture Store Manager: no Admin row, no "Xuất CSV" and no "Thêm nhân viên", with filters in use. Signed in as
  the fixture IT Support: the tab Chờ duyệt with "Xem chi tiết" only. Narrow (390 px): the tab Tất cả as a list of
  blocks with checkboxes and the bar stuck to the bottom; the tab Chờ duyệt as blocks with full-width buttons. The
  tab Chờ duyệt empty. Dark: the tab Chờ duyệt with the confirmation "Từ chối" (DEC-123 (5)).
- **Artboards of the first canvas** (still the design of these states): desktop and narrow at 2026-11-05; loading,
  empty (no account), empty (nothing matches the filters), error. No permission is the page of SCR-ADM-01
  (`RequirePermission` with `staff.read`).
- **shadcn**: the first build added `table badge select pagination`; `dropdown-menu`, `tooltip`, `alert-dialog`,
  `checkbox`, `dialog`, `popover`, `calendar`, `field`, `input-group`, `empty`, `skeleton` and `sonner` are installed.
  The change adds only `shadcn add progress` (STF-07, on the approved `radix-ui`, no new package; committed alone
  first, DEC-116 (1)). The status links are links styled like shadcn's segmented control (`bg-muted` track, the current
  one `bg-background` with a shadow), not `tabs`: they change the URL and have no panel.
- **Theme**: soft badges, as the **Theme** bullet of the Layout of SCR-STF-03 says: ACTIVE `success` with
  `--success-soft` and `--success-soft-foreground`, DISABLED `secondary` (grey), PENDING `outline` with ClockIcon
  (the text tells it from ANONYMIZED, which no list shows); the count of the tab Chờ duyệt a Badge `default`.
- **Texts** (`messages/vi.json`): the keys of `staff.list` and `staff.account` that the STF-05 build wrote stay; new
  ones are marked *new*, under `staff.list` unless named.
  - STF-09: `description` "Tài khoản nhân viên của cửa hàng, cùng vai trò và trạng thái. Tìm và lọc trên toàn bộ danh
    sách, chọn nhiều tài khoản để khóa, khôi phục hoặc xuất, và duyệt tài khoản mới ở tab Chờ duyệt." (the text once
    STF-08 merges; each run writes the part that exists after it: STF-09 "Tài khoản nhân viên của cửa hàng, cùng vai
    trò và trạng thái. Xem, khóa hoặc khôi phục một tài khoản, và duyệt tài khoản mới ở tab Chờ duyệt.", STF-06 adds
    the search and filters, STF-07 the selection with "khóa, khôi phục", STF-08 "hoặc xuất"); `status.label` "Trạng
    thái" (*new*, the visible label of the links), `status.pending` "Chờ duyệt" (*new*; the link and the badge) and the
    count's sr text `status.pendingCount` ", {count} tài khoản" (*new*; the visible number is `aria-hidden`);
    `pending.description` "Nhân viên chưa đăng nhập được cho tới khi tài khoản được duyệt. Kiểm tra vai trò và người tạo
    trước khi duyệt." (*new*); `columns.createdBy` "Người tạo", `columns.createdAt` "Ngày tạo" (*new*, gap 13); the
    narrow block's `pending.createdBy` "Tạo bởi {name} · {time}" (*new*); the row buttons `staff.account.approve`
    "Duyệt" and `staff.account.reject` "Từ chối" with their `aria-label`s `actions.approveLabel` "Duyệt {name}" and
    `actions.rejectLabel` "Từ chối {name}" (*new*), and the menu items `actions.approve` "Duyệt tài khoản" and
    `actions.reject` "Từ chối" (*new*); `staff.account.approveDialog.title` "Duyệt tài khoản này?", `.description` "Tài
    khoản được kích hoạt với mật khẩu mặc định. Nhân viên phải đổi mật khẩu ở lần đăng nhập đầu tiên." and `.submit`
    "Duyệt"; `staff.account.rejectDialog.title` "Từ chối tài khoản này?", `.description` "Tài khoản chờ duyệt bị xóa
    cùng hồ sơ và vai trò, không hoàn tác được. Muốn dùng lại email này, hãy tạo tài khoản mới." and `.submit` "Từ chối
    tài khoản" (*new*); the rows `staff.account.summary.name` "Họ tên", `.email` "Email", `.roles` "Vai trò",
    `.createdBy` "Người tạo" and its value `.createdByValue` "{name} · {time}" (*new*); `staff.account.cancel` "Hủy"
    (existing); the toasts `staff.account.approved` "Đã duyệt tài khoản {name}." with `staff.account.approvedHint` "Nhân
    viên đăng nhập được bằng mật khẩu mặc định." and `staff.account.rejected` "Đã từ chối tài khoản {name}." (*new*);
    `pendingEmpty.title` "Không còn tài khoản chờ duyệt.", `pendingEmpty.description` "Tài khoản mới do người không có
    quyền duyệt tạo sẽ hiện ở đây." and `pendingEmpty.showAll` "Xem tất cả nhân viên" (*new*).
  - STF-06: `search` "Tìm nhân viên" (changed from "Lọc theo tên, mã hoặc email"), its placeholder `searchPlaceholder`
    "an@example.com" (kept) and its hint `searchHint` "Tên, email hoặc mã nhân viên, từ 2 ký tự; tìm trên toàn bộ danh
    sách, không phân biệt dấu." (changed, always shown); `role.label` "Vai trò", `role.all` "Tất cả vai trò" (*new*);
    `filters.open` "Bộ lọc khác", its count's sr text `filters.count` ", {count} đang dùng", `filters.title` "Bộ lọc
    khác", `filters.description` "Ngày theo giờ Việt Nam, gồm cả ngày đầu và ngày cuối. Để \"Không giới hạn\" ở một
    đầu để không chặn đầu đó.", `filters.joined` "Ngày vào làm", `filters.left` "Ngày nghỉ việc", `filters.lastLogin`
    "Đăng nhập gần nhất" with its hint `filters.lastLoginHint` "Không gồm tài khoản chưa đăng nhập lần nào.",
    `filters.from` "Từ ngày", `filters.to` "Đến ngày", `filters.anyDate` "Không giới hạn", `filters.rangeOrder` "Ngày
    bắt đầu phải trước hoặc bằng ngày kết thúc.", `filters.mustChangePassword` "Chưa đổi mật khẩu mặc định" with its
    hint `filters.mustChangePasswordHint` "Tài khoản chưa đặt mật khẩu riêng sau khi được tạo.", `filters.clearDates`
    "Xóa các ngày", `filters.apply` "Áp dụng" (*new*); the chips `filters.chip.joined` "Ngày vào làm: {range}",
    `filters.chip.left` "Ngày nghỉ việc: {range}", `filters.chip.lastLogin` "Đăng nhập gần nhất: {range}",
    `filters.chip.range` "{from} – {to}", `filters.chip.mustChangePassword` "Chưa đổi mật khẩu mặc định",
    `filters.chip.remove` "Bỏ lọc {name}" (*new*, the chip button's `aria-label` and tooltip), `filters.active` "Bộ lọc
    đang áp dụng" (*new*, the chips' group name); `clearFilters` "Xóa bộ lọc" (existing); `columns.lastLogin` "Đăng
    nhập gần nhất", `neverSignedIn` "Chưa đăng nhập", `mustChangePassword` "Chưa đổi mật khẩu" (*new*); `pageSize` "Số
    dòng mỗi trang", `range` "Hiển thị {from}–{to} trên {total} nhân viên" (*new*; `total` and `page` stay for the
    narrow pager).
  - STF-07: `bulk.label` "Thao tác với tài khoản đã chọn", `bulk.selected` "Đã chọn {count} tài khoản", `bulk.clear`
    "Bỏ chọn", `bulk.select` "Chọn {name}", `bulk.selectAll` "Chọn mọi tài khoản trong trang", `bulk.disable` "Khóa
    {count}", `bulk.restore` "Khôi phục {count}" (*new*); `bulk.disableDialog.title` "Khóa {count} tài khoản?",
    `.description` "Nhân viên bị đăng xuất ngay và không đăng nhập được cho tới khi được khôi phục. Hệ thống khóa từng
    tài khoản; tài khoản nào không khóa được sẽ giữ nguyên và có lý do trong kết quả.", `.leavingHint` "Đánh dấu khi
    các nhân viên đã nghỉ, để ghi một ngày làm việc cuối cùng chung cho cả nhóm.", `.leftDateHint` "Không sau hôm nay.
    Tài khoản có ngày vào làm sau ngày này sẽ không khóa được và có lý do trong kết quả.", `.submit` "Khóa {count} tài
    khoản", `.list` "Tài khoản sẽ khóa" (*new*; the checkbox and the field reuse `disableDialog.leaving` and
    `columns.leftDate`); `bulk.restoreDialog.title` "Khôi phục {count} tài khoản?", `.description` "Nhân viên đăng nhập
    lại được bằng mật khẩu đang có, và ngày nghỉ việc được xóa. Tài khoản nào không khôi phục được sẽ có lý do trong
    kết quả.", `.submit` "Khôi phục {count} tài khoản", `.list` "Tài khoản sẽ khôi phục" (*new*); the job, under
    `staff.list.jobs` (*new*): `progress.disable` "Đang khóa {count} tài khoản", `progress.restore` "Đang khôi phục
    {count} tài khoản", `progress.export` "Đang xuất danh sách", `progress.approve` "Đang duyệt {count} tài khoản",
    `progress.reject` "Đang từ chối {count} tài khoản", `progress.description` "Hệ thống xử lý từng tài khoản. Bạn có
    thể đóng hộp này: việc vẫn chạy và có thông báo khi xong.", `progress.count` "Đã xử lý {done} / {count}",
    `progress.list` "Tiến độ từng tài khoản", `progress.running` "Đang xử lý…", `progress.background` "Chạy nền";
    `result.disable` "Đã khóa {done} trên {count} tài khoản", `result.restore` "Đã khôi phục {done} trên {count} tài
    khoản", `result.approve` "Đã duyệt {done} trên {count} tài khoản", `result.reject` "Đã từ chối {done} trên {count}
    tài khoản" (each also the toast, with a final "."), `result.description` "Tài khoản không xử lý được vẫn giữ
    nguyên. Xem lý do bên dưới.", `result.list` "Kết quả từng tài khoản", `result.done.disable` "Đã khóa",
    `result.done.restore` "Đã khôi phục", `result.done.approve` "Đã duyệt", `result.done.reject` "Đã từ chối",
    `result.failed` "Không xử lý được: {detail}", `result.failedJob` "Việc dừng giữa chừng. Các tài khoản chưa có kết
    quả giữ nguyên.", `result.export` "Đã xuất {count} nhân viên.", `result.exportFailed` "Xuất danh sách không
    xong. Hãy xuất lại.", `result.download` "Tải file", `result.show` "Xem kết quả", `result.close` "Đóng";
    `bulk.selectPage` "Chọn cả trang" (the narrow list's checkbox label).
  - STF-08: `export.open` "Xuất CSV", `bulk.export` "Xuất {count}" (*new*); `export.confirm.title` "Xuất {count} nhân
    viên khớp bộ lọc?", `.description` "File CSV gồm họ tên, email, mã nhân viên, trạng thái, vai trò, ngày vào làm,
    ngày nghỉ việc và lần đăng nhập gần nhất; không có số điện thoại. Mỗi lần xuất được ghi lại với tên bạn.",
    `.limits` "File chỉ bạn tải được, trong 24 giờ sau khi xong. Tối đa 50.000 nhân viên mỗi lần.", `.submit` "Xuất
    CSV", the rows `export.confirm.search` "Tìm kiếm" (the search text, a dialog is not the URL), `.status`, `.role`,
    `.joined`, `.left`, `.lastLogin`, `.password` "Mật khẩu"
    (*new*; the others reuse the filter labels, a filter not in use shows "Tất cả" or is left out); `export.ready.title`
    "File đã sẵn sàng", `.description` "{count} nhân viên. Chỉ bạn tải được file này, tới {time}.", `.format` "CSV
    UTF-8, mở được bằng Excel", `.download` "Tải file CSV", `.close` "Đóng" (*new*).
  - Gap 7, drawn ahead: `actions.editRoles` "Sửa vai trò" (*new*, written by the run of STF-11).
  - Gap 12, drawn ahead (not built until the Decision): `bulk.approve` "Duyệt {count}", `bulk.reject` "Từ chối
    {count}", `bulk.selectAllPending` "Chọn mọi tài khoản duyệt được trong trang", `bulk.approveDialog.title` "Duyệt
    {count} tài khoản?", `.description` "Các tài khoản được kích hoạt với mật khẩu mặc định. Hệ thống duyệt từng tài
    khoản; tài khoản nào không duyệt được sẽ giữ trạng thái Chờ duyệt và có lý do trong kết quả.", `.submit` "Duyệt
    {count} tài khoản", `bulk.rejectDialog.title` "Từ chối {count} tài khoản?", `.description` "Các tài khoản chờ duyệt
    bị xóa cùng hồ sơ và vai trò, không hoàn tác được.", `.submit` "Từ chối {count} tài khoản" (*new*); the result
    uses `jobs.result.*`.

  No cell is cut short on a wide screen (the cells do not wrap and the table box scrolls sideways); the icon-only
  buttons (a row's menu, a chip's remove button, the narrow pager) have a Tooltip with their name.
- **Component tree** (DEC-118 (3)), under `web/src/features/identity/staff-accounts/components/`; the route
  `app/(admin)/admin/(shell)/staff/page.tsx` composes the layout with the sections inside `RequirePermission`
  (`staff.read`); the feature of each new or changed piece is in parentheses:

  ```text
  staff-list-layout.tsx              slots: header | filters | list — one Card (the h1 in CardTitle); narrow: the
  │                                  filters stack and the list is blocks (STF-07 changes the narrow list)
  ├── staff-list-header-section.tsx  CardHeader: CardTitle h1 "Nhân viên", CardDescription `description`; CardAction:
  │   │                              "Xuất CSV" (STF-08) and "Thêm nhân viên" — card, button
  │   ├── staff-add-button.tsx       the link to SCR-STF-02 with `staff.create`, full width on a narrow screen
  │   └── staff-export-button.tsx    (STF-08) Button outline (DownloadIcon) with `staff.export`, opening
  │                                  `staff-export-dialog.tsx` with the criteria of `hooks/use-staff-list.ts`
  ├── staff-list-filters-section.tsx the status links, the search, the role select, "Bộ lọc khác" and the chips; the
  │   │                              route (a server component with `generateMetadata`) wraps both sections in
  │   │                              `StaffListProvider` (`hooks/staff-list-context.tsx`), which calls
  │   │                              `hooks/use-staff-list.ts` once and gives its state to this section and the table
  │   │                              section — field, input-group, select
  │   ├── staff-status-nav.tsx       (STF-09) a `nav` labelled by the visible `status.label` (aria-labelledby): four
  │   │                              Links in a `bg-muted` track, the current one `aria-current="page"`; the count a
  │   │                              Badge `default` with its number `aria-hidden` and `status.pendingCount` sr-only,
  │   │                              hidden at 0, from `hooks/use-pending-count.ts`; scrolls sideways on a narrow
  │   │                              screen, the current link scrolled into view; a link drops `page` and `sort`
  │   ├── ../hooks/use-pending-count.ts (STF-09) the list query with `status=PENDING&pageSize=1` and its
  │   │                              `totalCount`, invalidated with the list after an approval, a rejection or a job
  │   ├── staff-search-field.tsx     the Field `search` (InputGroup with SearchIcon, placeholder `searchPlaceholder`,
  │   │                              FieldDescription `searchHint`); STF-06: debounced 300 ms, sent from 2 characters,
  │   │                              kept in the page only — field, input-group
  │   ├── staff-role-filter.tsx      (STF-06) the Field `role.label`: Select of `role.all` and the role options of
  │   │                              `GET /staff/role-options`, setting `roleId` and dropping `page` — select, field
  │   ├── staff-more-filters.tsx     (STF-06) Button outline (SlidersHorizontalIcon) `filters.open` with the count
  │   │                              Badge, opening a Popover (align start, 380 px): PopoverHeader `filters.title` and
  │   │                              its description, three fieldsets of two `staff-date-picker.tsx` each, the Checkbox
  │   │                              `filters.mustChangePassword` with its FieldDescription, then "Xóa các ngày" (ghost)
  │   │                              and "Áp dụng" (default); a range whose start is after its end shows
  │   │                              `filters.rangeOrder` under its fieldset (FieldError) and "Áp dụng" sends nothing —
  │   │                              popover, checkbox, field, button
  │   ├── staff-date-picker.tsx      (STF-06) presentational — the Popover and Calendar of `staff-left-date-field.tsx`
  │   │                              moved here, with optional bounds and the empty text `filters.anyDate`; the left
  │   │                              date field and the bulk dialog use it too — popover, calendar, button
  │   └── staff-filter-chips.tsx     (STF-06) a group named `filters.active`: one chip per filter in use (an outline
  │                                  Badge with a Button ghost icon-xs XIcon named `filters.chip.remove`, in a
  │                                  Tooltip) and "Xóa bộ lọc" (Button link) — badge, button, tooltip
  └── staff-list-table-section.tsx   renders what `hooks/use-staff-list.ts` gives: the query (GET /staff, from STF-06
      │                              `POST /staff/search` with the URL's filters and the page's `q`), the selection
      │                              (STF-07, cleared on any change of the query) and the URL changes; under 150
      │                              lines, it only picks the child of the tab and the state — empty, skeleton
      ├── staff-bulk-bar.tsx         (STF-07) a toolbar named `bulk.label` while a row is selected: `bulk.selected`
      │                              (aria-live polite), "Bỏ chọn" (ghost), then by tab and permission "Xuất {count}"
      │                              (outline, STF-08), "Khôi phục {count}" (outline, RotateCcwIcon) and "Khóa {count}"
      │                              (`destructive-soft`, LockIcon); on the tab Chờ duyệt (gap 12) "Từ chối {count}"
      │                              (`destructive-soft`) and "Duyệt {count}" (default); on a narrow screen stuck to the
      │                              bottom, its buttons in a grid — button
      ├── staff-bulk-dialog.tsx      (STF-07) AlertDialog of a bulk action: title and description of its action, the
      │                              list of the accounts with their role Badges (`.list`, scrolls past 240 px); for
      │                              "Khóa" the Checkbox `disableDialog.leaving` with `.leavingHint` and, when ticked,
      │                              `staff-date-picker.tsx` labelled `columns.leftDate` with `.leftDateHint` (not after
      │                              today); "Hủy" and the submit, disabled with LoaderCircleIcon while it is sent; the
      │                              approval of gap 12 uses it too — alert-dialog, checkbox, field
      ├── staff-job-dialog.tsx       (STF-07) Dialog of a job: while running `jobs.progress.*` with the Progress
      │                              (`aria-labelledby` its count) and `jobs.progress.list`, then `jobs.result.*`: one
      │                              line per account, CircleCheckIcon and its done text, or CircleAlertIcon
      │                              (destructive) and `jobs.result.failed` with the item's `detail`; "Chạy nền"
      │                              (outline) while running, "Đóng" at the end; for an export the end is
      │                              `staff-export-ready.tsx` — dialog, progress, button
      ├── ../hooks/use-staff-job.ts  (STF-07) starts a job (the request of its action with an `Idempotency-Key` from
      │                              `newIdempotencyKey`, made once per confirmation), polls `GET /jobs/{id}` every
      │                              1.5 s until Completed or Failed, keeping the running jobs in `sessionStorage` (id,
      │                              kind, count; read in try/catch) to resume on return, then invalidates the list
      │                              and the count and, if the dialog was closed, shows the toast `jobs.result.*` with
      │                              the action `jobs.result.show` (an export: `jobs.result.export` with
      │                              `jobs.result.download`); a 400 or 429 (or 409 for an export) shows the problem's
      │                              `detail` in a toast and starts nothing
      ├── staff-export-dialog.tsx    (STF-08) AlertDialog `export.confirm.*` with the count and a `dl` of the filters in
      │                              use, "Hủy" and "Xuất CSV" — alert-dialog
      ├── staff-export-ready.tsx     (STF-08) the end of an export in `staff-job-dialog.tsx`: `export.ready.*`, the
      │                              file's name from Content-Disposition, `export.ready.format`, the expiry time
      │                              (finished + 24 h, Asia/Ho_Chi_Minh), "Đóng" and "Tải file CSV" (default,
      │                              DownloadIcon), which fetches `GET /jobs/{id}/file` through the generated client and
      │                              saves the Blob; STF-08 changes the mutator `src/lib/api/api.ts` (and
      │                              `client.ts`) to return `response.blob()` for `text/csv`, since `text()` drops the
      │                              BOM; a refusal shows its `detail` in a toast — button
      ├── staff-list-table.tsx       the Table of the tabs Tất cả, Đang hoạt động and Đã khóa in a box with
      │                              role="region", tabIndex 0 and aria-label `tableLabel` on a wide screen: the header
      │                              checkbox (STF-07, `bulk.selectAll`), STT, Họ tên, Mã nhân viên, Vai trò, Trạng
      │                              thái, Ngày vào làm, Đăng nhập gần nhất (STF-06), Ngày nghỉ việc and the sr-only
      │                              `columns.actions`; its rows `staff-list-row.tsx`; on a narrow screen (STF-07) the
      │                              same rows as a list of blocks with `bulk.selectPage` above — table, checkbox
      ├── staff-pending-table.tsx    (STF-09) the same box for the tab Chờ duyệt: `pending.description` above, the
      │                              columns STT, Họ tên, Vai trò, Người tạo and Ngày tạo (gap 13), Thao tác (the
      │                              checkbox of gap 12 later), no sort; its rows `staff-pending-row.tsx`; on a narrow
      │                              screen a list of blocks; empty: Empty `pendingEmpty.*` (CircleCheckIcon) with "Xem
      │                              tất cả nhân viên" (outline), the link Tất cả — table, empty
      ├── staff-pending-row.tsx      (STF-09) presentational — one PENDING account: its cells, or its block on a narrow
      │                              screen (name and email, the role Badges and `pending.createdBy`, the two buttons
      │                              side by side full width); by the rule of the tab (gap 13), Button
      │                              `destructive-soft` sm "Từ chối" and Button outline sm "Duyệt" (CheckIcon), each
      │                              with its `aria-label`, opening the two dialogs below; otherwise the link "Xem chi
      │                              tiết" (outline sm) — button
      ├── staff-approve-dialog.tsx   (STF-09) AlertDialog `approveDialog.*` with a `dl` of the account (`summary.*`:
      │                              name, email, role Badges, and `createdByValue` once gap 13 closes), "Hủy" and the
      │                              submit (default), disabled with LoaderCircleIcon while the request of
      │                              `hooks/use-staff-approval.ts` runs; reusable by SCR-STF-03 once its Layout draws
      │                              the approval — alert-dialog
      ├── staff-reject-dialog.tsx    (STF-09) the same with `rejectDialog.*` and the submit `destructive-soft`
      │                              — alert-dialog
      ├── ../hooks/use-staff-approval.ts (STF-09) the two requests; on 200 or 204: closes the dialog, a toast
      │                              (`approved` with `approvedHint`, or `rejected`) and invalidates the list and the
      │                              count; on 404 or 409 the problem's `detail` in a toast, on 500 or no answer the
      │                              texts of *Conventions*, each closing the dialog and invalidating both
      ├── staff-list-pagination.tsx  footer: `range` (STF-06; `total` until then), the Field `pageSize` (Select 20, 50,
      │                              100; STF-06) and the Pagination with numbered pages (STF-06); on a narrow screen
      │                              the two chevrons in their Tooltips and `page`, as built — pagination, select
      ├── staff-sort-header.tsx      presentational — a header cell whose button sorts, with aria-sort and its arrow;
      │                              "Đăng nhập gần nhất" sorts with STF-06
      ├── staff-roles-cell.tsx       presentational — one Badge (secondary) per role name — badge
      ├── staff-status-badge.tsx     presentational — Badge "Đang hoạt động", "Đã khóa", and (STF-09) "Chờ duyệt"
      │                              (`outline` with ClockIcon 12px, aria-hidden); (STF-06) "Chưa đổi mật khẩu" under
      │                              it, muted, with KeyRoundIcon — badge
      └── staff-list-row.tsx         presentational — one account's cells: the checkbox (STF-07, `bulk.select`, none
          │                          on the user's own row or a PENDING row), STT, Họ tên with `email` under it
          │                          (STF-09), the others and the actions; the selected row `bg-muted`
          │                          (the checkbox carries the selection); its skeleton row has a bar per cell
          ├── staff-row-actions.tsx  a Button ghost icon (MoreHorizontalIcon) named `actions.label`, in a Tooltip
          │                          `columns.actions`, opening a DropdownMenu (align end): "Xem chi tiết" (EyeIcon);
          │                          (gap 7, built by STF-11) "Sửa vai trò" (UserCogIcon) with `staff.update` on an
          │                          ACTIVE or DISABLED row of another user, a link to SCR-STF-03 that opens its role
          │                          dialog; with `staff.disable`, on another user's account, a separator, then on
          │                          ACTIVE "Khóa tài khoản" (variant destructive), on DISABLED "Khôi phục tài
          │                          khoản" and "Khóa lại", opening `staff-disable-dialog.tsx` and
          │                          `staff-restore-dialog.tsx` of SCR-STF-03; (STF-09) on a PENDING row the items of
          │                          `staff-pending-actions.tsx` — dropdown-menu, tooltip, button
          └── staff-pending-actions.tsx (STF-09) the menu items of a PENDING row, split for size: when the rule of
                                     the tab Chờ duyệt allows it (gap 13), a separator, `actions.approve`
                                     (CheckIcon) and `actions.reject` (variant destructive, XIcon), opening
                                     `staff-approve-dialog.tsx` and `staff-reject-dialog.tsx` — dropdown-menu
  ```
