# Tech Stack

| Container | Technology | Version | Purpose | Phase |
|---|---|---|---|---|
| Web App | Next.js | — | Framework cho storefront, trang quản trị và POS | Phase 1 |
| Web App | TypeScript | — | Ngôn ngữ | Phase 1 |
| Web App | Tailwind CSS | — | Styling | Phase 1 |
| Web App | shadcn/ui | — | UI components | Phase 1 |
| Web App | Zustand | — | Client state | Phase 1 |
| Web App | TanStack Query | — | Server state | Phase 1 |
| Web App | React Hook Form + Zod | — | Form và validation | Phase 1 |
| Web App | openapi-typescript + openapi-fetch | — | Sinh kiểu TypeScript từ api/openapi/ZenGear.Api.json và gọi API có kiểu; dùng với TanStack Query (DEC-093) | Phase 1 |
| Hosting | Vercel | — | Chạy web app Next.js (storefront, trang quản trị, POS) | Phase 1 |
| Hosting | Azure | — | Chạy backend API .NET và PostgreSQL | Phase 1 |
| Backend API | .NET (ASP.NET Core) | 10 | Backend framework; 3 project ZenGear.Api, ZenGear.Services, ZenGear.Data (DEC-091) | Phase 1 |
| Backend API | Entity Framework Core + Npgsql | 10 | ORM và EF Core Migrations — cơ chế duy nhất áp dụng schema, chạy bằng migration bundle ở bước deploy; xmin làm concurrency token | Phase 1 |
| Backend API | EFCore.NamingConventions | — | Tên bảng, cột snake_case khớp đặc tả database | Phase 1 |
| Backend API | FluentValidation | — | Validation | Phase 1 |
| Backend API | AutoMapper | — | Object mapping; Community License miễn phí khi doanh thu năm dưới 5 triệu USD (Lucky Penny Software) | Phase 1 |
| Backend API | Firebase Admin SDK + JWT Bearer | — | Xác thực token Firebase | Phase 1 |
| Backend API | Microsoft.AspNetCore.OpenApi + Scalar | — | Sinh tài liệu OpenAPI lúc build vào api/openapi (hợp đồng cho web, DEC-093); Scalar hiển thị tài liệu API | Phase 1 |
| Backend API | Serilog | — | Log có cấu trúc, gồm log bảo mật cho sự kiện đăng nhập (R-SEC-11); không ghi token, mật khẩu, mã OTP | Phase 1 |
| Backend API | Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | — | Health check kết nối database cho endpoint /health | Phase 1 |
| Background Jobs | BackgroundService + PeriodicTimer | .NET 10 | Job nền của Scheduler (A10): hủy đơn quá hạn, gửi thông báo, phát hành hóa đơn, đối soát hằng đêm | Phase 1 |
| Cache | Redis + StackExchange.Redis | — | Cache trạng thái user và quyền theo Role với key có phiên bản (R-SEC-07); maxmemory-policy volatile-* hoặc noeviction | Phase 1 |
| Database | PostgreSQL | 18 | CSDL quan hệ; cần uuidv7() native; file SQL và DBML trong DATABASE.md là đặc tả để CI đối chiếu với kết quả migration | Phase 1 |
| Database | btree_gist | — | Extension: exclusion constraint cho tax_rates | Phase 1 |
| Database | unaccent + pg_trgm | — | Extension: tìm kiếm không dấu (PDS-03) | Phase 2 |
| File Storage | Firebase Storage | — | Ảnh sản phẩm, ảnh chứng từ và bằng chứng (attachments) | Phase 1 |
| External Service | Firebase Authentication | — | Đăng nhập nhân viên; khách đăng nhập email, Google, SĐT + OTP ở Phase 2 cần gói Blaze (A13) | Phase 1 |
| External Service | Resend | — | Gửi email giao dịch (A14) | Phase 1 |
| External Service | MISA meInvoice | — | Phát hành hóa đơn bán hàng điện tử qua API tích hợp dành cho developer (A15) | Phase 1 |
| External Service | VNPay | — | Cổng thanh toán, dùng sandbox cho demo (A12) | Phase 2 |
| Testing | NUnit | — | Test framework cho unit test và integration test | Phase 1 |
| Testing | Shouldly | — | Assertion | Phase 1 |
| Testing | Moq | — | Mock dependency trong unit test | Phase 1 |
| Testing | Testcontainers for .NET | image postgres:18, redis | PostgreSQL và Redis thật cho integration, concurrency, DB constraint và cache test | Phase 1 |
| Testing | Respawn | — | Reset dữ liệu database giữa các integration test | Phase 1 |
| Testing | Microsoft.Extensions.TimeProvider.Testing | — | FakeTimeProvider: điều khiển thời gian trong test của clock service (R-TZ-01) | Phase 1 |
| Testing | zengear_07_schema_check.sql | — | Kiểm tra đủ đối tượng không có trên ERD sau mỗi migration | Phase 1 |
| Dev Tooling | Visual Studio / VS Code | — | IDE | Phase 1 |
| Dev Tooling | pgAdmin | — | PostgreSQL GUI | Phase 1 |
| Dev Tooling | Postman | v12 | Collection v3 lưu bằng Git gốc trong postman/, mỗi tính năng một folder; chủ dự án test trước khi merge; Postman CLI 1.69.0 chạy collection trong CI và trên API dev, không cần đăng nhập; collection đăng nhập bằng custom token của Firebase Auth Emulator (DEC-100, DEC-101) | Phase 1 |
| Dev Tooling | Firebase Local Emulator Suite | — | Chạy Firebase local | Phase 1 |
| Dev Tooling | Docker | image postgres:18 | Container local, khớp version Azure Database for PostgreSQL | Phase 1 |
| Dev Tooling | Claude Code | — | Plugin từ marketplace claude-plugins-official (csharp-lsp, pr-review-toolkit, code-review, feature-dev, commit-commands, security-guidance) và MCP qua Docker MCP Toolkit (PostgreSQL, Postman) chỉ với dữ liệu dev (DEC-100) | Phase 1 |
| Source & Docs | Git + GitHub | — | Quản lý mã nguồn; một repository cho api, web và docs (DEC-093) | Phase 1 |
| Source & Docs | GitHub Actions | — | Một pipeline (pipeline.yml), tên job trùng id: changes chọn job theo path; secrets, tools-test, api-test, schema, acceptance (build image và migration bundle một lần, chạy toàn bộ collection Postman trên image đó, trên main đẩy image lên GHCR), web-test và web-preview khi có web, ci-ok; gates.yml: pr-gates, pr-title; required checks ci-ok, pr-gates, pr-title; push main deploy staging rồi production (DEC-093, DEC-100, DEC-101) | Phase 1 |
| Source & Docs | GitHub Container Registry | — | Lưu image API build một lần trong pipeline để deploy staging và production (DEC-100) | Phase 1 |
| Source & Docs | dbdocs.io | — | Tài liệu ERD sinh từ khối DBML trong DATABASE.md | Phase 1 |
