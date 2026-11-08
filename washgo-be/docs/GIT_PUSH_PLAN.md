# Thứ tự push và tạo PR — 09/11/2026

Đợt thay đổi được chia thành **4 nhánh nối tiếp**, mỗi nhánh thêm một commit so với nhánh trước. Điểm xuất phát: `f68a9b4` (merge PR #79). Chưa push nhánh nào trong đợt này.

| Thứ tự | Nhánh | Nội dung commit thêm |
| --- | --- | --- |
| 1 | `feat/toantran/merchant-private-documents` | Giấy tờ riêng tư Merchant, upload lifecycle, nền tảng version/operation/batch, schema/EF/migrations dùng chung. |
| 2 | `feat/toantran/settings-order-resources` | Cấu hình, snapshot chính sách đơn, khóa vận hành, Locker và dịch vụ/duyệt đề xuất. |
| 3 | `feat/toantran/finance-incident-safety` | Quote/đối soát, partial refund và hoàn khác kỳ, khiếu nại/bồi thường, phân trang dòng đối soát tại DB và giới hạn báo cáo. |
| 4 | `feat/toantran/promotion-admin-filters` | Promotion version/status/batch, sort, lịch sử voucher, kiểm thử liên module và tài liệu/checklist. |

Schema/DbContext/model snapshot và sáu migration được giữ cùng ở nhánh 1 để không tạo các bản model/migration lệch nhau. Các trường mới chuẩn bị cho chức năng ở nhánh sau. Nhánh sau chứa các commit của nhánh trước; đây là phụ thuộc có chủ ý, không phải bốn nhánh độc lập cùng tách từ develop.

## Push

Chạy trong terminal của repository. Dùng tên nhánh đầy đủ nên không cần checkout từng nhánh:

```powershell
git push -u origin feat/toantran/merchant-private-documents
git push -u origin feat/toantran/settings-order-resources
git push -u origin feat/toantran/finance-incident-safety
git push -u origin feat/toantran/promotion-admin-filters
```

## Tạo và merge PR

Cách dễ theo dõi: **tạo/merge từng PR vào develop theo thứ tự 1 → 2 → 3 → 4**, sau khi PR trước đã merge mới tạo PR tiếp. Dùng **Create a merge commit** cho chuỗi này để giữ quan hệ commit; squash/rebase merge có thể khiến PR sau hiển thị lại commit cũ và cần rebase riêng.

Nếu muốn mở bốn PR trước để review, đặt base lần lượt:

| PR | Base lúc mở trước | Compare |
| --- | --- | --- |
| 1 | `develop` | `feat/toantran/merchant-private-documents` |
| 2 | `feat/toantran/merchant-private-documents` | `feat/toantran/settings-order-resources` |
| 3 | `feat/toantran/settings-order-resources` | `feat/toantran/finance-incident-safety` |
| 4 | `feat/toantran/finance-incident-safety` | `feat/toantran/promotion-admin-filters` |

Sau khi PR trước merge vào develop bằng merge commit, đổi base PR kế sang `develop` rồi merge. Không merge PR 2–4 vào nhánh tính năng cũ. Không cần force push.

Gợi ý tiêu đề PR lần lượt:

1. `feat(platform): add private merchant documents and administrative write foundation`
2. `feat(operations): snapshot order policies and guard locker and service management`
3. `feat(finance): secure settlements refunds and incident compensation`
4. `feat(promotion): add guarded management usage history and admin filters`

Kiểm thử hành vi đã ghi trong [IMPLEMENTATION_CHECKLIST.md](../IMPLEMENTATION_CHECKLIST.md); mỗi mốc commit được build riêng từ checkout sạch trong quá trình chia nhánh. `.env`, build output và fixture tài liệu được ignore, không đưa vào commit.

Repository làm việc được để ở nhánh 4, chứa đầy đủ các thay đổi. Không checkout nhánh 1 để chạy ứng dụng rồi nhầm rằng các chức năng ở nhánh 2–4 đã có trong source nhánh đó. Docker đang chạy vẫn là bản đầy đủ đã build trước khi chia nhánh.
