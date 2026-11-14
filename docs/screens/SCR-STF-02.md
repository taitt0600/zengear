# SCR-STF-02 New staff account

The Admin (or a Store Manager holding `staff.create`) creates a staff account with one or more roles (UC-005).

| Field | API | Rule (web) |
|---|---|---|
| Email ("Email công ty") | `email` | required, email format, at most 255 characters |
| Họ tên | `name` | required, at most 100 characters after trimming |
| Mã nhân viên | `employeeCode` | required, at most 50 characters, no space at the start or end |
| Ngày vào làm | `joinedDate` | required; date picker, today by default |
| SĐT công việc | `workPhone` | optional; 8 to 15 digits, may start with + |
| Vai trò | `roleIds` | at least one, at most 20; multi-select of the roles of `GET /api/v1/admin/staff/assignable-roles` (gap 2), the caller's manageable set (R-SEC-12): the web never checks a role name (R-SEC-03), and the API refuses a role outside that set (R-TX-11, R-SEC-12, row below) |

1. "Tạo tài khoản" sends `POST /api/v1/admin/staff`.
2. 201 opens SCR-STF-03 of the new account with "Đã tạo tài khoản. Nhân viên đăng nhập bằng mật khẩu mặc định và
   phải đổi mật khẩu ngay lần đầu." The web never shows or knows the default password.

| Answer | Shown |
|---|---|
| 400 with an item on `#/<field>` | Under the field |
| 409 `/problems/email-already-registered`, `/problems/auth-account-exists` (item on `#/email`) | Under Email |
| 409 `/problems/employee-code-taken` (item on `#/employeeCode`) | Under Mã nhân viên |
| 409 `/problems/role-required`, `/problems/role-not-for-staff`, `/problems/admin-role-not-assignable`, `/problems/role-out-of-scope` (item on `#/roleIds`) | Under Vai trò |

## Layout

- **Design**: <https://claude.ai/artifact/BBYRvdq6Pg2dRgMncSahww>, drawn 2026-11-14 with the design system of
  2026-11-13 (commit 563a162); approved by the merge of the pull request that wrote this section (DEC-119). The canvas
  shows the frame of SCR-ADM-01 for context only; this Layout does not approve it. It adds the creation of a PENDING
  account of STF-09 (DEC-128 (3), R-USR-17): a notice for a user without `staff.approve` and the toast of a 201 whose
  `status` is `PENDING`.
- **Builds**, each part by its feature:
  - the web-only run of STF-01 (`feat(web): … (STF-01)` on `feat/stf-01-web`, since the route is a placeholder page):
    the page, the form, its states and the toast of an ACTIVE account.
  - STF-09 (its web part, with its parts of SCR-STF-01 and SCR-STF-03): the notice "Tài khoản bạn tạo sẽ chờ duyệt" and
    the toast of a PENDING account; `staff.approve` and the status `PENDING` are not in the API yet (`UserStatus` has
    `ACTIVE`, `DISABLED`, `ANONYMIZED`). Whichever of the two runs comes second adds its part to the page as it is then.
- **Artboards**, desktop 1440 px unless named: the form signed in as the fixture admin (every assignable role, today in
  Ngày vào làm); the form signed in as the fixture IT Support, filled with the sample "Thu ngân mới (fixture)" (not a
  fixture yet; FX0010, thungan.moi@zengear.test, Cashier ticked), with the notice and the date picker open; errors under
  their fields (the 409 of Email, the 409 of Mã nhân viên and the check of Vai trò, from different submits, drawn
  together); the same in dark (DEC-123 (5): the `destructive` texts and borders); loading (the roles as Skeleton);
  empty (no role the user may give); error (the roles failed to load); sending (the button busy); the toasts after a 201,
  ACTIVE and PENDING, over SCR-STF-03 (not drawn again). Narrow (390 px): the IT Support form, the frame's menu as a
  sheet, the buttons stacked at full width with the primary on top. No permission is the page of SCR-ADM-01
  (`RequirePermission` with `staff.create`, as the placeholder has it). There is no icon-only button and no label cut
  short, so no tooltip: the date picker shows its day beside CalendarIcon, and the calendar's month buttons keep the
  `aria-label`s of `ui.calendar` as in the dialog "Khóa tài khoản" of SCR-STF-03.
- **shadcn**: no `add` (`card`, `field`, `input`, `checkbox`, `popover`, `calendar`, `button`, `alert`, `skeleton`,
  `empty` and `sonner` are installed).
- **Theme**: unchanged. The notice is the `alert` default variant with ClockIcon, the icon of the badge "Chờ duyệt" of
  SCR-STF-01; "Tạo tài khoản" is the Button `default` (the one primary action), "Hủy" the Button `outline`.
- **One card**: the form is one task, so it is one `Card` with the page's heading in its `CardTitle` (**Grouping**),
  at most 672px wide (`max-w-2xl`) and centred in the page area, one column of fields (`docs/FRONTEND.md`). Inside,
  two `FieldSet`s split by a `FieldSeparator` group the fields as SCR-STF-03 groups the account's cards: "Hồ sơ nhân
  viên" (Họ tên, Mã nhân viên, Ngày vào làm, SĐT công việc) and "Tài khoản" (Email công ty, Vai trò). The notice comes
  first in the content, before anything is typed. The actions sit in the `CardFooter`, right-aligned: "Hủy" (a link to
  SCR-STF-01) then "Tạo tài khoản".
- **Behaviour**: Ngày vào làm starts on today in Asia/Ho_Chi_Minh; SĐT công việc is marked "(không bắt buộc)" and is
  the only optional field; the other fields carry no mark. Vai trò is a group of `Checkbox`es, two columns from `sm`,
  one below, in the order of `GET /api/v1/admin/staff/assignable-roles` (all pages), none ticked. The web checks the
  shape with the limits of the table above before it sends; the API checks again. While the roles load, fail or come
  back empty, "Tạo tài khoản" is disabled; a failed load keeps what was typed and offers "Thử lại". While the request
  runs the button is disabled with LoaderCircleIcon and the fields stay as typed. A 201 opens SCR-STF-03 of the new
  account (`Location`, or the `id` of the body) with the toast of its `status`: `staff.create.created` for ACTIVE,
  `staff.create.createdPending` for PENDING (STF-09); the web reads the status the API answers and never decides it.
  An error moves the focus to the first field that shows one; an item no field shows goes in an `Alert`
  (`destructive`) at the top of the card's content, under the notice. The notice shows when `permissions` of
  `GET /api/v1/admin/me` lack `staff.approve`; it only informs, the API sets the status (R-USR-17).
- **Texts** (`messages/vi.json`), new ones marked *new*, under `staff.create` unless named: the page's title, breadcrumb
  entry and `CardTitle` `staff.list.add` "Thêm nhân viên"; the `CardDescription` `description` "Tạo tài khoản đăng
  nhập cho nhân viên mới và gán vai trò. Nhân viên đăng nhập bằng mật khẩu mặc định và phải đổi mật khẩu ngay lần
  đầu." (*new*); the legends `profile.title` "Hồ sơ nhân viên" (*new*, the words of SCR-STF-03) with the
  `FieldDescription` `profile.description` "Thông tin của nhân viên tại cửa hàng, dùng để liên hệ trong công việc."
  (*new*) and `account.title` "Tài khoản" (*new*) with `account.description` "Email đăng nhập và vai trò quyết định
  nhân viên mở được những trang nào." (*new*); the labels `staff.list.columns.name`, `.employeeCode`, `.joinedDate`,
  `staff.account.workPhone`, `email` "Email công ty" (*new*), `staff.list.columns.roles`, and `optional` "(không bắt
  buộc)" (*new*); the hints (`FieldDescription`, all *new*) `nameHint` "Viết đủ dấu. Tối đa 100 ký tự.",
  `employeeCodeHint` "Mã do cửa hàng cấp, không trùng với nhân viên khác. Tối đa 50 ký tự, không có khoảng trắng ở
  đầu hoặc cuối.", `joinedDateHint` "Mặc định là hôm nay; chọn ngày khác nếu nhân viên đã vào làm từ trước hoặc sẽ
  vào làm sau.", `workPhoneHint` "Từ 8 đến 15 chữ số, có thể bắt đầu bằng dấu +.", `emailHint` "Nhân viên đăng nhập
  bằng email này. Tối đa 255 ký tự." and `rolesHint` "Chọn ít nhất một vai trò. Danh sách chỉ có các vai trò bạn được
  phép gán."; the placeholders (*new*) `workPhonePlaceholder` "0901234567" and `emailPlaceholder` "an@example.com" (none
  in Họ tên, Mã nhân viên, which has no fixed pattern, or the date picker); the checks of the web (*new*)
  `validation.nameRequired` "Vui lòng nhập họ tên.", `.nameTooLong` "Họ tên tối đa 100 ký tự.",
  `.employeeCodeRequired` "Vui lòng nhập mã nhân viên.", `.employeeCodeTooLong` "Mã nhân viên tối đa 50 ký tự.",
  `.employeeCodeSpaces` "Mã nhân viên không có khoảng trắng ở đầu hoặc cuối.", `.workPhoneInvalid` "SĐT công việc
  gồm 8 đến 15 chữ số, có thể bắt đầu bằng dấu +.", `.emailRequired` "Vui lòng nhập email công ty.", `.emailInvalid`
  "Email không đúng định dạng.", `.emailTooLong` "Email tối đa 255 ký tự.", `.rolesRequired` "Vui lòng chọn ít nhất
  một vai trò." and `.rolesTooMany` "Chọn tối đa 20 vai trò."; the Empty of no role `noRoles.title` "Bạn chưa được phép
  gán vai trò nào." with `noRoles.description` "Vai trò của bạn chưa được giao quản lý vai trò nào, nên chưa tạo được
  tài khoản. Liên hệ quản trị viên để được cấp." (*new*); the Empty of a failed load, titled with the web's text of the
  answer (the API problem's `detail` when it has one, then without a description) and otherwise `rolesError.description`
  "Chưa tải được danh sách vai trò, nên chưa tạo được tài khoản. Những gì bạn đã nhập vẫn được giữ." (*new*), with
  `admin.retry`; the buttons `staff.account.cancel` "Hủy" and `submit` "Tạo tài khoản" (*new*); the toasts `created`
  "Đã tạo tài khoản. Nhân viên đăng nhập bằng mật khẩu mặc định và phải đổi mật khẩu ngay lần đầu." (*new*, the text
  of step 2 above) and `createdPending` "Đã tạo tài khoản, đang chờ duyệt. Nhân viên đăng nhập được sau khi một người
  có quyền duyệt tài khoản này." (*new*, STF-09); the notice (STF-09) `pendingNotice.title` "Tài khoản bạn tạo sẽ chờ
  duyệt" and `pendingNotice.description` "Bạn không có quyền duyệt tài khoản, nên tài khoản mới ở trạng thái Chờ
  duyệt. Nhân viên chưa đăng nhập được cho tới khi một người khác có quyền duyệt tài khoản này." (*new*).
- **Component tree** (DEC-118 (3)), under `web/src/features/identity/staff-accounts/components/`:

  ```text
  staff-create-layout.tsx                 the Card as the `form` (noValidate), `max-w-2xl mx-auto`; slots: header |
  │                                       notice | form alert | profile | account | actions; narrow: the page's 16px
  │                                       padding, the roles in one column, the actions stacked
  ├── staff-create-header-section.tsx     CardHeader: CardTitle `staff.list.add` as the page's h1;
  │                                       CardDescription `staff.create.description`
  ├── staff-create-pending-notice.tsx     (STF-09) Alert default with ClockIcon: AlertTitle `pendingNotice.title`,
  │                                       AlertDescription `pendingNotice.description`; only without `staff.approve`
  ├── staff-create-form-alert.tsx         Alert destructive with the `detail` of each `errors` item no field shows
  │                                       (`role="alert"`); hidden when there is none
  ├── staff-create-profile-section.tsx    FieldSet: FieldLegend `profile.title`, FieldDescription
  │                                       `profile.description`; FieldGroup of: Họ tên (Input, FieldDescription
  │                                       `nameHint`), Mã nhân viên (Input, `spellCheck=false`, FieldDescription
  │                                       `employeeCodeHint`), Ngày vào làm, SĐT công việc (Input `type="tel"`, label
  │                                       with `optional`, placeholder `workPhonePlaceholder`, FieldDescription
  │                                       `workPhoneHint`); each FieldError under its input, before the hint
  │   └── staff-joined-date-field.tsx     presentational — Popover with Calendar (date-fns, locale `vi`, Monday first,
  │                                       today marked) behind a Button `outline` w-60 (full width narrow) showing
  │                                       `dd/MM/yyyy` beside CalendarIcon; `required`; FieldDescription
  │                                       `joinedDateHint`; the pattern of `staff-left-date-field.tsx`
  ├── staff-create-account-section.tsx    FieldSeparator, then FieldSet: FieldLegend `account.title`, FieldDescription
  │                                       `account.description`; FieldGroup of: Email công ty (Input `type="email"`,
  │                                       `spellCheck=false`, placeholder `emailPlaceholder`, FieldDescription
  │                                       `emailHint`), Vai trò
  │   └── staff-create-roles-field.tsx    presentational — FieldSet with FieldLegend `staff.list.columns.roles`
  │                                       (variant label), FieldDescription `rolesHint`, FieldError, and a grid of
  │                                       Field orientation horizontal with Checkbox and FieldLabel per role
  │                                       (`grid-cols-1 sm:grid-cols-2`); loading: six rows of Skeleton
  │                                       (`aria-busy`); empty: Empty outline with ShieldXIcon, EmptyTitle
  │                                       `noRoles.title`, EmptyDescription `noRoles.description`; error: Empty outline
  │                                       with CircleAlertIcon, EmptyTitle the answer's text, EmptyDescription
  │                                       `rolesError.description` (left out when the title is an API problem's
  │                                       `detail`), EmptyContent Button `outline` `admin.retry`
  └── staff-create-actions-section.tsx    CardFooter with border-t, `justify-end`: Button `outline` asChild Link
                                          `staff.account.cancel` to SCR-STF-01; Button `default` type submit
                                          `submit`, disabled while the roles are not ready and while sending
                                          (LoaderCircleIcon `motion-safe:animate-spin`); narrow: `flex-col-reverse`,
                                          full width
  ../hooks/use-assignable-roles.ts        the generated query of `GET /api/v1/admin/staff/assignable-roles`, every
                                          page (`pageSize` 100)
  ../hooks/use-staff-create-form.ts       the form with `staffCreateSchema` of `../schemas.ts`; the mutation of
                                          `POST /api/v1/admin/staff`; puts each `errors` item under the field its
                                          `pointer` names (`#/roleIds` and `#/roleIds/<n>` under Vai trò), the rest in
                                          the form alert; on 201 invalidates the staff list and the count waiting,
                                          then routes to SCR-STF-03 with the toast of the answer's `status`
  ```
