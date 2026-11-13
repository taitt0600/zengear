# SCR-LOC-01 Business location

The Admin edits the store's address, hotline and opening hours (UC-070, R-LOC-01). Phase 1 has one location: the
screen opens it directly; there is no list, no add and no delete.

Loaded from `GET /api/v1/admin/business-locations` (the one item's `id`), then
`GET /api/v1/admin/business-locations/{id}` (keep its `ETag`).

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

"Lưu" sends `PUT /api/v1/admin/business-locations/{id}` with every field and `If-Match`; 200 shows "Đã lưu." The note under
the title says "Website, phiếu in và email dùng thông tin này."

| Answer | Shown |
|---|---|
| 400 with an item on `#/<field>` | Under the field or the row of hours (`#/regularHours/1`, `#/specialHours`; a province no longer in use is under Tỉnh/Thành phố, a ward outside the province under Phường/Xã) |
| 412 | Reload, as in [*Conventions*](conventions.md) |
