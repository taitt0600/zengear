# SCR-AUT-02 Required password change

Shown alone, without the menu, while `mustChangePassword` is true (R-USR-03): the staff member replaces the default
password before anything else.

| Field | Rule (web) |
|---|---|
| New password ("Mật khẩu mới") | required, 12 to 128 characters (counted as Unicode code points); spaces count and are kept |
| Repeat ("Nhập lại mật khẩu mới") | the same as the new password (checked by the web only, never sent) |

Help text under the field: "Tối thiểu 12 ký tự. Không dùng mật khẩu mặc định, mật khẩu phổ biến hay chữ zengear."
The web does not check the blocklist or the default password: the API does (R-USR-11).

1. "Đổi mật khẩu" sends `POST /api/v1/admin/me/change-password` `{ newPassword }`.
2. 204: every session of the account is revoked (DEC-094). The web signs out and shows SCR-AUT-01 with "Đã đổi mật
   khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới." The password is never kept after the request.

| Answer | Shown |
|---|---|
| 400 with an item on `#/newPassword` | Under the field, below the help text that names the policies (the default password, the blocklist or zengear; the web has checked the length) |
| 403 `/problems/recent-login-required` | A dialog "Vui lòng nhập lại mật khẩu hiện tại để tiếp tục": re-authenticates with Firebase, then sends the change again once |
| 409 `/problems/staff-account-required` | Toast |

"Đăng xuất" is the only other action on this screen.

Texts of the page that the table does not quote: "Vui lòng nhập mật khẩu mới.", "Mật khẩu mới dài từ 12 đến 128 ký
tự." and "Mật khẩu nhập lại không khớp." (checked by the web); the dialog of 403 `/problems/recent-login-required`
has the description "Lần đăng nhập gần nhất đã quá 5 phút, nên cần xác nhận lại trước khi đổi mật khẩu.", the field
"Mật khẩu hiện tại" ("Vui lòng nhập mật khẩu hiện tại." when empty), "Tiếp tục" and "Hủy"; Firebase's answers there
are "Mật khẩu hiện tại không đúng." for a wrong password and the texts of SCR-AUT-01 for the others; a second 403
after signing in again shows its `detail`; a network failure "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và
thử lại." The sign-in page shows the message of step 2 as a notice, not as an error, until the next sign-in
attempt.
