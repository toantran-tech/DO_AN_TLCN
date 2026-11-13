> Bổ sung 14/11/2026: luồng kho/giao lại, task type 3/4, order 14/15 và self-status xem [STORAGE_WORKFLOW_API_REVIEW.md](STORAGE_WORKFLOW_API_REVIEW.md). Với đơn v1 không dùng shortcut nhận trực tiếp ở kho. Chatbot hướng dẫn yêu cầu giao lại locker; không đưa mã/địa chỉ tiệm vào AI. Phần cũ bên dưới cần đọc cùng hợp đồng bổ sung; Docker chính chưa cập nhật cụm này.

> Quyết định thành viên mới nhất và thay đổi hợp đồng tại [AGREED_BUSINESS_RULES_20261109.md](AGREED_BUSINESS_RULES_20261109.md). Phần này cập nhật nghiệp vụ hợp đồng/khiếu nại/bù trừ; không còn chặn tạo kỳ chỉ vì thực nhận âm hoặc bằng 0.

# Hướng dẫn review đợt hoàn thiện Backend — 09/11/2026

Phạm vi: checklist tại `washgo-be/IMPLEMENTATION_CHECKLIST.md`, đối chiếu 13 tài liệu trong `C:/Users/PC/Desktop/HK1_2027/cần làm`. Đây là thay đổi BE; FE cần cập nhật các hợp đồng ghi bên dưới. Đã chia commit thành 4 nhánh theo yêu cầu; chưa push. Thứ tự PR tại GIT_PUSH_PLAN.md. Các thao tác chi trả/hoàn/bồi thường chỉ ghi nhận chuyển khoản ngoài hệ thống, không gọi ngân hàng.

## Quy ước ghi và tra cứu

- Đọc bản ghi trước khi sửa, gửi lại `expectedVersion` dạng **chuỗi**. Thiếu trả 400, đã thay đổi trả 409. Giữ nguyên key và payload khi retry.
- `operationId` dạng UUID lưu kết quả cùng transaction. Cùng key/payload trả kết quả cũ; khác actor/payload/action/resource trả 409. Không tạo thêm audit/notification.
- `GET /admin-operations/{module}/{operationId}`: module `Finance`, `SystemSetting`, `Locker`, `Service`, `Promotion`, `Incident`. Chỉ Admin tạo key đó được tra. 404 không chứng minh request đang chạy đã thất bại.
- Merchant giữ lookup riêng `GET /merchant/operations/{id}`; gửi đề xuất hồ sơ hỗ trợ `operationId` và không tạo hai yêu cầu Pending.
- Batch `POST /locker/batch`, `/wash-service/batch`, `/promotion/batch`: `operationId` bắt buộc; 1–50 locker/dịch vụ, 1–100 khuyến mãi; không trùng ID. Mỗi dòng cần `id`, `expectedVersion`. `action=Delete|Status`; trạng thái là giá trị đích (`status` hoặc `isActive`). Kết quả từng ID, có thể thành công một phần. Ý định batch được ghi trước, thao tác con có key xác định để tiếp tục an toàn sau mất kết nối. Lookup batch xem `result.data.state`.

```json
{
  "operationId": "<UUID mới>",
  "action": "Status",
  "isActive": false,
  "items": [{ "id": "<promotionId>", "expectedVersion": "<version từ GET>" }]
}
```

## Giấy tờ Merchant

- `POST /files/documents/`, multipart: `file`, `kind=BusinessLicense|Contract|Sla`, `merchantId` tùy chọn khi Admin upload trước tạo Merchant. PDF/DOC/DOCX tối đa 10 MB, kiểm tra cấu trúc nội dung và phần mở rộng; không tin MIME do client gửi. DOC mã hóa và DOCX macro/DTD bị từ chối.
- Kết quả có `fileId`, tên, MIME, dung lượng, loại, hạn bản nháp, `downloadUrl`, `previewSupported`. GET metadata và `/download` cần JWT. File nằm ở volume riêng `/app/private-documents`, không có URL tĩnh công khai.
- Admin create/update Merchant nhận `businessLicenseDocumentId`, `contractDocumentId`, `slaDocumentId`. Null/bỏ qua giữ nguyên; gỡ dùng `removeBusinessLicenseDocument`, `removeContractDocument`, `removeSlaDocument`. Không vừa gán vừa gỡ một loại.
- Merchant chỉ đề xuất `businessLicenseDocumentId`/`removeBusinessLicenseDocument` qua `/merchant-profile/update-requests`; hợp đồng/SLA do Admin quản lý, Merchant chỉ xem/tải.
- Đề xuất giữ file ngay khi gửi, kể cả sau đó bị từ chối. File đã tham chiếu được giữ để đọc lịch sử, không xóa vật lý khi thay/gỡ. Bản nháp chưa gán hết hạn sau 24 giờ; worker quét 30 phút, tối đa 100/lượt; Admin có `/files/documents/cleanup`.
- Các trường giấy phép/SLA dạng chuỗi cũ vẫn đọc được; không tự chuyển đường dẫn cũ thành file riêng tư. DTO hồ sơ có danh sách `documents` và ID từng loại.

## Cấu hình và hiệu lực chính sách

- GET `/system-setting` có `version`, `isPersisted`, `source`, `serverNow`, `updatedBy`. PUT các nhóm cần đủ trường cấu hình; thiếu không biến thành 0. Mức 0 hợp lệ vẫn được chấp nhận.
- PUT `/sla`, `/commission`, `/penalty` nhận `expectedVersion`, `operationId`. Toàn singleton dùng chung khóa; hai Admin cùng phiên bản chỉ một yêu cầu ghi được.
- `/system-setting/operations/{id}` và POST `/system-setting/history/filter` phục vụ lookup và audit append-only (action `UpdateSla`, `UpdateCommission`, `UpdatePenalty`).
- Đơn mới lưu snapshot SLA/phí hủy/phí locker/kho lúc tạo. Đổi cấu hình không hồi tố các đơn đó. Đơn legacy được chốt baseline tại migration, có `operationalPolicyBasis=migration-baseline` và thời điểm chốt; không giả khôi phục được mức lịch sử. Hạn gửi có sẵn, giao dịch và phí đã chốt được giữ.
- Cảnh báo Admin/Merchant, nhắc nhở, gửi/lấy/trả/nhận và calculator dùng cùng căn cứ đơn. Hoa hồng mặc định chỉ áp dụng Merchant mới bỏ trống; không sửa tỷ lệ Merchant/đơn đã chốt.
- Bồi thường khi xử lý khiếu nại dùng cấu hình tại quyết định, nhận `expectedSettingsVersion` từ GET cấu hình và lưu `%`/version đã áp dụng.

## Finance, đối soát và hoàn tiền

1. POST `/finance/settlements/quote` với Merchant và Unix giây `fromDate/toDate`; kỳ phải đã kết thúc. Kết quả có tổng thu/hoàn/hoa hồng/thực nhận, dòng theo đơn, `quoteToken`, `canCreate`, `blockingCode`.
2. POST `/finance/settlements` gửi cùng kỳ và `quoteToken`, thêm `operationId`. Kiểm tra lại số liệu, khóa theo Merchant chống kỳ chồng lấn. Biên UTC **gồm cả hai đầu**: hai kỳ dùng chung một mốc bị coi là chồng lấn. Kỳ thiếu tỷ lệ snapshot bị chặn để đối chiếu, không tự suy ra tỷ lệ hiện tại. Quyết định nhóm ngày 09/11 bổ sung CarryForward cho kỳ âm và NoPayout cho kỳ 0; xem AGREED_BUSINESS_RULES_20261109.md.
3. GET `/finance/settlements/{id}`, `/orders`, `/lines?page=1&take=20`: Admin hoặc đúng Merchant. Kỳ mới lưu snapshot các dòng; phát sinh hoàn sau đó không thay số đã chốt. Kỳ cũ không có snapshot được đánh dấu `isSnapshot=false`.
4. POST `/{id}/confirm-paid` cần `reference`, `expectedVersion`, `operationId`. Replay cùng chứng từ không trả lần hai; chứng từ khác trả 409. Kỳ 0/âm không ghi chi trả giả.
5. Refund create có `amount` tùy chọn (null = phần tiền gốc còn lại). Review/confirm nhận `expectedVersion`, `operationId`; từ chối cần lý do. Hoàn một phần giữ giao dịch/đơn Paid; chỉ khi hoàn hết phần có thể hoàn mới Refunded. Tổng đã hoàn không vượt tiền gốc trừ phí hủy giữ lại. Đơn hủy tiếp tục tạo yêu cầu hoàn tự động theo luồng riêng.
6. POST `/payment/refund-requests/filter`: lọc tại DB và phân trang. Lookup review/confirm thuộc module `Finance`. Thông báo duyệt/từ chối/đã hoàn ghi cùng transaction.
7. Thu ở kỳ A, hoàn kỳ B: khoản hoàn và phần đảo hoa hồng ghi ở B, có thể âm; không chặn số hoàn bằng tổng thu riêng trong B. Giao dịch mới lưu quan hệ Merchant/tỷ lệ; dữ liệu legacy được đánh dấu baseline, tỷ lệ thiếu giữ null. Báo cáo Merchant dùng cùng snapshot.
8. CSV có thời điểm tạo mới, timezone, VND và escape chuỗi chống công thức Excel; đây là bản xuất tại thời điểm export, không phải bản đóng băng từ màn hình trước đó. Doanh thu chỉ phủ giao dịch đã ghi và refund xác nhận; Cash legacy không có giao dịch không được cộng giả.

## Locker, dịch vụ, khuyến mãi và khiếu nại

- Locker: create/update/delete/box-status/status có khóa và audit; giữ ô/giao nhận/xóa cùng khóa locker trước khi đổi ô. Không giải phóng đồ đơn hủy để xóa trạm. `PUT /locker/{id}/status` nhận trạng thái đích. Lịch sử usage lọc/scope/count/page tại PostgreSQL, giữ `usageId=orderId:1|2`, không tải toàn lịch sử vào RAM.
- Dịch vụ Admin: DTO list/detail có ảnh và version; create/update kiểm tra ownership ảnh mới, null giữ ảnh, [] gỡ. CRUD/status và duyệt đề xuất đồng bộ với tạo đơn qua khóa catalog; chặn sửa dịch vụ đã xóa mềm. Duyệt đề xuất nhận version của đề xuất, không phải version nội dung dịch vụ.
- Khuyến mãi: `PUT /promotion/{id}/status` nhận `isActive` đích. Route toggle cũ trả 400 hướng dẫn chuyển route để tránh retry đảo trạng thái. Update/delete dùng khóa dòng cùng consume/release; tạo đơn đọc lại giá trị dưới khóa trước tính giảm. Đã từng có lịch sử usage thì không xóa, kể cả usage đã revert. Thống kê tổng/ngày tại DB.
- Summary Admin: GET `/wash-service/summary`, `/promotion/summary`, `/incident/summary`.
- Khiếu nại: version tăng khi khách sửa/rút hoặc thêm message; Admin resolve/reject/confirm/message cần expectedVersion. Resolve cần thêm expectedSettingsVersion; lưu tỷ lệ và version làm căn cứ. Chứng từ bồi thường khác bản đã trả bị chặn. GET `/incident/{id}/messages/page` phân trang ổn định. List/detail enrich liên hệ hiện tại theo batch, chỉ Admin thấy email/phone/avatar/ảnh Merchant.
- `POST /incident/filter` nhận thêm `orderId`, `customerId`, `compensationStatus`; sort whitelist `CreatedOn|CreatedAt|Status|Severity|CompensationAmount|Id`, nhiều tiêu chí và Id phụ. Field không được hỗ trợ trả 400. Tạo/sửa/rút khiếu nại ghi audit trong transaction nghiệp vụ.
- Dòng snapshot đối soát mới có `paymentTransactionIds`, `refundRequestIds` để lần về nguồn. Snapshot cũ thiếu trường trả mảng rỗng; không sửa lịch sử để bổ sung dữ liệu suy ra sau này. Revenue DTO có `serviceRevenueBasis` giải thích subtotal chưa phân bổ.
- Merchant filter: sort whitelist BusinessName/CreatedAt/JoinedAt/Rating/Status/Id, hỗ trợ nhiều tiêu chí, Id làm thứ tự phụ; FromDate/ToDate lọc CreatedAt. `publicRatingCount` tách cửa hàng chưa có đánh giá khỏi điểm mặc định. `totalRevenueBasis` mô tả giá trị đơn Paid, không gọi đó là lợi nhuận/thực nhận.

## Ảnh/video và kiểm thử nội bộ

- Upload mới ghi registry. Bản nháp chưa gắn bản ghi hết hạn sau 24 giờ; worker hoặc POST Admin `/files/media/cleanup` dọn tối đa 100/lượt. Gắn file và đánh dấu tham chiếu chung transaction/khóa với cleanup; file hết hạn trả 400 UploadExpired và rollback bản ghi. File đã từng gắn hồ sơ/đề xuất/minh chứng không bị dọn; legacy không có registry không tự xóa.
- Auth tắt log tự động tham số để không in mật khẩu/token; audit nghiệp vụ được lọc riêng vẫn giữ.
- `tests/smoke/regression.compose.yml` là override **chỉ để test tự động**: Mock thanh toán và tắt SMTP. Sau test chạy compose chính để khôi phục sandbox VNPay/MoMo/SMTP từ `.env`; không sửa `.env` và không gọi chuyển tiền thật trong các smoke test.
- Kết quả thực tế từng script và các giới hạn còn lại được ghi ở checklist; có file test không đồng nghĩa đã PASS.
- Chốt đợt: build thành công, 6 migration mới đã áp dụng, EF model sạch, Swagger HTTP 200; compose chính đã được khôi phục với Provider Live. Test tự động không thực hiện lại giao dịch trên cổng sandbox chính thức. Các API ghi mới cần FE gửi version/key đúng hợp đồng trước khi dùng.

## Bổ sung filter, lịch sử voucher và phân trang — 09/11/2026

`POST /promotion/filter` hỗ trợ `sortBy` tối đa 8 tiêu chí, Field không phân biệt hoa/thường: `Code`, `CreatedOn` (alias `CreatedAt`), `StartAt`, `EndAt`, `UsageLimit`, `UsedCount`, `Id`. Mặc định CreatedOn giảm dần, Code tăng dần; luôn thêm Id phụ. UsageLimit null theo thứ tự PostgreSQL: cuối khi tăng, đầu khi giảm.

```json
{
  "page": 1,
  "take": 20,
  "sortBy": [
    { "field": "UsedCount", "isDescending": true },
    { "field": "Code", "isDescending": false }
  ]
}
```

`POST /promotion/{id}/usages/filter` chỉ Admin: `orderId`, `customerId`, `isReverted`, `keyword` mã đơn/tên khách hiện tại, `fromDate`/`toDate` Unix giây theo UsedAt, page/take. Sort `UsedAt`, `RevertedAt`, `DiscountAmount`, `OrderAmount`, `Id`; mặc định UsedAt giảm. Response list có mã đơn, khách, tiền đơn/giảm, thời điểm dùng và hoàn lượt. `revertReason` null vì dữ liệu cũ chưa lưu lý do; không suy ra từ trạng thái hiện tại. Ngày lọc tính cả hai đầu, không phải ngày hoàn lượt. Voucher xóa mềm/không có trả 404. Tên khách là thông tin hiện tại, không phải snapshot khi dùng mã.

`POST /finance/settlements/filter`: `fromDate`/`toDate` lọc kỳ **giao nhau**, `paidFromDate`/`paidToDate` lọc thời điểm đã chi trả (các kỳ chưa trả không khớp nếu có bộ lọc ngày trả). Mỗi cặp Unix giây gồm hai đầu, 0 = không giới hạn; sai khoảng trả 400. Keyword theo tên Merchant/chứng từ. Sort `PeriodFrom`, `PeriodTo`, `PaidAt`, `CreatedOn`, `MerchantName`, `PayoutAmount`, `Status`, `Id`, tối đa 8 tiêu chí. Merchant không thể thay scope bằng merchantId trong request.

`GET /finance/settlements/{id}/lines?page=1&take=20`: snapshot mới count/page ngay tại PostgreSQL theo thứ tự dòng đã chốt, take tối đa 100; không gửi toàn snapshot về app. Kỳ legacy không có snapshot vẫn fallback sang breakdown tính lại có giới hạn.

Report/CSV và nguồn quote/breakdown có giới hạn đồng bộ **25.000 dòng mỗi nguồn** giao dịch thu, hoàn; báo cáo có thêm giới hạn 25.000 dòng dịch vụ. Vượt giới hạn trả HTTP 413, `meta.code=ReportTooLarge`, `maxRowsPerSource=25000`: thu hẹp kỳ hoặc chọn Merchant. Không trả bản CSV/báo cáo bị cắt thiếu. Export bất đồng bộ chưa có. Không thay đổi cấu hình thanh toán sandbox/SMTP cho các kiểm thử mới này.
