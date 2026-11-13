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
