# Sửa dependency High trong PR CI

Nhánh: `ci/toantran/backend-pr-checks`, PR #106. Log GitHub cho thấy Build/Analyze/Regression pass; Gitleaks quét 113 commit không phát hiện secret nhưng NuGet có 3 High nên Security fail, Docker package bị skip đúng thiết kế.

**Phương án cuối người dùng đã chốt:** dùng mapper không cần key. Đã thay AutoMapper bằng Mapster MIT; phương án AutoMapper 15/license trước đó không còn áp dụng.

## Thay đổi

| Dependency | Trước | Bản dùng sau sửa | Advisory |
|---|---|---|---|
| AutoMapper / Volo.Abp.AutoMapper | 13.0.1 / 8.2.0 | Gỡ, dùng Mapster 10.0.14 MIT | https://github.com/advisories/GHSA-rvv3-g6hj-g44x |
| Microsoft.Extensions.Caching.Memory | 8.0.0 | 8.0.1 | https://github.com/advisories/GHSA-qj66-m88j-hmgj |
| System.Text.Json | 8.0.4 | 8.0.6 | https://github.com/advisories/GHSA-8g4q-xg66-9fp4 |
| System.IdentityModel.Tokens.Jwt | 8.0.2 | 8.14.0 | Giữ dòng IdentityModel 8.14 đã nâng; hồi quy login/me/refresh PASS |

`Directory.Build.props` pin hai gói Microsoft trong toàn graph BE, gồm các test project. Dockerfile copy props trước restore để build image dùng đúng graph. Không sửa DB/schema hoặc nâng ABP/.NET major.

`MapsterObjectMappingProvider` thực hiện provider ABP chung và provider context WashGo. Các service giữ cách gọi ObjectMapper, bao gồm cập nhật vào entity hiện có. 5 module Locker/Merchant/ServiceArea/WashService/WashOrder chuyển profile sang IRegister; DTO chi tiết đơn kế thừa rule chung để tránh lặp. Giữ bộ ignore cho trường enrich và hành vi xóa bằng null; không bật IgnoreNullValues toàn cục.

Config Mapster riêng yêu cầu đăng ký cặp mapping, giới hạn depth 32 và compile sớm. AutoMapper/Volo.Abp.AutoMapper phải biến mất khỏi resolved dependency graph, không chỉ bỏ using. Nguồn và giấy phép: https://github.com/MapsterMapper/Mapster (MIT).

## Không cần license key

Mapster MIT không cần tài khoản, key hay biến môi trường cho mapper. Đã gỡ wiring license AutoMapper vừa thêm khỏi Compose/.env.example, không sửa `.env` thật. Không cần đăng ký AutoMapper hoặc thêm secret mapper vào GitHub.

## Kiểm thử và trạng thái

- Build Release và kiểm tra syntax/whitespace PASS, còn 5 cảnh báo nullable có sẵn.
- Regression trực tiếp `tests/smoke/Mapping` PASS: đủ 5 module, list/nesting, kế thừa DTO, cùng instance, null clearing, bỏ qua trường enrich và từ chối cặp mapping chưa đăng ký.
- Security.ps1 sau chuyển Mapster PASS: Gitleaks 144 commit không có finding theo scanner; NuGet 0 High/Critical. Đã xác minh AutoMapper/Volo.Abp.AutoMapper không còn trong graph. Không thay đổi scanner, severity hoặc thêm ngoại lệ.
- Docker API trên DB mới PASS: ABP mapping/JWT, catalog và actual-weight đầy đủ luồng đơn, phí/ETA/quyền/replay/concurrency, thanh toán Mock và nhận đồ. Container/DB test đã dọn; không chạy suite trên database thật.
- Hai suite PDF/DOC/DOCX và email MIME PASS với graph mới.
- Chưa commit/push bản vá; run GitHub sau push mới là bằng chứng CI của bản sửa.

`mapping-compatibility.ps1` gọi Merchant list/detail, Locker (mapping boxes) và WashService qua HTTP; JWT login/me/refresh và logout. CI API còn chạy catalog cùng `actual-weight.ps1` để hồi quy đơn từ tạo/giao nhận/cân giá/Mock payment tới nhận đồ. Script chạy với seed trong DB CI tạm, không thay tài khoản thật.

## Commit cập nhật cùng PR

Tại gốc DO_AN, xác nhận đang ở nhánh CI. Không dùng `git add .`, vì còn thay đổi keyword đánh giá và tài liệu khác chưa thuộc PR này:

```powershell
git branch --show-current
git add .github/workflows/backend-ci.yml
git add washgo-be/Directory.Build.props washgo-be/Dockerfile
git add washgo-be/src/WashGo.Application/WashGo.Application.csproj
git add washgo-be/src/WashGo.Application/WashGoApplicationModule.cs
git add washgo-be/src/WashGo.Application/Mappings
git add "washgo-be/src/WashGo.Application/Features/*/Mappings/*MappingProfile.cs"
git add washgo-be/tests/smoke/Mapping
git add washgo-be/tests/smoke/mapping-compatibility.ps1
git add washgo-be/tests/ci/Run-ApiSmoke.ps1
git add washgo-be/docs/CI_DEPENDENCY_SECURITY_FIX.md washgo-be/docs/BACKEND_CI_GUIDE.md
git diff --cached --stat
git diff --cached --check
git commit -m "fix(deps): replace AutoMapper with Mapster and patch vulnerable packages"
git push origin ci/toantran/backend-pr-checks
```

Agent chưa commit/push. Sau push mở PR #106 → Checks; chờ Security và Docker package thực sự xanh rồi mới merge. Không đổi severity, bỏ scanner, whitelist advisory thật hoặc bỏ gate để tạo PASS.
