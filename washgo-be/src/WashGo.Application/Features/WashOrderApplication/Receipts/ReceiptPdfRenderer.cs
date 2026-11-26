using System;
using System.Globalization;
using System.IO;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Shared.Enums;
using PdfSettings = global::QuestPDF.Settings;

namespace WashGo.Application.Features.WashOrderApplication.Receipts
{
    /// <summary>Biên lai PDF khổ A4. Font Be Vietnam Pro (SIL OFL) nhúng trong assembly để không phụ thuộc font của máy chủ.</summary>
    public static class ReceiptPdfRenderer
    {
        private const string FontFamily = "Be Vietnam Pro";
        private static readonly object Gate = new();
        private static bool _ready;
        private static byte[] _logo = [];
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        private static void EnsureReady()
        {
            lock (Gate)
            {
                if (_ready) return;
                PdfSettings.License = LicenseType.Community;
                PdfSettings.UseEnvironmentFonts = false;
                var assembly = typeof(ReceiptPdfRenderer).Assembly;
                foreach (var name in new[] { "WashGo.ReceiptFonts.Regular.ttf", "WashGo.ReceiptFonts.Bold.ttf" })
                {
                    using var stream = assembly.GetManifestResourceStream(name)
                        ?? throw new InvalidOperationException($"Missing embedded font {name}.");
                    FontManager.RegisterFont(stream);
                }
                using var logoStream = assembly.GetManifestResourceStream("WashGo.EmailAssets.logo.png")
                    ?? throw new InvalidOperationException("Missing embedded WashGo logo.");
                using var logo = new MemoryStream();
                logoStream.CopyTo(logo);
                _logo = logo.ToArray();
                _ready = true;
            }
        }

        private static string Money(decimal value) => value.ToString("N0", Vi) + " đ";
        private static string Time(DateTime? utc) => utc.HasValue ? (utc.Value + VietnamOffset).ToString("HH:mm dd/MM/yyyy") : "";

        private static string MethodName(PaymentMethod method) => method switch
        {
            PaymentMethod.VnPay => "VNPay",
            PaymentMethod.Momo => "MoMo",
            PaymentMethod.Cash => "Tiền mặt",
            _ => method.ToString()
        };

        private static string DisplayReference(string orderCode, ReceiptPaymentDto payment)
        {
            if (!string.IsNullOrWhiteSpace(payment.ProviderTransactionId))
                return payment.ProviderTransactionId;

            var reference = payment.Reference;
            var separator = reference.LastIndexOf('-');
            if (separator < 0)
                return reference;
            var suffix = reference[(separator + 1)..];
            if (!Guid.TryParseExact(suffix, "N", out _) &&
                !(payment.Method == PaymentMethod.Momo && suffix.Length == 16 && ulong.TryParse(suffix, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)))
                return reference;

            var prefix = reference[..separator];
            return prefix == $"WG-PAY-{orderCode}" || prefix == $"WG-TOTAL-{orderCode}" ||
                prefix == $"WG-FEE-{orderCode}" || prefix == $"WG-CANCEL-{orderCode}" ? prefix : reference;
        }

        private static string DisplayQuantity(ReceiptLineDto line, bool hasActualWeight) => line.Unit switch
        {
            "item" => $"{line.Quantity} cái",
            "pair" => $"{line.Quantity} đôi",
            "kg" => $"{line.Quantity} gói",
            _ when !hasActualWeight && line.WeightKg is { } kg => $"{kg.ToString("0.##", Vi)} kg",
            _ => line.Quantity.ToString(Vi)
        };

        public static byte[] Render(OrderReceiptDto r)
        {
            EnsureReady();
            return Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(19, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(10));

                page.Header().BorderBottom(1).BorderColor(Colors.Blue.Lighten3)
                    .Background(Colors.Blue.Lighten5).Padding(14).Column(col =>
                {
                    col.Spacing(14);
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(30).Image(_logo).FitArea();
                        row.RelativeItem().PaddingLeft(9).AlignMiddle().Text("WashGo").Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                    });
                    col.Item().Text("BIÊN LAI THANH TOÁN").Bold().FontSize(19);
                    col.Item().Text($"Số biên lai: {r.ReceiptNumber}");
                    col.Item().Text($"Ngày phát hành: {Time(r.IssuedAt)}");
                    col.Item().Text($"Đơn hàng: #{r.OrderCode}" + (r.MerchantOrderCode != null ? $" (mã tiệm {r.MerchantOrderCode})" : ""));
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(14);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Bên bán").Bold();
                            c.Item().Text(r.Seller.Name);
                            if (!string.IsNullOrEmpty(r.Seller.TaxCode)) c.Item().Text($"MST: {r.Seller.TaxCode}");
                            if (!string.IsNullOrEmpty(r.Seller.Address)) c.Item().Text(r.Seller.Address);
                            if (!string.IsNullOrEmpty(r.Seller.Phone)) c.Item().Text($"ĐT: {r.Seller.Phone}");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Bên mua").Bold();
                            c.Item().Text(r.Buyer.Name);
                            if (!string.IsNullOrEmpty(r.Buyer.Phone)) c.Item().Text($"ĐT: {r.Buyer.Phone}");
                            if (!string.IsNullOrEmpty(r.Buyer.Email)) c.Item().Text(r.Buyer.Email);
                        });
                    });

                    if (r.ActualWeightKg is { } kg)
                        col.Item().Text($"Khối lượng thực tế: {kg.ToString("0.##", Vi)} kg");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Blue.Lighten5).Padding(7).Text("Dịch vụ").Bold();
                            h.Cell().Background(Colors.Blue.Lighten5).Padding(7).AlignRight().Text("SL / kg").Bold();
                            h.Cell().Background(Colors.Blue.Lighten5).Padding(7).AlignRight().Text("Đơn giá").Bold();
                            h.Cell().Background(Colors.Blue.Lighten5).Padding(7).AlignRight().Text("Thành tiền").Bold();
                        });
                        foreach (var line in r.Lines)
                        {
                            table.Cell().ShowEntire().Padding(7).Text(line.Description);
                            table.Cell().ShowEntire().Padding(7).AlignRight().Text(DisplayQuantity(line, r.ActualWeightKg.HasValue));
                            table.Cell().ShowEntire().Padding(7).AlignRight().Text(Money(line.UnitPrice));
                            table.Cell().ShowEntire().Padding(7).AlignRight().Text(Money(line.Amount));
                        }
                    });

                    col.Item().AlignRight().Column(c =>
                    {
                        void Row(string label, string value) => c.Item().Text($"{label}: {value}");
                        Row("Tiền dịch vụ", Money(r.ServicesAmount));
                        if (r.WeightSurchargeAmount > 0) Row("Phụ phí vượt cân", Money(r.WeightSurchargeAmount));
                        if (r.DiscountAmount > 0) Row($"Giảm giá{(r.PromotionCode != null ? $" ({r.PromotionCode})" : "")}", "-" + Money(r.DiscountAmount));
                        if (r.LaundryAmount != r.TotalPaid && r.LaundryAmount > 0) Row("Tiền giặt thực thu", Money(r.LaundryAmount));
                        if (r.RedeemedPoints > 0) Row("Điểm đã dùng (WashGo tài trợ)", r.RedeemedPoints.ToString("N0") + " điểm");
                        foreach (var fee in r.Fees) Row(fee.Description, Money(fee.Amount));
                        c.Item().PaddingTop(5).Text($"TỔNG ĐÃ THANH TOÁN  {Money(r.TotalPaid)}")
                            .Bold().FontSize(15).FontColor(Colors.Blue.Darken2);
                    });

                    if (r.Payments.Count > 0)
                    {
                        col.Item().Text("Giao dịch").Bold();
                        foreach (var payment in r.Payments)
                        {
                            col.Item().ShowEntire().Background(Colors.Grey.Lighten4).Padding(8).Column(paymentDetails =>
                            {
                                paymentDetails.Spacing(3);
                                paymentDetails.Item().Text($"Phương thức: {MethodName(payment.Method)}");
                                if (payment.PaidAt.HasValue)
                                    paymentDetails.Item().Text($"Thời điểm: {Time(payment.PaidAt)}");
                                paymentDetails.Item().Text($"Số tiền: {Money(payment.Amount)}");
                                paymentDetails.Item().Text($"Mã đối soát: {DisplayReference(r.OrderCode, payment)}");
                            });
                        }
                    }
                });
            })).GeneratePdf();
        }
    }
}
