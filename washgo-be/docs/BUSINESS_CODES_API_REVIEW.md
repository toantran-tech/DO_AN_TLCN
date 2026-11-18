# Bàn giao FE — mã nghiệp vụ tự sinh và validate Locker

Ngày 19/11/2026. Thực hiện `Docs/cần làm/BACKEND_CODE.md` và `BACKEND_LOCKER_REMAINING.md`.

## Mã do BE cấp

| Đối tượng | Mã mới | API |
| --- | --- | --- |
| Dịch vụ (Admin tạo và dịch vụ tạo khi duyệt đề xuất Merchant) | `SVdddd` | `POST/PUT /wash-service`, `POST /service-proposal/{id}/approve` |
| Danh mục dịch vụ | `DMdddd` | `POST /admin/service-categories`, `PUT /admin/service-categories/{id}` |
| Trạm locker | `LKdddd` | `POST/PUT /locker` |
| Vùng phục vụ | `KVdddd` | `POST/PUT /service-area` |
| Mã lưu giữ vòng kho | `STdddd` | cấp khi vòng kho chuyển Stored (không có API nhập) |

- Đúng 2 chữ in hoa + 4 chữ số (`0000`–`9999`, giữ số 0 đầu). BE cấp trong transaction tạo và trả trong response (`code`; vòng kho: `storageReference`). FE không gửi/sinh/đoán mã.
- Tạo mới: trường `code` không bắt buộc; client cũ gửi mã thì bị bỏ qua. Sửa: không gửi `code` (khuyến nghị) hoặc gửi đúng mã hiện tại (kể cả mã cũ, không phân biệt hoa thường); mã khác → 400 `meta.code = "CodeImmutable"` và không đổi gì.
- Mã bất biến. Mã cũ, mã seed (`SVC_...`, `LK_...`, `STANDARD_WASH`...) và mã lưu giữ đã phát hành `LK` + 18 số giữ nguyên; FE hiển thị mọi dạng.
- Gửi lại cùng `operationId` trả đúng tài nguyên và mã cũ, không cấp mã mới.
- Mỗi tiền tố có 10.000 mã. Hết mã → 409 `CodeSpaceExhausted` (không tạo bản ghi dở). Hiếm khi tranh chấp kéo dài → 409 `CodeAllocationBusy`, thử lại được. Mã của bản ghi đã xóa mềm không bị cấp lại.
- Không đổi: mã đơn `WG...`/`MC...`, mã khuyến mãi Admin nhập tay, OTP/PIN/QR/token, số ô locker.

## Locker: địa chỉ và lỗi theo trường

- Tạo locker chỉ cần tên, địa chỉ, tỉnh/thành, tọa độ, Merchant và cấu hình ô; `ward`/`district` không bắt buộc (`district` mặc định chuỗi rỗng, `ward` null).
- Sửa: không gửi `ward`/`district` (null) thì giữ giá trị đã lưu; gửi chuỗi thì cập nhật.
- Lỗi validate trả 400, `meta.code = "ValidationFailed"`, `meta.fields` và `errors` theo từng trường (camelCase): `name`, `address`, `city`, `district`, `ward`, `description`, `latitude`, `longitude`, `lockerType`, `merchantIds`, `serviceAreaId`; `message` là lỗi đầu tiên. Ví dụ:

```json
{ "succeeded": false, "statusCode": 400, "message": "Tỉnh/thành không được để trống (tối đa 100 ký tự).",
  "errors": { "city": ["Tỉnh/thành không được để trống (tối đa 100 ký tự)."] },
  "meta": { "code": "ValidationFailed", "fields": ["city"] } }
```

- Mã khác khi sửa: `errors.code` và `meta.code = "CodeImmutable"`.

## Dữ liệu và triển khai

Migration `AddBusinessCodeClaims` tạo bảng `business_code_claims` (mã duy nhất, một mã mỗi tài nguyên) và giữ chỗ các mã cũ đã có dạng mới. Không đổi mã dữ liệu cũ. FE mới (không gửi mã) cần BE này trước hoặc cùng lúc.
