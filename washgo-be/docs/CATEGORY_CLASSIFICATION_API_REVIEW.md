# Bàn giao FE — chỉ chọn Danh mục, bỏ Loại xử lý riêng

Ngày 19/11/2026. Thực hiện `Docs/cần làm/BACKEND_CATEGORY_REMAINING.md`.

## Hợp đồng

- Tạo/sửa dịch vụ (`POST/PUT /wash-service`) và tạo đề xuất Merchant (`POST /merchant-service/proposals`, đề xuất sửa dùng danh mục của dịch vụ) chỉ cần `categoryId`. FE bỏ dropdown/validation/payload `serviceType`.
- Tạo mới không có `categoryId` → 400 `meta.code = "CategoryRequired"`. Sửa không gửi `categoryId` thì giữ danh mục hiện tại.
- `serviceType` trong response chỉ còn là **loại xử lý legacy**: có giá trị 1–5 khi dịch vụ thuộc một trong 5 danh mục gốc (ánh xạ cố định theo UUID danh mục gốc), `null` với danh mục mới. FE không hiển thị như một lựa chọn và không lọc trùng theo trường này.
- Tương thích client cũ: chỉ gửi `serviceType` (không có `categoryId`) → BE dùng danh mục gốc tương ứng. Gửi cả hai → danh mục quyết định, `serviceType` gửi kèm bị bỏ qua.
- Bộ lọc `serviceType` (Admin/Merchant) và `serviceTypes` (Customer, chatbot) vẫn nhận nhưng nghĩa là "danh mục gốc của loại đó". Lọc mới dùng `categoryIds`.

## Dữ liệu, ETA và lịch sử

- Dự đoán ETA nhóm lịch sử theo danh mục (snapshot danh mục của dòng đơn; đơn cũ không có snapshot dùng ánh xạ cố định từ loại xử lý legacy). Danh mục chưa đủ lịch sử dùng ETA theo quy tắc (`predictionSource = "rule"`), không mượn lịch sử danh mục khác.
- Snapshot đơn (`categoryIdSnapshot`, `processingTypeSnapshot`) và JSON đề xuất cũ giữ nguyên; đề xuất cũ chỉ có `serviceType` vẫn được đọc qua ánh xạ danh mục gốc (`categoryMappingBasis = "LegacyServiceTypeMapping"`).
- Migration `MakeServiceLegacyTypeOptional` chỉ cho phép cột loại xử lý nhận null, không ghi lại dữ liệu cũ. Dịch vụ mới/sửa ở danh mục mới có loại legacy null.
