# Tech Stack

| Container | Technology | Version | Purpose | Phase |
|---|---|---|---|---|
| Web App | Next.js | 16.4.3 | Framework cho storefront, trang quản trị và POS | Phase 1 |
| Web App | React | 19.3.1 | Thư viện giao diện của Next.js (DEC-104) | Phase 1 |
| Web App | TypeScript | 6.0.3 | Ngôn ngữ; TypeScript 7 khi typescript-eslint hỗ trợ (DEC-122) | Phase 1 |
| Web App | Tailwind CSS | 4.3.3 | Styling | Phase 1 |
| Web App | shadcn/ui | — | UI components theo mô hình open code: components/ui là code của dự án, sửa trực tiếp nhưng giữ thiết kế gốc, chữ qua file chuỗi, cập nhật bằng add --diff; block chính thức làm khuôn; calendar dùng date-fns (DEC-116) | Phase 1 |
| Web App | Zustand | — | Client state | Phase 1 |
| Web App | TanStack Query | — | Server state | Phase 1 |
| Web App | React Hook Form + Zod | — | Form và validation | Phase 1 |
| Web App | Orval | 8.46.0 | Sinh từ api/openapi/ZenGear.Api.json kiểu, client fetch và hook TanStack Query vào web/src/lib/api/generated/; mutator trong lib/api thêm Firebase ID token (trừ /api/v1/shop/) và đổi Problem Details thành ApiError (DEC-093, DEC-117, DEC-122) | Phase 1 |
| Web App | Firebase JS SDK | — | Đăng nhập Firebase Authentication phía trình duyệt; nối emulator khi chạy dev (DEC-104) | Phase 1 |
| Web App | next-intl | — | i18n: mọi chuỗi giao diện qua file chuỗi, Phase 1 chỉ tiếng Việt, tiếng Anh sau này cho storefront (DEC-104) | Phase 1 |
| Web App | lucide-react | — | Icon, bộ mặc định của shadcn/ui (DEC-104) | Phase 1 |
| Web App | motion | — | Animation; tôn trọng prefers-reduced-motion (DEC-104) | Phase 1 |
| Web App | TanStack Table | — | Bảng danh sách của trang quản trị (DEC-104) | Phase 1 |
| Web App | ESLint + Prettier | ESLint 10 | Lint và định dạng code web, eslint-config-next 16.4.3; tsc --noEmit kiểm kiểu (DEC-104, DEC-122) | Phase 1 |
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
| Testing | NUnit | 5 | Test framework cho unit test và integration test; NUnit3TestAdapter 6, Microsoft.NET.Test.Sdk 18, coverlet 10 (DEC-122) | Phase 1 |
| Testing | Shouldly | — | Assertion | Phase 1 |
| Testing | Moq | — | Mock dependency trong unit test | Phase 1 |
| Testing | Testcontainers for .NET | image postgres:18, redis | PostgreSQL và Redis thật cho integration, concurrency, DB constraint và cache test | Phase 1 |
| Testing | Respawn | — | Reset dữ liệu database giữa các integration test | Phase 1 |
| Testing | Microsoft.Extensions.TimeProvider.Testing | — | FakeTimeProvider: điều khiển thời gian trong test của clock service (R-TZ-01) | Phase 1 |
| Testing | zengear_07_schema_check.sql | — | Kiểm tra đủ đối tượng không có trên ERD sau mỗi migration | Phase 1 |
| Testing | Vitest + Testing Library | — | Unit test của web: schema Zod, ánh xạ Problem Details vào form, hook (DEC-104) | Phase 1 |
| Testing | Playwright | — | Test luồng màn hình trên API, Firebase Auth Emulator và bản build của web; ảnh chụp cho hướng dẫn test trước merge (DEC-104) | Phase 1 |
| Dev Tooling | Visual Studio / VS Code | — | IDE | Phase 1 |
| Dev Tooling | pgAdmin | — | PostgreSQL GUI | Phase 1 |
| Dev Tooling | Postman | v12 | Collection v3 lưu bằng Git gốc trong postman/, mỗi tính năng một folder; chủ dự án test trước khi merge; Postman CLI 1.69.0 chạy collection trong CI và trên API dev, không cần đăng nhập; collection đăng nhập bằng custom token của Firebase Auth Emulator (DEC-100, DEC-101) | Phase 1 |
| Dev Tooling | Firebase Local Emulator Suite | — | Chạy Firebase local | Phase 1 |
| Dev Tooling | Docker | image postgres:18 | Container local, khớp version Azure Database for PostgreSQL | Phase 1 |
| Dev Tooling | Claude Code | — | Plugin từ marketplace claude-plugins-official (csharp-lsp, feature-dev, typescript-lsp, frontend-design; security-guidance dùng thử) và MCP qua Docker MCP Toolkit (PostgreSQL, Postman) chỉ với dữ liệu dev (DEC-100, DEC-111, DEC-112) | Phase 1 |
| Dev Tooling | Node.js + npm | 26.13.0 (npm 11.20.0) | Runtime và package manager của web; package-lock.json commit, mọi bước setup-node của CI đọc web/.nvmrc, chạy npm ci; Dependabot cooldown 7 ngày (DEC-104, DEC-122) | Phase 1 |
| Dev Tooling | Claude Design | — | Bản vẽ màn hình chủ dự án duyệt trước khi code; design system dựng từ codebase (DEC-119) | Phase 1 |
| Source & Docs | Git + GitHub | — | Quản lý mã nguồn; một repository cho api, web và docs (DEC-093) | Phase 1 |
| Source & Docs | GitHub Actions | — | Một pipeline (pipeline.yml), tên job trùng id: changes chọn job theo path; secrets, tools-test, api-test, schema, web-test (kiểm key của file chuỗi, rồi npm ci, gen:api so với web/src/lib/api/generated/, ESLint, Prettier, tsc, Vitest, build), acceptance (build image và migration bundle một lần, chạy toàn bộ collection Postman trên image đó, trên main đẩy image lên GHCR, rồi chạy các luồng Playwright của web), ci-ok; gates.yml: pr-gates, pr-title; required checks ci-ok, pr-gates, pr-title; push main deploy staging rồi production (DEC-093, DEC-100, DEC-101, DEC-104, DEC-122) | Phase 1 |
| Source & Docs | Dependabot | — | Cập nhật phiên bản hằng tuần cho npm (web/), NuGet (api/), GitHub Actions, Docker và Docker Compose; patch và minor gộp theo ecosystem, mỗi major một PR, ignore các major gắn với .NET 10 và PostgreSQL 18, và TypeScript từ 6.1; pr_gates chỉ nhận PR của dependabot[bot] đổi dòng phiên bản; chủ dự án merge từng PR (DEC-104, DEC-122) | Phase 1 |
| Source & Docs | GitHub Container Registry | — | Lưu image API build một lần trong pipeline để deploy staging và production (DEC-100) | Phase 1 |
| Source & Docs | dbdocs.io | — | Tài liệu ERD sinh từ khối DBML trong DATABASE.md | Phase 1 |
