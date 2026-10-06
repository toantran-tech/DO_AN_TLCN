# WashGo — Nền tảng Giặt sấy & Tủ đồ thông minh

Dự án đồ án tốt nghiệp xây dựng hệ thống giặt sấy thông minh kết hợp mạng lưới trạm tủ đồ (Smart Locker), ứng dụng mô hình kiến trúc Domain-Driven Design (DDD), Clean Architecture trên nền tảng .NET 8 và giao diện quản trị React TypeScript.

---

## 🚀 Khởi chạy nhanh dự án với Docker

Hệ thống đã được đóng gói hoàn chỉnh bằng Docker Compose (gồm PostgreSQL 16, Backend API .NET 8 và Admin Web UI Vite + Nginx).

```bash
# Khởi động toàn bộ dịch vụ (PostgreSQL + Backend + Admin)
docker compose up -d --build

# Xem logs hoạt động
docker compose logs -f

# Dừng hệ thống
docker compose down
```

- **Admin Web UI**: [http://localhost:5173](http://localhost:5173) (Tài khoản mẫu: `admin@washgo.vn` / `Admin@123`)
- **Backend API Swagger**: [http://localhost:5000/swagger](http://localhost:5000/swagger)
- **PostgreSQL Database**: `localhost:5432` (`washgo_db` / `postgres` / `123456`)

---

## 🌿 Git Workflow for Team Development (Quy trình Git cho nhóm 3 người)

Quy trình quản lý mã nguồn được chuẩn hóa theo mô hình phân nhánh rút gọn từ GitFlow, đảm bảo tính ổn định của mã nguồn, hạn chế xung đột (conflict) và tạo điều kiện làm việc độc lập, trơn tru cho nhóm 3 thành viên.

### 1. Cấu trúc các nhánh (Branches)

- **`main`**:
  - Chỉ chứa mã nguồn của phiên bản ổn định (production-ready).
  - **Tuyệt đối không code hoặc push trực tiếp lên `main`**.
  - Chỉ nhận code được merge từ `develop` khi hệ thống đã qua kiểm thử ổn định hoặc chuẩn bị đến đợt demo / nộp bài.
- **`develop`**:
  - Nhánh tích hợp trung tâm của cả nhóm.
  - Chứa phiên bản phát triển mới nhất đang được xây dựng.
  - Mọi tính năng sau khi hoàn thành sẽ tạo **Pull Request (PR)** để merge vào `develop`.
  - **Không khuyến khích push trực tiếp lên `develop`**.
- **`feature/*`**:
  - Nhánh chức năng: mỗi tính năng hoặc tác vụ độc lập tạo một branch riêng tách ra từ `develop`.
  - Quy ước đặt tên theo định dạng `feature/<tên-chức-năng>`.
  - *Ví dụ*: `feature/login`, `feature/register`, `feature/product-management`, `feature/dashboard`, `feature/locker-management`.
  - **Không** đặt tên branch theo tên thành viên (tránh `feature/nam`, `feature/toan`).

---

### 2. Sơ đồ luồng phát triển (Workflow Diagram)

```text
feature/*  ───(Pull Request & Review)───►  develop  ───(Pull Request & Release)───►  main
```

```text
main (Phiên bản ổn định / Demo / Nộp bài)
  ▲
  │ [Pull Request khi Release / Nộp đồ án]
develop (Nhánh tích hợp chung của nhóm)
  ▲
  │ [Pull Request sau khi hoàn tất Feature]
  ├── feature/login
  ├── feature/product-management
  └── feature/dashboard
```

---

### 3. Hướng dẫn các bước thao tác chi tiết (Step-by-step Guide)

#### Bước A: Khi bắt đầu làm một feature mới
Luôn đồng bộ code mới nhất từ `develop` trước khi tách nhánh tính năng:

```bash
# 1. Chuyển về nhánh develop và lấy code mới nhất về
git checkout develop
git pull

# 2. Tạo và chuyển sang nhánh feature mới
git checkout -b feature/<feature-name>
```
*Ví dụ*: `git checkout -b feature/login`

---

#### Bước B: Khi đã code xong tính năng
Commit các thay đổi kèm thông điệp rõ nghĩa và đẩy nhánh lên GitHub:

```bash
# 1. Thêm các file thay đổi vào staging
git add .

# 2. Commit với message đúng quy chuẩn
git commit -m "feat: <mô tả ngắn gọn chức năng>"

# 3. Đẩy branch lên GitHub
git push -u origin feature/<feature-name>
```

Sau khi push thành công, truy cập giao diện GitHub của repository để tạo **Pull Request**:
- **Base (nhánh đích)**: `develop`
- **Compare (nhánh nguồn)**: `feature/<feature-name>`

---

#### Bước C: Cập nhật code trước khi tạo PR (hoặc khi `develop` đã có code mới)
Nếu các thành viên khác đã merge code mới vào `develop`, hãy kéo code `develop` mới về nhánh feature của bạn và giải quyết conflict trên máy cá nhân trước:

```bash
# 1. Cập nhật nhánh develop local
git checkout develop
git pull

# 2. Quay lại nhánh feature và gộp code mới từ develop vào
git checkout feature/<feature-name>
git merge develop
```

Nếu có xung đột (merge conflicts):
1. Mở editor (VS Code) và giải quyết xung đột tại các file được đánh dấu.
2. Kiểm tra lại logic và test thử ứng dụng.
3. Hoàn tất merge và đẩy lên GitHub:

```bash
git add .
git commit -m "fix: resolve merge conflicts"
git push
```

---

#### Bước D: Sau khi Pull Request được merge vào `develop`
Khi tính năng đã được duyệt và merge thành công trên GitHub:

```bash
# Cập nhật develop local với code vừa merge
git checkout develop
git pull
```

*Muốn làm chức năng tiếp theo?* Lặp lại từ **Bước A** bằng cách tạo branch mới từ `develop` vừa cập nhật.

---

#### Bước E: Khi chuẩn bị Release / Demo / Nộp bài
Khi toàn bộ các tính năng trong đợt phát triển đã hoàn tất trên `develop` và sẵn sàng báo cáo:
1. Tạo Pull Request trên GitHub:
   - **Base**: `main`
   - **Compare**: `develop`
2. Cả nhóm cùng review, kiểm tra lần cuối và merge vào `main`.

---

### 4. Quy ước viết Commit Message (Commit Conventions)

Mỗi commit message cần mở đầu bằng một tiền tố biểu thị rõ mục đích thay đổi:

| Tiền tố | Ý nghĩa | Ví dụ minh họa |
| :--- | :--- | :--- |
| `feat:` | Thêm tính năng hoặc chức năng mới | `feat: add login form` |
| `fix:` | Sửa lỗi / fix bug | `fix: handle login validation error` |
| `refactor:` | Chỉnh sửa cấu trúc code (không đổi logic hay tính năng) | `refactor: extract API auth service` |
| `docs:` | Cập nhật tài liệu, tài liệu hướng dẫn | `docs: update README with git workflow` |
| `chore:` | Thay đổi cấu hình, build tool, thư viện phụ | `chore: update docker compose ports` |

---

### 5. Các quy tắc vàng của nhóm (Team Rules)

> [!IMPORTANT]
> **Tuyệt đối tuân thủ 7 nguyên tắc sau để dự án không bao giờ bị mất code hoặc xung đột lớn:**

1. ❌ **Không bao giờ push trực tiếp lên `main`**: `main` chỉ nhận code thông qua Pull Request từ `develop`.
2. ⚠️ **Hạn chế push trực tiếp lên `develop`**: Luôn làm việc trên `feature/*` và tạo Pull Request để các thành viên khác có thể theo dõi.
3. 🔄 **Luôn `git pull` từ `develop` trước khi rẽ nhánh mới**: Không bao giờ tạo feature branch mới từ một nhánh tính năng khác hoặc từ `develop` chưa cập nhật.
4. 🎯 **Một branch chỉ phục vụ một tính năng**: Tránh sửa lan man nhiều module không liên quan trong cùng một branch.
5. ⏱️ **Không giữ feature branch quá lâu**: Chia nhỏ công việc để hoàn thành và merge trong vòng 1-2 ngày, tránh để branch tách biệt quá lâu dẫn đến xung đột lớn.
6. 🛠️ **Giải quyết xung đột (conflict) trên feature branch của mình**: Luôn merge `develop` vào `feature/*` để xử lý conflict và test trước khi yêu cầu gộp vào `develop`.
7. 🚫 **Không đặt commit message vô nghĩa**: Nghiêm cấm các commit kiểu `"update"`, `"fix"`, `"abc"`, `"done"`, `"final"`, `"final2"`, `"fix bug"`. Luôn viết rõ mục đích sửa đổi.
