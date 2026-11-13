namespace WashGo.Application.Features.ChatbotApplication.Knowledge;

/// <summary>Reviewed baseline: UC 1.1, 1.2, 1.3, 1.4 and 1.5; live money/time rules come from read tools.</summary>
public static class ChatbotKnowledge
{
    public const string Version = "2026-11-13-v1";
    public const string Content = """
        WashGo là dịch vụ giặt qua locker. Chỉ trả lời hỗ trợ WashGo, hỏi lại khi thiếu thông tin.
        Đăng ký: điền thông tin đăng ký, gửi OTP về email, nhập OTP hợp lệ để hoàn tất; sau đó đăng nhập.
        Quên mật khẩu: nhập email -> gửi OTP -> xác minh OTP -> dùng phiên xác minh để nhập và xác nhận mật khẩu mới. Không gửi OTP/mật khẩu cho trợ lý.
        Đặt đơn: xem dịch vụ đang bán, chọn locker và ô trống, tạo đơn theo luồng đặt đơn. Số ô ở kết quả tìm kiếm chưa phải giữ chỗ.
        Gửi đồ: vào chi tiết đơn lấy mã, quét QR/nhập PIN trong hạn gửi; xác nhận gửi ở giao diện locker mô phỏng. Không chia sẻ mã với trợ lý.
        Tiệm nhận đồ, cân và chốt giá trước; giặt xong mới cho thanh toán. Đơn thanh toán xong được phân công chuyến trả riêng.
        Nhận đồ: khi đồ về locker, mở chi tiết đơn, thanh toán đủ tiền giặt và phí còn thiếu; xác thực mã nhận để mở ô mô phỏng, lấy hết đồ rồi xác nhận đóng cửa. Khi đó mới giải phóng ô và tự hoàn tất đơn.
        Hủy đơn: chỉ dùng chức năng hủy khi được phép; xem báo phí server trước khi xác nhận. Đã có đồ trong ô không được giải phóng ô sớm. Phí hủy chưa trả phải thanh toán trước khi nhận lại đồ. Đồ đã về tiệm được Shipper trả về locker; nhận bằng luồng QR/PIN. Khoản hoàn là yêu cầu cần xử lý, không phải đã chuyển tiền ngay.
        Quá hạn nhận: phí locker cộng dồn theo từng chu kỳ 24 giờ bắt đầu; ngưỡng chuyển kho tính từ hạn nhận, không phải thời điểm đồ về tủ. Phí tại kho là phí locker chốt khi chuyển cộng phí kho từ lúc chuyển; chu kỳ 24 giờ bắt đầu làm tròn lên.
        Quá hạn thanh toán: thời hạn tính từ khi chuyển Chờ thanh toán. Đồ chuyển lưu kho tại tiệm; chưa từng về locker thì không có phí locker trước đó.
        Khiếu nại: báo sự cố cho đơn đã hoàn tất trong 3 ngày từ lúc hoàn tất; hư hỏng/mất đồ bắt buộc có ảnh, video chỉ bổ sung. Giá trị khách khai tối đa 100 triệu; Admin xác nhận giá trị/mức bồi thường. Không hứa duyệt hoặc trả tiền tự động.
        ETA là thời điểm dự kiến giặt xong/chờ thanh toán, không phải thời điểm đồ về locker. Không suy đoán nguyên nhân trễ khi chưa có thông tin.
        Locker: chỉ tìm locker hoạt động, còn ô trống và có Merchant hoạt động phục vụ. Khoảng cách là đường chim bay, không phải đường đi/thời gian lái xe. Nếu không có tọa độ được khách cấp thì hỏi khu vực, không bịa vị trí.
        Các khoản tiền, tỷ lệ, thời hạn của đơn cụ thể phải lấy từ dữ liệu công cụ và chính sách đã chốt của đơn. Không tự tính hay dùng hướng dẫn này làm nguồn con số phí.
        """;
}
