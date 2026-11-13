# Chia nhánh chuẩn hóa using và chatbot

Ngày tách: 13/11/2026. Base local: `develop` tại `bb18f18` (PR #99). Hai nhánh nối tiếp, chưa push hoặc tạo PR.

| Thứ tự | Nhánh | Nội dung | PR vào develop |
|---|---|---|---|
| 1 | `refactor/toantran/be-namespace-imports` | Chuẩn hóa using/alias trong source BE, không thêm chatbot hoặc schema mới | Tạo và merge trước |
| 2 | `feat/toantran/customer-chatbot-gemini` | Hội thoại/lazy load, scope, Gemini, công cụ đọc, migration, cấu hình, smoke và bàn giao | Tạo sau khi PR 1 đã merge |

Nhánh 2 phát triển tiếp từ nhánh 1. Để giữ ancestry và PR sau không hiện lại commit refactor, ưu tiên **Create a merge commit** khi merge PR 1. Nếu nhóm dùng squash/rebase merge, cần cập nhật nhánh 2 trước khi mở PR tiếp; không force-push tùy ý.

Đã build riêng nội dung staged của từng nhánh trong thư mục review độc lập: cả hai PASS, 0 lỗi; còn 5 nullable warnings có sẵn. Nhánh refactor không cần các file chatbot chưa commit để build.

## Lệnh push

Chạy tại repo `DO_AN` hoặc `washgo-be`; không cần đổi nhánh trước mỗi lệnh:

```powershell
git push -u origin refactor/toantran/be-namespace-imports
git push -u origin feat/toantran/customer-chatbot-gemini
```

Sau đó tự tạo PR trên GitHub, **base: develop**. Tạo/merge PR 1 trước; PR 2 sau khi develop đã có refactor. Không chọn main làm base cho hai PR này.

## Nội dung PR đề xuất

PR 1:

```text
refactor(be): simplify namespace references with using directives
```

Mô tả: Đưa tên type đầy đủ từ phần xử lý lên using/alias đầu file, giữ nguyên nghiệp vụ, chuỗi resource và migration. Build riêng nội dung nhánh để kiểm tra không phụ thuộc chatbot.

PR 2:

```text
feat(chatbot): add Gemini customer assistant and conversation history
```

Mô tả: Bổ sung trợ lý Customer tra cứu dữ liệu WashGo qua Gemini, lịch sử hội thoại lazy load, request replay, quota và retention. Có migration AddCustomerChatbot và 6 API. Smoke fixture và 4 live test Gemini đã PASS; còn nghiệm thu rộng đơn/ETA/policy, prompt injection và FE.

Chi tiết API/cấu hình/test: [CHATBOT_API_REVIEW.md](CHATBOT_API_REVIEW.md). Phạm vi và việc còn lại: [CHATBOT_AI_IMPLEMENTATION_PLAN.md](CHATBOT_AI_IMPLEMENTATION_PLAN.md).

## Các phần không đưa vào commit

- `.env` chứa key: bị ignore, không push.
- Repo FE riêng `WashGo/`: không stage hoặc đưa thành submodule.
- Thay đổi có sẵn `BACKEND_DB_NAME` trong compose: giữ nguyên ở working tree, ngoài commit chatbot. Commit chatbot chỉ thêm hai dòng map Gemini vào compose.

Working tree vì vậy vẫn có thể báo compose modified và WashGo untracked sau khi tách. Không dùng `git add .` để gom chúng vào hai commit đã tách.
