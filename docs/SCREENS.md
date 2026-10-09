# Screens

The screens of the web app (`web/`), per feature: route, who opens it, what it shows, what it sends to the API and
how each answer of the API is shown. Structure, stack and conventions are in `docs/FRONTEND.md`; this file says
*what* each screen does, `docs/FRONTEND.md` says *how* screens are built. `/implement-feature` builds the web part of
a feature only when this file describes its screens (step 7), and only once the screen's row below says nothing
blocks it.

A screen is written here before its feature is built on the web, from the API that exists: every field names the
property of the OpenAPI contract (`api/openapi/ZenGear.Api.json`) it shows or sends. The API decides; the web checks
only the shape of the input (required, length, format) with the limits below, which mirror the validators of the API.

## Conventions for every screen

- **Text**: every label, message and button comes from `web/messages/vi.json` (next-intl). The texts quoted below
  are the Vietnamese wording for that file; terms of `docs/GLOSSARY.md` keep its Vietnamese label.
- **Access**: a staff screen needs a signed-in staff account; the permission of each screen is the one its endpoints
  require. Menus and buttons the user's permissions do not allow are hidden (`GET /api/v1/me`), and the API still
  answers 403 (see *Known gaps*, 1).
- **Errors**: a 400 validation problem puts each message of `errors` under its form field and keeps what was typed;
  any other problem shows the message of its `type` (table of each screen) in a toast, or `detail` when the type has
  no message; `ruleId` is never shown. 401 signs the user out to the sign-in page; 403
  `/problems/permission-denied` shows the no-permission page; 403 `/problems/recent-login-required` asks for the
  password again and retries once (DEC-094); 403 `/problems/password-change-required` (any call while the default
  password is unchanged) goes to SCR-AUT-02; 403 `/problems/account-disabled` (an account disabled while signed in,
  R-SEC-04) and 403 `/problems/account-not-registered` sign the user out to SCR-AUT-01 with the message of
  SCR-AUT-01.
- **Edits with an ETag** (`If-Match`): the form keeps the `ETag` of the loaded resource; 412 says "Dữ liệu đã được
  người khác thay đổi. Đang tải lại." and reloads the form.
- **States**: loading shows skeleton rows or fields, never an empty screen; an empty list says why it is empty; a
  button that sends a request is disabled until the answer comes, so a double click sends one request.
- **Dates** `yyyy-MM-dd` from the API are shown `dd/MM/yyyy`; timestamps are shown in Asia/Ho_Chi_Minh.
- **Accessibility**: WCAG 2.2 AA; every form works with the keyboard alone, every field has a visible label, errors
  are announced to screen readers.

## Screen list

| ID | Screen | Route | Part | Feature | Permission | Blocked by |
|---|---|---|---|---|---|---|
| SCR-ADM-01 | Admin layout and menu | `/admin` (layout) | Admin | AUT-04 | signed-in staff | Web shell, gaps 1 and 4 |
| SCR-AUT-01 | Staff sign-in | `/admin/sign-in` | Admin | AUT-01 | — | Web shell, gap 4 |
| SCR-AUT-02 | Required password change | `/admin/change-password` | Admin | STF-03 | signed-in staff | Web shell, gap 4 |
| SCR-ACC-01 | My account | `/admin/account` | Admin | STF-03 | signed-in staff | Web shell, gaps 1 and 4 |
| SCR-STF-01 | Staff list | `/admin/staff` | Admin | STF-05 | `staff.read` | Web shell, gaps 1 and 4 (role filter: gap 2) |
| SCR-STF-02 | New staff account | `/admin/staff/new` | Admin | STF-01 | `staff.create` | Web shell, gaps 1, 2 and 4 |
| SCR-STF-03 | Staff account | `/admin/staff/[id]` | Admin | STF-01, STF-02 | `staff.read`; actions `staff.disable` | Web shell, gaps 1 and 4 |
| SCR-LOC-01 | Business location | `/admin/settings/business-location` | Admin | LOC-01 | `business_location.update` | Web shell, gaps 1, 3 and 4 |

The order of DEC-104 holds: the tooling pull request (package checks, the job `web-test`), then the API pull request
(gap 4), then the web shell (`web/` set up with SCR-ADM-01); until then no screen is built, even with its API ready.
Every admin screen also sits inside SCR-ADM-01, which needs gap 1. The POS and the storefront have no screen yet:
their features (catalog, stock, orders) have no API.

---

## SCR-ADM-01 Admin layout and menu

Every admin screen except the sign-in page sits in this layout.

- **Header**: the store name, the signed-in user's `name` (from `GET /api/v1/me`) with a menu: "Tài khoản của tôi"
  (SCR-ACC-01) and "Đăng xuất" (signs out of Firebase, clears the query cache, goes to SCR-AUT-01).
- **Menu**: "Nhân viên" (SCR-STF-01, needs `staff.read`), "Cài đặt" → "Địa điểm kinh doanh" (SCR-LOC-01, needs
  `business_location.update`). An entry the user cannot use is hidden.
- **Guard**: without a Firebase session, every admin route goes to SCR-AUT-01 with the requested path kept as
  `?next=`. When `GET /api/v1/me` says `mustChangePassword: true`, every admin route goes to SCR-AUT-02. When it says
  `isStaff: false`, the user is signed out with "Tài khoản này không phải tài khoản nhân viên."
- **No-permission page** (403): "Bạn không có quyền xem trang này." and a link back to the first menu entry the user
  can open.

## SCR-AUT-01 Staff sign-in

Staff sign in with their company email and password (UC-001).

| Field | Rule (web) |
|---|---|
| Email ("Email công ty") | required, email format |
| Password ("Mật khẩu") | required; shown as dots, with a show/hide button |

1. "Đăng nhập" signs in with Firebase (email and password).
2. Then `GET /api/v1/me`:
   - `mustChangePassword: true` → SCR-AUT-02;
   - otherwise the `?next=` path, or SCR-STF-01 when the user may open it, or SCR-ACC-01.

| Answer | Shown |
|---|---|
| Firebase: wrong email or password | "Email hoặc mật khẩu không đúng." (never which one) |
| Firebase: too many attempts | "Bạn đã thử quá nhiều lần. Vui lòng thử lại sau." |
| 403 `/problems/account-disabled` | "Tài khoản đã bị khóa. Vui lòng liên hệ quản lý cửa hàng." (then signs out) |
| 403 `/problems/account-not-registered` | "Tài khoản này chưa được đăng ký trong hệ thống." (then signs out) |

No "forgot password" link in Phase 1: a staff member whose password is lost asks the Admin (UC-001 has no reset
flow).

## SCR-AUT-02 Required password change

Shown alone, without the menu, while `mustChangePassword` is true (R-USR-03): the staff member replaces the default
password before anything else.

| Field | Rule (web) |
|---|---|
| New password ("Mật khẩu mới") | required, 12 to 128 characters (counted as Unicode code points); spaces count and are kept |
| Repeat ("Nhập lại mật khẩu mới") | the same as the new password (checked by the web only, never sent) |

Help text under the field: "Tối thiểu 12 ký tự. Không dùng mật khẩu mặc định, mật khẩu phổ biến hay chữ zengear."
The web does not check the blocklist or the default password: the API does (R-USR-11).

1. "Đổi mật khẩu" sends `POST /api/v1/me/change-password` `{ newPassword }`.
2. 204: every session of the account is revoked (DEC-094). The web signs out and shows SCR-AUT-01 with "Đã đổi mật
   khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới." The password is never kept after the request.

| Answer | Shown |
|---|---|
| 400 with `errors.newPassword` | The message under the field (too short, default password, blocklist, contains zengear) |
| 403 `/problems/recent-login-required` | A dialog "Vui lòng nhập lại mật khẩu hiện tại để tiếp tục": re-authenticates with Firebase, then sends the change again once |
| 409 `/problems/staff-account-required` | "Chỉ tài khoản nhân viên mới đổi mật khẩu tại đây." |

"Đăng xuất" is the only other action on this screen.

## SCR-ACC-01 My account

The staff member's own account (UC-007): display name and password; the email is read-only.

Loaded from `GET /api/v1/me` (keep its `ETag`).

| Field | API | Rule (web) |
|---|---|---|
| Display name ("Tên hiển thị") | `name` | required, at most 100 characters after trimming |
| Email | `email` | read-only, with the note "Email là tên đăng nhập. Liên hệ quản trị viên khi cần đổi." (gap 5) |

- "Lưu" sends `PATCH /api/v1/me` `{ name }` with `If-Match`; 200 shows "Đã lưu." and refreshes the header name.
- "Đổi mật khẩu" opens the form of SCR-AUT-02 in a dialog, with the same answers; after 204 the user is signed out the
  same way.

| Answer | Shown |
|---|---|
| 400 with `errors.name` | The message under the field |
| 412 | Reload, as in *Conventions* |

## SCR-STF-01 Staff list

All staff accounts with their status and roles, to find the one to open, disable or restore (STF-05, DEC-105).

Loaded from `GET /api/v1/staff`. The filters, sort and page are kept in the URL (`?status=&roleId=&sort=&page=`), so a
reload or a shared link shows the same list.

| Column | API | Shown as |
|---|---|---|
| Họ tên | `name` | link to SCR-STF-03 |
| Mã nhân viên | `employeeCode` | "—" when empty |
| Email | `email` | |
| Vai trò | `roles[].name` | one badge per role |
| Trạng thái | `status` | badge "Đang hoạt động" (ACTIVE) or "Đã khóa" (DISABLED) |
| Ngày vào làm | `joinedDate` | `dd/MM/yyyy`, "—" when empty |
| Ngày nghỉ việc | `leftDate` | `dd/MM/yyyy`, empty while employed |

- **Filters**: "Trạng thái" — Tất cả (no `status`), Đang hoạt động (`ACTIVE`), Đã khóa (`DISABLED`); "Vai trò" — every
  role (`roleId`), shown once the roles can be listed (gap 2).
- **Search box** "Lọc theo tên, mã hoặc email": filters the rows already loaded, in the browser; nothing is sent to
  the API, since names and emails stay out of the query string (DEC-105). With more than one page, the box says
  "Chỉ lọc trong trang đang xem."
- **Sort**: clicking "Họ tên", "Mã nhân viên" or "Ngày vào làm" sends `sort=name|employeeCode|joinedDate`, a second
  click the same with `-`; default `name` (Vietnamese order).
- **Paging**: 20 rows a page (`pageSize=20`), with the total from `totalCount`.
- **Button** "Thêm nhân viên" (SCR-STF-02), shown with `staff.create`.
- **Empty**: no account at all → "Chưa có tài khoản nhân viên."; nothing matches the filters → "Không có nhân viên
  phù hợp với bộ lọc." with a button to clear the filters.

A 400 means the URL was edited by hand: the list falls back to the default filters.

## SCR-STF-02 New staff account

The Admin (or a Store Manager holding `staff.create`) creates a staff account with one or more roles (UC-005).

| Field | API | Rule (web) |
|---|---|---|
| Email ("Email công ty") | `email` | required, email format, at most 255 characters |
| Họ tên | `name` | required, at most 100 characters after trimming |
| Mã nhân viên | `employeeCode` | required, at most 50 characters, no space at the start or end |
| Ngày vào làm | `joinedDate` | required; date picker, today by default |
| SĐT công việc | `workPhone` | optional; 8 to 15 digits, may start with + |
| Vai trò | `roleIds` | at least one, at most 20; multi-select of the roles (gap 2), Admin included for every caller: the web never checks a role name (R-SEC-03), and the API refuses a caller who may not give it (R-TX-11, row below) |

1. "Tạo tài khoản" sends `POST /api/v1/staff`.
2. 201 opens SCR-STF-03 of the new account with "Đã tạo tài khoản. Nhân viên đăng nhập bằng mật khẩu mặc định và
   phải đổi mật khẩu ngay lần đầu." The web never shows or knows the default password.

| Answer | Shown |
|---|---|
| 400 with `errors.<field>` | Under the field |
| 409 `/problems/email-already-registered` | Under Email: "Email này đã có tài khoản." |
| 409 `/problems/auth-account-exists` | Under Email: "Email này đã có tài khoản đăng nhập." |
| 409 `/problems/employee-code-taken` | Under Mã nhân viên: "Mã nhân viên đã được dùng." |
| 409 `/problems/role-required` | Under Vai trò: "Chọn ít nhất một vai trò." |
| 409 `/problems/role-not-for-staff` | Under Vai trò: "Vai trò này không dùng cho nhân viên." |
| 409 `/problems/admin-role-not-assignable` | Under Vai trò: "Chỉ quản trị viên mới gán được vai trò Admin." |

## SCR-STF-03 Staff account

One staff account, with the actions to disable or restore it (UC-005, UC-006).

Loaded from `GET /api/v1/staff/{id}`: name, email, employee code, joined date, left date, work phone, status, roles,
and "Chưa đổi mật khẩu mặc định" when `mustChangePassword` is true. No field is editable in Phase 1 (STF-01 creates,
STF-02 disables and restores).

**Action "Khóa tài khoản"** (with `staff.disable`, on an ACTIVE account; on a DISABLED account the same action is
named "Khóa lại", for the case of a 500 below; not shown on the user's own account — the API refuses it anyway, R-USR-12): a dialog

| Field | API | Rule (web) |
|---|---|---|
| Nhân viên nghỉ việc | — | checkbox; when ticked, the date field below is required |
| Ngày nghỉ việc | `leftDate` | date picker, not after today (Asia/Ho_Chi_Minh), not before the joined date |

with the warning "Nhân viên sẽ bị đăng xuất khỏi mọi thiết bị ngay lập tức." "Khóa" sends
`POST /api/v1/staff/{id}/disable` `{ leftDate }` (null when not ticked); 200 shows the account as returned.

**Action "Khôi phục tài khoản"** (with `staff.disable`, on a DISABLED account): a confirmation, then
`POST /api/v1/staff/{id}/restore`; 200 shows the account as returned.

| Answer | Shown |
|---|---|
| 404 | "Không tìm thấy tài khoản nhân viên." with a link to SCR-STF-01 |
| 400 with `errors.leftDate` | Under the date field |
| 409 `/problems/left-date-before-joined-date` | Under the date field: "Ngày nghỉ việc không được trước ngày vào làm." |
| 409 `/problems/left-date-in-future` | Under the date field: "Ngày nghỉ việc không được sau hôm nay." |
| 409 `/problems/own-account-not-lockable` | "Bạn không thể khóa tài khoản của chính mình." |
| 409 `/problems/last-active-admin` | "Không thể khóa tài khoản quản trị viên cuối cùng đang hoạt động." |
| 409 `/problems/admin-account-not-lockable` | "Chỉ quản trị viên mới khóa được tài khoản quản trị viên." |
| 409 `/problems/admin-account-not-restorable` | "Chỉ quản trị viên mới khôi phục được tài khoản quản trị viên." |
| 409 `/problems/account-anonymized` | "Tài khoản đã được ẩn danh, không thể khôi phục." |
| 409 `/problems/concurrency-conflict` | "Tài khoản vừa được thay đổi. Đang tải lại." and reloads |
| 500 on "Khóa" | The account may be disabled with its sessions still open (R-SEC-04), or not disabled at all: "Chưa khóa xong tài khoản. Vui lòng bấm Khóa lại để thử lại." The account is reloaded; "Khóa" (ACTIVE) or "Khóa lại" (DISABLED) sends the request again, which revokes the sessions again |

## SCR-LOC-01 Business location

The Admin edits the store's address, hotline and opening hours (UC-070, R-LOC-01). Phase 1 has one location: the
screen opens it directly; there is no list, no add and no delete.

Loaded from `GET /api/v1/business-locations` (the one item's `id`), then `GET /api/v1/business-locations/{id}`
(keep its `ETag`).

| Field | API | Rule (web) |
|---|---|---|
| Tên cửa hàng | `name` | required, at most 150 characters after trimming |
| Hotline | `phone` | required, 8 to 11 digits |
| Email | `email` | optional, email format, at most 255 characters |
| Địa chỉ | `addressDetail` | required, at most 255 characters after trimming (house number, street) |
| Tỉnh/Thành phố | `provinceId` | required; searchable select (gap 3) |
| Phường/Xã | `wardId` | required; searchable select of the wards of the chosen province (gap 3); cleared when the province changes |
| Giờ mở cửa hằng tuần | `regularHours[]` | for each day Thứ 2 … Chủ nhật (`day` 1–7): closed, or 1 to 3 periods `open`–`close` (`HH:mm`), each opening before it closes, periods of a day not overlapping; at least one period in the week |
| Lịch ngày lễ | `specialHours[]` | at most 100 rows: a date, then "Đóng cửa" (`closed: true`, no hours) or 1 to 3 non-overlapping periods `open`–`close` that replace that day's weekly hours; a date before today is dropped when saving, so the list says "Ngày đã qua sẽ được xóa khi lưu." |

"Lưu" sends `PUT /api/v1/business-locations/{id}` with every field and `If-Match`; 200 shows "Đã lưu." The note under
the title says "Website, phiếu in và email dùng thông tin này."

| Answer | Shown |
|---|---|
| 400 with `errors.<field>` | Under the field or the row of hours (a province no longer in use is under Tỉnh/Thành phố, a ward outside the province under Phường/Xã) |
| 412 | Reload, as in *Conventions* |

---

## Known gaps

What the API does not offer yet for the screens above. A screen whose row names a gap is built once the gap is closed.

1. **Permissions of the signed-in user.** `GET /api/v1/me` returns `id`, `name`, `email`, `isStaff` and
   `mustChangePassword`, but not the user's permissions, which `docs/FRONTEND.md` (Authentication) says the web uses
   to hide menus and buttons. Needed: the permission keys of the user in the answer of `GET /api/v1/me` (AUT-04),
   before SCR-ADM-01.
2. **Roles to choose from.** No endpoint lists the roles: SCR-STF-02 cannot offer them and SCR-STF-01 cannot filter by
   role. Comes with AUT-05 (Manage Role & Permission).
3. **Provinces and wards.** No endpoint lists provinces and their wards, and no feature describes one; the official
   list of Quyết định 19/2025/QĐ-TTg is not seeded (`api/seed/README.md`) and not in the repository yet; development
   has only the sample rows of `postman/fixtures/LOC-01.sql`. SCR-LOC-01 cannot offer the selects. The address
   book (ACC-02) and the checkout will need the same list. Needs a Decision.
4. **Contract for the web** (DEC-104): CORS exposing `ETag` and `Location`, the `ETag` response header in the OpenAPI
   document (`If-Match` is already there), and `enum` values for `status` and `sort` of `GET /api/v1/staff`.
5. **Changing a staff member's email.** No endpoint changes it, while UC-007 (2a) and R-USR-04 say the Admin does it in the
   staff management of STF-01, whose feature row has no such criterion; SCR-ACC-01 only points the staff member to the Admin. Needs a feature or a Decision when the Admin must do it.
