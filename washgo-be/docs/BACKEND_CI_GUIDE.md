# Hướng dẫn CI Backend WashGo

Đợt 1 tự kiểm tra Pull Request bằng GitHub Actions; chưa deploy staging, chưa đẩy image lên registry và không dùng key thật. Fen vẫn tạo nhánh → commit/push → PR như thường. GitHub tự chạy các job và lưu report để reviewer kiểm tra.

Cập nhật PR #106: run đầu chặn đúng 3 dependency High; sau bản vá 04bb072, người dùng đã gửi ảnh cả 5 check GitHub PASS. Người dùng chốt Mapster MIT không cần key, giữ ObjectMapper ABP và bản vá Microsoft. Xem [CI_DEPENDENCY_SECURITY_FIX.md](CI_DEPENDENCY_SECURITY_FIX.md) cho kết quả, danh sách file và lệnh commit; không cần đăng ký license mapper.

## 1. Những gì đã chuẩn bị

Workflow nằm tại **DO_AN/.github/workflows/backend-ci.yml**, cùng cấp với `washgo-be`, không nằm bên trong thư mục Backend. GitHub chỉ nhận workflow ở thư mục `.github/workflows` tại gốc repository.

| Check hiển thị trên GitHub | Công việc thực tế |
|---|---|
| Build .NET | Restore và build Host Release bằng .NET 8 |
| Analyze code | Kiểm tra whitespace thay đổi, cú pháp script PowerShell và chạy analyzer .NET, lưu log |
| Regression tests | Mapping 5 module, Privacy theo role, PDF/DOC/DOCX và email MIME; tạo Backend + PostgreSQL mới, migrate/seed, kiểm thử ABP/JWT, catalog, lifecycle/cân giá đơn, keyword privacy và Publish đánh giá |
| Secret and dependency scan | Gitleaks quét lịch sử checkout đầy đủ, che giá trị secret; audit NuGet trực tiếp và bắc cầu, chặn High/Critical |
| Docker package | Sau khi các check trên thành công, build image gắn commit SHA, lưu image ID; chưa publish/deploy |

Trigger: PR vào `develop`/`main`, push vào hai nhánh này, hoặc Run workflow thủ công sau khi workflow đã có trên default branch. Chỉ push feature branch chưa có PR thì workflow không tự chạy. Không dùng path filter trong đợt đầu để tránh required check bị Pending do thay đổi không khớp filter.

Build và analysis/security có thể chạy song song. Regression chờ build; Docker package chờ cả build, analysis, regression và security. Không phải một chuỗi 6 chặng deploy theo rubric; đây là CI giai đoạn đầu, còn thiếu CD.

## 2. Dữ liệu và key test

- `tests/ci/compose.yml` là Compose độc lập, không merge với Compose local.
- `tests/ci/ci.env` chỉ có comment; truyền bằng `--env-file` để không nạp `.env` của máy.
- DB `washgo_ci` nằm trong tmpfs của container PostgreSQL test, không bind cổng DB host và không dùng volume DB thật.
- Project/container/image test có tên riêng `washgo-ci-…`; Backend test bind loopback cổng mặc định 5088. Nếu cổng bận, chạy với `-Port 5089`.
- Payment Mock chỉ trong Compose CI; không cần VNPay/MoMo/Gemini/Google/SMTP thật. Không kiểm chứng thanh toán cổng thật hay Gemini thật trong suite này.
- Test API kiểm tra bảng/seed thực sự sau startup, không coi Swagger lên là migration thành công. Catalog fixture được xóa; Compose dọn ở finally. Nếu runner bị hủy giữa chừng, VM GitHub-hosted tạm được bỏ sau job; không có dữ liệu thật trong đó.
- `.dockerignore` chặn `.env`, `.env.*` và thư mục results khỏi build context. `tests/ci/results/` đã ignore Git; không commit report/log sinh ra hoặc `.env`.

Không tự lấy 77 smoke script chạy toàn bộ: nhiều script cần dữ liệu và cấu hình đặc thù. Chỉ những suite đã tích hợp bên trên được tính là đang có trong CI.

## 3. Chạy thử trên máy trước khi push

Mở terminal trong DO_AN/washgo-be; Docker Desktop cần hoạt động và máy có .NET SDK 8. PowerShell 7 (`pwsh`) là shell trên GitHub runner; các helper cũng đã chạy thử cục bộ bằng Windows PowerShell.

```powershell
dotnet restore WashGo.HttpApi.Host/WashGo.HttpApi.Host.csproj
dotnet build WashGo.HttpApi.Host/WashGo.HttpApi.Host.csproj -c Release --no-restore
./tools/ci/Analyze.ps1
dotnet run --project tests/smoke/PrivateDocuments/PrivateDocuments.csproj -c Release
dotnet run --project tests/smoke/EmailLogo/EmailLogo.csproj -c Release
dotnet run --project tests/smoke/Mapping/Mapping.csproj -c Release
dotnet run --project tests/smoke/Privacy/Privacy.csproj -c Release
./tests/ci/Run-ApiSmoke.ps1
./tools/ci/Security.ps1
```

Lệnh API test tự dựng/dọn DB test; không chạy `docker compose down -v` với Compose dự án thật. Xem log tại `tests/ci/results/containers.log` nếu test lỗi. Security cần restore graph trước và truy cập mạng tới nguồn advisory; scanner không chạy được cũng phải báo lỗi, không coi là PASS.

## 4. Kết quả kiểm tra ban đầu và giới hạn

- Build Host Release và bước analyzer/kiểm tra cú pháp helper local PASS; analyzer hiện vẫn báo warning theo cấu hình dự án. Build cache có thể không in lại warning; không dùng output incremental không warning làm bằng chứng 0 lint toàn repo.
- PDF/DOC/DOCX và email MIME regression PASS, không gửi email thật.
- Backend Docker + PostgreSQL mới PASS migrate/seed và bộ API catalog: visibility, name/price sort, khoảng giá/loại dịch vụ nhiều lựa chọn, validation, pagination và cleanup. Container test đã dọn.
- Gitleaks v8.30.0 quét 143 commit local, không phát hiện secret theo bộ luật scanner. Không phải cam kết mọi hình thức rò rỉ đều đã được phát hiện.
- **Phương án cuối:** bỏ AutoMapper/Volo.Abp.AutoMapper, dùng Mapster 10.0.14 MIT qua provider ABP; Caching.Memory 8.0.1, System.Text.Json 8.0.6 và JWT 8.14.0. Không cần key. Sau chuyển Mapster, Security local PASS (0 High/Critical), Docker HTTP mapping/JWT/catalog và vòng đời đơn/actual-weight PASS; giữ nguyên gate.
- Workflow đã qua actionlint và có run GitHub đầu tiên theo ảnh/log PR #106: Build/Analyze/Regression xanh, Security đỏ với graph cũ. Bản vá 04bb072 đã commit/push và người dùng gửi ảnh cả 5 check PASS. Các thay đổi privacy/review mới trên nhánh riêng chưa push nên chưa có kết quả Actions cho cụm mới.
- Chưa có coverage report, duplication metric, gate 0 lint/0 analyzer warning toàn repo, E2E/UI/tải, registry, staging, rollback hoặc monitoring. Chưa đạt trọn TC2.4/TC2.5/TC2.6 chỉ nhờ workflow này.

Không thêm `continue-on-error`, giảm severity hoặc allowlist rộng chỉ để job Security xanh. Finding giả cần bằng chứng và ngoại lệ hẹp có người review; dependency có lỗi thật cần nâng/test hoặc quyết định xử lý được ghi lại.

## 5. Tạo nhánh và push đúng phần CI

Trong terminal ở **gốc DO_AN**, kiểm tra trước:

```powershell
git status --short
git branch --show-current
git switch -c ci/toantran/backend-pr-checks
```

Nếu tên nhánh đã có, dùng `git switch ci/toantran/backend-pr-checks`. Không reset/stash/delete thay đổi đang có một cách mù quáng. Hiện còn các thay đổi riêng về keyword đánh giá, plan và rubric; không dùng `git add .` để gộp hết vào PR CI.

Stage đúng các file CI:

```powershell
git add .github/workflows/backend-ci.yml
git add washgo-be/.dockerignore
git add washgo-be/tools/ci/Analyze.ps1 washgo-be/tools/ci/Security.ps1
git add washgo-be/tests/ci/.gitignore washgo-be/tests/ci/ci.env
git add washgo-be/tests/ci/compose.yml washgo-be/tests/ci/Run-ApiSmoke.ps1
git add washgo-be/docs/BACKEND_CI_GUIDE.md
git diff --cached --stat
git diff --cached --check
git commit -m "ci(be): add PR checks and isolated API regression"
git push -u origin ci/toantran/backend-pr-checks
```

Hướng dẫn chỉ đưa lệnh; agent chưa tự tạo nhánh/commit/push. Checklist đang có thay đổi nhiều nhiệm vụ nên không stage cả checklist vào PR CI nếu chưa tách hunk.

## 6. Trên GitHub

1. Vào repo `toantran-tech/DO_AN_TLCN` → Pull requests → New pull request.
2. Base `develop`, compare `ci/toantran/backend-pr-checks`; tạo PR, có thể để Draft trong lúc xử lý dependency finding.
3. Mở tab Checks của PR hoặc Actions → Backend CI. PR đầu tiên có workflow mới sẽ chạy workflow theo trigger; không cần tạo workflow mẫu thứ hai trên giao diện.
4. Nếu GitHub báo Actions disabled: người có quyền quản trị vào Settings → Actions → General, bật Actions và cho phép các action chính thức `actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact` theo chính sách repo. Không cần cấp quyền write cho GITHUB_TOKEN.
5. Mở từng job. Dòng đỏ là lỗi cần xử lý; Docker package bị Skipped khi job trước thất bại là hành vi đúng.
6. Vào trang run, phần Artifacts để tải `backend-build`, `backend-analysis`, `backend-tests`, `backend-security-redacted`, `backend-image-metadata` nếu job tương ứng đã sinh file. Retention cấu hình 14 ngày; tải minh chứng các mốc quan trọng trước khi hết hạn.
7. Sửa lỗi trên cùng nhánh rồi push tiếp. PR tự chạy lại; không cần tạo PR mới mỗi lần. Không merge khi check đang đỏ nếu mục tiêu là baseline CI xanh.

Nếu PR tới từ fork, GitHub có thể yêu cầu maintainer approve workflow theo chính sách repo. Workflow không dùng secrets thật và không dùng `pull_request_target`.

## 7. Sau run đầu tiên

Khi các check đã chạy và ổn định, quản trị repo cấu hình bảo vệ `develop`/`main`: yêu cầu PR/review và required status checks của workflow này. Chọn tên check thực tế đã xuất hiện trên GitHub, không đoán tên trước khi có run. Không tắt quyền review để pipeline xanh tự merge code.

Sau đó mới làm CD: chọn server staging, HTTPS, image registry/tag/digest, secrets môi trường, migration/backup, deploy/health check và rollback. CI hiện tại không đếm thành một lần deploy staging tự động và không thay UAT.

## 8. Nguồn dùng để cấu hình

- GitHub .NET Actions: https://docs.github.com/en/actions/tutorials/build-and-test-code/net
- Actions checkout: https://github.com/actions/checkout
- Setup .NET: https://github.com/actions/setup-dotnet
- Upload artifact: https://github.com/actions/upload-artifact
- Gitleaks CLI: https://github.com/gitleaks/gitleaks

Action được ghim commit SHA đã xác minh; Gitleaks ghim version và image digest. Khi cập nhật cần kiểm tra release/test lại, không tự thay bằng `latest`.

## Bổ sung cụm lưu kho ngày 14/11/2026

Regression có `tests/smoke/Storage` (fee/cycle/transit/HMAC/busy predicate) và `storage-lifecycle.ps1` trong API runner. HTTP suite kiểm tra hai vòng kho, concurrent replay, PIN, phí, rollback hết ô, self-status, legacy và worker; dùng Mock/DB riêng. Chỉ CI chạy bộ rộng sau push; local tập trung test mới, không rebuild Docker chính mỗi vòng sửa. Run mới phải xem Actions/artifact, không suy từ kết quả run PR cũ. Các script legacy chưa trong runner không tự được coi PASS.

## Customer bổ sung 14/11/2026

API runner có customer-locker-slots.ps1 và customer-public-reviews-notifications.ps1, bao gồm slots/full locker, public approval/media/summary và safe notification query. Chạy trên DB CI riêng, không dùng main DB. Người dùng chốt từ đây để PR CI chạy bộ test; local chỉ kiểm tra cần thiết, không tự build/recreate Docker hoặc chạy bộ rộng mỗi lần sửa.
