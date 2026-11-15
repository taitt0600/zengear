# Glossary

Business terms (Vietnamese) and the names used in code and database. Use these names for classes, properties, endpoints and UI labels; add a row here before introducing a new term.

## Entities

Class names are singular PascalCase in `ZenGear.Data/Entities/<Area>`; table names are in `docs/DATABASE.md`.

| Thuật ngữ | Entity | Table | Area |
|---|---|---|---|
| Tài khoản (nhân viên, khách hàng) | `User` | `users` | Identity |
| Hồ sơ khách hàng | `CustomerProfile` | `customer_profiles` | Identity |
| Hồ sơ nhân viên | `StaffProfile` | `staff_profiles` | Identity |
| Vai trò | `Role` | `roles` | Identity |
| Quyền | `Permission` | `permissions` | Identity |
| Gán vai trò (một tài khoản giữ nhiều vai trò) | `UserRole` | `user_roles` | Identity |
| Gán quyền cho vai trò | `RolePermission` | `role_permissions` | Identity |
| Phạm vi quản lý (các vai trò mà người giữ vai trò được quản lý tài khoản) | `RoleManageableRole` | `role_manageable_roles` | Identity |
| Phòng ban | `Department` | `departments` | Identity |
| Vị trí (chức danh công việc) | `Position` | `positions` | Identity |
| Phân công (nhân viên vào phòng ban, vị trí, địa điểm làm việc) | `StaffAssignment` | `staff_assignments` | Identity |
| Sổ địa chỉ giao hàng | `Address` | `addresses` | Customers |
| Danh sách yêu thích | `Wishlist` | `wishlists` | Customers |
| Quốc gia | `Country` | `countries` | Geography |
| Tỉnh/thành phố | `Province` | `provinces` | Geography |
| Xã/phường/đặc khu | `Ward` | `wards` | Geography |
| Danh mục sản phẩm | `Category` | `categories` | Catalog |
| Thương hiệu (hãng) | `Brand` | `brands` | Catalog |
| Dòng sản phẩm | `ProductSeries` | `product_series` | Catalog |
| Sản phẩm | `Product` | `products` | Catalog |
| Biến thể bán (SKU), SKU dịch vụ | `ProductSku` | `product_skus` | Catalog |
| Ảnh sản phẩm | `ProductImage` | `product_images` | Catalog |
| Linh kiện niêm yết của PC bộ | `PcBuildComponent` | `pc_build_components` | Catalog |
| Nhãn sản phẩm (badge) | `Badge` | `product_badges` | Catalog |
| Khung ưu đãi (tag) | `ProductTag` | `product_tags` | Catalog |
| Chuyển hướng đường dẫn (301) | `UrlRedirect` | `url_redirects` | Catalog |
| Nhóm hàng hóa, dịch vụ tính thuế | `TaxClass` | `tax_classes` | Tax |
| Thuế suất GTGT | `TaxRate` | `tax_rates` | Tax |
| Địa điểm kinh doanh | `BusinessLocation` | `business_locations` | Locations |
| Kho (khu vực tồn kho) | `Warehouse` | `warehouses` | Inventory |
| Tồn kho | `StockBalance` | `stock_balances` | Inventory |
| Sổ kho, thẻ kho (dòng biến động) | `StockLedgerEntry` | `stock_ledger_entries` | Inventory |
| Máy có serial (từng máy vật lý) | `SerialUnit` | `serial_units` | Inventory |
| Phiếu lắp ráp PC | `PcAssembly` | `pc_assemblies` | Inventory |
| Linh kiện của phiếu lắp ráp | `PcAssemblyComponent` | `pc_assembly_components` | Inventory |
| Phiếu điều chỉnh tồn kho | `StockAdjustment` | `stock_adjustments` | Inventory |
| Lý do điều chỉnh | `AdjustmentReason` | `adjustment_reasons` | Inventory |
| Phiếu kiểm kho | `StockCount` | `stock_counts` | Inventory |
| Dòng kiểm kho | `StockCountLine` | `stock_count_lines` | Inventory |
| Nhà cung cấp (NCC) | `Supplier` | `suppliers` | Purchasing |
| Đơn đặt hàng NCC (PO) | `PurchaseOrder` | `purchase_orders` | Purchasing |
| Dòng đơn đặt hàng NCC | `PurchaseOrderItem` | `purchase_order_items` | Purchasing |
| Phiếu nhập kho (nhận hàng) | `PurchaseReceipt` | `purchase_receipts` | Purchasing |
| Phiếu trả hàng nhập | `PurchaseReturn` | `purchase_returns` | Purchasing |
| Dòng trả hàng nhập | `PurchaseReturnItem` | `purchase_return_items` | Purchasing |
| Thanh toán NCC (công nợ phải trả) | `SupplierPayment` | `supplier_payments` | Purchasing |
| Đơn hàng | `Order` | `orders` | Sales |
| Dòng đơn hàng | `OrderItem` | `order_items` | Sales |
| Máy đã bán trong dòng đơn | `OrderItemSerial` | `order_item_serials` | Sales |
| Cấu hình linh kiện của dòng PC bộ | `OrderItemComponent` | `order_item_components` | Sales |
| Thông tin xuất hóa đơn của người mua | `OrderInvoiceDetail` | `order_invoice_details` | Sales |
| Giỏ hàng | `CartItem` | `cart_items` | Sales |
| Máy đổi trả trong giỏ | `CartSerialUnit` | `cart_serial_units` | Sales |
| Bộ đếm số chứng từ theo ngày | `DailySequence` | `daily_sequences` | Sales |
| Hãng vận chuyển | `Carrier` | `carriers` | Shipping |
| Phương thức giao hàng | `ShippingMethod` | `shipping_methods` | Shipping |
| Bảng phí giao hàng | `ShippingFeeRule` | `shipping_fee_rules` | Shipping |
| Thông tin giao hàng của đơn | `OrderShippingDetail` | `order_shipping_details` | Shipping |
| Bảng đối soát COD | `CarrierSettlement` | `carrier_settlements` | Shipping |
| Dòng đối soát COD | `CarrierSettlementLine` | `carrier_settlement_lines` | Shipping |
| Khoản thanh toán / hoàn tiền của đơn | `OrderPayment` | `order_payments` | Payments |
| Hóa đơn điện tử | `EInvoice` | `e_invoices` | Invoicing |
| Ca làm việc (ca thu ngân) | `Shift` | `shifts` | Pos |
| Khoản thu chi tiền mặt trong ca | `ShiftCashAdjustment` | `shift_cash_adjustments` | Pos |
| Quỹ (tiền mặt, tài khoản ngân hàng, ví) | `CashAccount` | `cash_accounts` | Accounting |
| Loại thu, loại chi | `CashVoucherCategory` | `cash_voucher_categories` | Accounting |
| Phiếu thu / phiếu chi (sổ quỹ) | `CashVoucher` | `cash_vouchers` | Accounting |
| Phiếu đổi trả | `Return` | `returns` | AfterSales |
| Dòng đổi trả | `ReturnItem` | `return_items` | AfterSales |
| Phiếu bảo hành | `WarrantyRequest` | `warranty_requests` | AfterSales |
| Voucher (mã giảm giá toàn đơn) | `Voucher` | `vouchers` | Promotions |
| Mã voucher dùng 1 lần | `VoucherCode` | `voucher_codes` | Promotions |
| Phạm vi áp dụng voucher | `VoucherTarget` | `voucher_targets` | Promotions |
| Lượt dùng voucher | `VoucherUsage` | `voucher_usages` | Promotions |
| Thông báo (hàng đợi gửi) | `Notification` | `notifications` | Notifications |
| Lần import Excel | `ImportJob` | `import_jobs` | Imports |
| Lịch sử trạng thái | `StatusHistory` | `status_history` | Audit |
| Ảnh/video bằng chứng | `Attachment` | `attachments` | Audit |
| Nhật ký thay đổi (audit) | `AuditLog` | `audit_logs` | Audit |

## Value sets

Stored as the English values below; the Vietnamese label is what the UI shows, through a key of `web/messages/vi.json` (next-intl, DEC-104), never a string written in a component. When the storefront gets English, this file gains an English label column and the web an `en.json`.

| Thuật ngữ | Column | Values |
|---|---|---|
| Trạng thái tài khoản | `users.status` | ACTIVE (đang hoạt động), DISABLED (đã khóa), ANONYMIZED (đã ẩn danh) |
| Loại đơn | `orders.type` | SELF_SERVICE (khách tự đặt online), ASSISTED (nhân viên tạo hộ / bán tại quầy) |
| Cách nhận hàng | `orders.fulfillment_method` | PICKUP (nhận tại cửa hàng), SHIP (giao hàng) |
| Trạng thái đơn | `orders.status` | Pending (chờ xác nhận), Confirmed (đã xác nhận, đã trừ kho), Processing (đang xử lý), Shipped (đã giao cho hãng vận chuyển), Ready_for_pickup (chờ khách đến lấy), Completed (hoàn tất), Cancelled (đã hủy), Returned_to_sender (giao thất bại, hoàn về) |
| Hình thức thanh toán | `order_payments.method` | CASH (tiền mặt), COD (thu hộ khi giao), BANK_TRANSFER (chuyển khoản), CARD_POS (quẹt thẻ), VNPAY, INSTALLMENT_FINANCE (trả góp qua công ty tài chính) |
| Trạng thái khoản thanh toán | `order_payments.status` | Pending (chờ), Collected (hãng đã thu COD, chưa trả cửa hàng), Paid (đã nhận tiền), Failed (thất bại) |
| Loại kho | `warehouses.type` | MAIN (kho tổng, hàng bán được), WARRANTY (kho bảo hành, hàng lỗi), DISPLAY (hàng trưng bày, không bán), OUTLET (máy đổi trả, bán từng máy) |
| Loại biến động kho | `stock_ledger_entries.movement_type` | OPENING (tồn đầu kỳ), PURCHASE_RECEIPT (nhập hàng NCC), ORDER_CONFIRM (xuất bán), ORDER_CANCEL (hoàn kho do hủy đơn), ADJUSTMENT (điều chỉnh/kiểm kê), RETURN_RESTOCK (nhập lại hàng đổi trả), WARRANTY_OUT / WARRANTY_IN (xuất/nhập bảo hành), SUPPLIER_RETURN (trả hàng NCC), DELIVERY_RETURN (hàng giao thất bại hoàn về), TRANSFER_OUT / TRANSFER_IN (chuyển kho nội bộ), ASSEMBLY_OUT / ASSEMBLY_IN (xuất linh kiện / nhập PC lắp ráp) |
| Trạng thái máy | `serial_units.status` | IN_STOCK (trong kho), RESERVED (đã giữ cho đơn), SOLD (đã bán), ASSEMBLED (đã lắp vào PC bộ), IN_WARRANTY (đang bảo hành), DEFECTIVE (lỗi), RETURNED_TO_SUPPLIER (đã trả NCC), LOST (mất) |
| Tình trạng máy | `serial_units.condition` | NEW (mới), OPEN_BOX (đã khui hộp), DISPLAY (máy trưng bày) |
| Cách xử lý đổi trả | `returns.resolution_type` | REFUND (hoàn tiền), EXCHANGE (đổi hàng) |
| Loại yêu cầu bảo hành | `warranty_requests.request_type` | REPAIR (bảo hành sửa chữa), DOA (1 đổi 1 do lỗi nhà sản xuất trong thời hạn đầu) |
| Kết quả bảo hành | `warranty_requests.resolution_type` | REPAIRED (đã sửa), REPLACED (đổi máy), NO_FAULT (không lỗi), BUYBACK (thu hồi có khấu hao), REJECTED (từ chối) |
| Mục đích thu chi trong ca | `shift_cash_adjustments.purpose` | EXPENSE (chi phí), OTHER_INCOME (thu khác), BANK_DEPOSIT (nộp tiền vào ngân hàng), TO_SAFE / FROM_SAFE (chuyển giữa ngăn kéo và két) |
| Chiều phiếu quỹ | `cash_vouchers.direction` | RECEIPT (phiếu thu), PAYMENT (phiếu chi) |
| Phạm vi truy cập của vai trò | `roles.scoping_type` | NONE (toàn bộ dữ liệu), SELF (chỉ dữ liệu của chính mình) |
| Trạng thái sản phẩm | `products.status` | DRAFT (nháp), PUBLISHED (đang bán), DISCONTINUED (ngừng kinh doanh) |

## Business terms

| Thuật ngữ | English | Meaning |
|---|---|---|
| Công ty TNHH một thành viên | single-member limited company | Hình thức pháp lý của ZenGear; thuế GTGT theo phương pháp khấu trừ, sổ kế toán kép trên phần mềm kế toán ngoài (DEC-143) |
| Hộ kinh doanh | household business | Một loại người mua trên hóa đơn (`HOUSEHOLD_BUSINESS`: hộ kinh doanh hoặc cá nhân kinh doanh), định danh bằng số định danh cá nhân 12 số (R-ORD-09); không còn là hình thức của cửa hàng (DEC-143) |
| Hóa đơn giá trị gia tăng | VAT invoice | Loại hóa đơn của công ty khấu trừ thuế (loại 1), khởi tạo từ máy tính tiền khi bán lẻ, ghi giá chưa thuế, thuế suất và tiền thuế theo từng thuế suất (R-EIV-01) |
| Thuế GTGT đầu vào | input VAT | Thuế GTGT ghi trên hóa đơn của NCC (`purchase_order_items.vat_rate`); không vào giá vốn (R-COST-01), khấu trừ khi đủ điều kiện (R-ACT-04), phần không được khấu trừ kế toán xử lý trên phần mềm kế toán (DEC-143) |
| Phương pháp khấu trừ | credit method | Cách tính thuế GTGT của công ty: thuế đầu ra trừ thuế đầu vào được khấu trừ (Luật Thuế GTGT 48/2024/QH15, DEC-143) |
| Giá chưa thuế | amount before VAT | Phần tổng thanh toán của một thuế suất sau khi tách tiền thuế GTGT, ghi trên hóa đơn GTGT (R-ORD-07) |
| Tiền thuế GTGT | VAT amount (`vat_amount`) | round(tổng thanh toán × thuế suất / (100 + thuế suất)) theo từng thuế suất của đơn (R-ORD-07); trên đơn mua là thuế đầu vào |
| Xuất dữ liệu cho phần mềm kế toán | accounting software export | Tệp CSV theo kỳ của hóa đơn, mua hàng, nhập xuất tồn, phiếu quỹ và công nợ NCC để kế toán ghi sổ kép (ACT-02, R-ACT-03) |
| Giá vốn bình quân | average cost (`average_cost`) | Bình quân gia quyền di động theo SKU, chỉ hiện với quyền `cost.view` |
| Doanh thu thuần | net revenue | Doanh thu − giá trị hàng bị trả (R-RPT-01) |
| Công nợ phải trả NCC | supplier payable | Tính từ số dư đầu kỳ, phiếu nhập, phiếu trả và thanh toán NCC (R-PO-04) |
| Sổ quỹ | cash book | Tập hợp phiếu thu, phiếu chi theo từng quỹ |
| Đối soát | reconciliation | So số liệu lưu sẵn với nguồn gốc; job đêm chỉ báo cáo, không tự sửa |
| Đơn tạo hộ / bán tại quầy | assisted order / counter sale | Đơn do nhân viên tạo (`ASSISTED`) |
| Vai trò hệ thống | system role | Admin và Customer (`is_system_role = true`): quyền do code quy định, ghi đè khi khởi động, không sửa qua API (DEC-095) |
| Vai trò mặc định | default role | 8 vai trò nhân viên có sẵn (DEC-128): code chỉ cấp quyền mới lần đầu, sau đó Admin chỉnh; không đổi tên, không xóa (DEC-095) |
| Mã nhân viên | employee code (`staff_profiles.employee_code`) | ZG + 2 số cuối của năm vào làm (`joined_date`) + 4 số thứ tự trong năm đó, ví dụ ZG260012; hệ thống sinh khi tạo tài khoản, không nhập tay, không bao giờ đổi, không tái dùng số đã cấp (R-USR-24, DEC-145); mẫu áp dụng khi R-USR-24 được dựng, đến lúc đó mã do người tạo nhập |
| Tên viết tắt | name abbreviation | Tên (từ cuối của họ tên) viết hoa chữ đầu + chữ đầu viết hoa của từng từ còn lại theo thứ tự, bỏ dấu (đ thành d) và ký tự không phải chữ, ví dụ Nguyễn Văn An thành AnNV; chỉ dùng để sinh tên đăng nhập, không lưu riêng (R-USR-24) |
| Tên đăng nhập | username (`staff_profiles.username`) | Tên viết tắt + mã nhân viên, ví dụ AnNVZG260012; duy nhất không phân biệt hoa thường; email công ty là chữ thường của nó + @ + tên miền cấu hình; chỉ đổi khi Admin sinh lại theo họ tên hiện tại (R-USR-21, R-USR-24); cột thêm khi R-USR-24 được dựng |
| Phân công chính | primary assignment (`is_primary`) | Phân công duy nhất được đánh dấu chính trong các phân công đang hiệu lực của một nhân viên; phòng ban, vị trí và quản lý trực tiếp của nhân viên là của phân công này (R-ORG-03) |
| Quản lý trực tiếp | line manager (`manager_user_id`) | Tài khoản nhân viên khác, đang hoạt động, mà nhân viên báo cáo trong một phân công; không tạo vòng báo cáo; không đổi quyền, quyền vẫn theo Role (R-ORG-03, DEC-144) |
| Kiêm nhiệm | concurrent assignment | Một nhân viên có nhiều phân công cùng hiệu lực, trong đó đúng một là phân công chính (R-ORG-03); khác với gán nhiều Role (`user_roles`) |
| Khách lẻ | walk-in customer | Tài khoản hệ thống cho khách không cho SĐT lẫn email (R-USR-08) |
| Tài khoản placeholder | placeholder account | Tài khoản khách do nhân viên tạo hộ, chưa gắn Firebase (`firebase_uid` NULL) |
| PC bộ, PC dựng sẵn | pre-built PC | SKU PC có cấu hình niêm yết; lắp sẵn (STOCK) hoặc lắp theo đơn (BUILD) |
| 1 đổi 1 | DOA exchange | Đổi máy mới khi lỗi nhà sản xuất trong `doa_exchange_days` của danh mục |
| Giờ mở cửa hằng tuần | regular hours (`regular_hours`, `RegularHoursPeriod`) | Các khung giờ mở cửa theo thứ trong tuần (1 = thứ Hai … 7 = Chủ nhật) của địa điểm kinh doanh; ngày không có khung giờ là ngày nghỉ |
| Lịch riêng ngày lễ | special hours (`special_hours`, `SpecialHoursPeriod`) | Lịch theo ngày cụ thể (đóng cửa cả ngày hoặc giờ khác) thay giờ mở cửa hằng tuần của ngày đó (R-LOC-01) |
