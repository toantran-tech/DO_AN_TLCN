using System;
using System.Globalization;
using System.Linq;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Shared.Enums;
using PdfSettings = global::QuestPDF.Settings;

namespace WashGo.Application.Features.WashOrderApplication.Receipts
{
    /// <summary>Biên lai PDF khổ A5. Font Be Vietnam Pro (SIL OFL) nhúng trong assembly để không phụ thuộc font của máy chủ.</summary>
    public static class ReceiptPdfRenderer
    {
        private const string FontFamily = "Be Vietnam Pro";
        private static readonly object Gate = new();
        private static bool _ready;
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

        public static byte[] Render(OrderReceiptDto r)
        {
            EnsureReady();
            return Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("WashGo").Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                    col.Item().Text("BIÊN LAI THANH TOÁN").Bold().FontSize(13);
                    col.Item().Text($"Số: {r.ReceiptNumber}    Ngày xuất: {Time(r.IssuedAt)}");
                    col.Item().Text($"Đơn hàng: #{r.OrderCode}" + (r.MerchantOrderCode != null ? $" (mã tiệm {r.MerchantOrderCode})" : ""));
                    col.Item().PaddingTop(6).LineHorizontal(0.5f);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(6);
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
                            h.Cell().BorderBottom(0.5f).Text("Dịch vụ").Bold();
                            h.Cell().BorderBottom(0.5f).AlignRight().Text("SL / kg").Bold();
                            h.Cell().BorderBottom(0.5f).AlignRight().Text("Đơn giá").Bold();
                            h.Cell().BorderBottom(0.5f).AlignRight().Text("Thành tiền").Bold();
                        });
                        foreach (var line in r.Lines)
                        {
                            table.Cell().PaddingVertical(2).Text(line.Description);
                            table.Cell().PaddingVertical(2).AlignRight().Text(line.WeightKg.HasValue
                                ? $"{line.WeightKg.Value.ToString("0.##", Vi)} kg" : line.Quantity.ToString(Vi));
                            table.Cell().PaddingVertical(2).AlignRight().Text(Money(line.UnitPrice));
                            table.Cell().PaddingVertical(2).AlignRight().Text(Money(line.Amount));
                        }
                    });

                    col.Item().AlignRight().Column(c =>
                    {
                        void Row(string label, string value, bool bold = false)
                        {
                            var text = c.Item().Text($"{label}: {value}");
                            if (bold) text.Bold();
                        }
                        Row("Tiền dịch vụ", Money(r.ServicesAmount));
                        if (r.WeightSurchargeAmount > 0) Row("Phụ phí vượt cân", Money(r.WeightSurchargeAmount));
                        if (r.DiscountAmount > 0) Row($"Giảm giá{(r.PromotionCode != null ? $" ({r.PromotionCode})" : "")}", "-" + Money(r.DiscountAmount));
                        Row("Tiền giặt", Money(r.LaundryAmount));
                        foreach (var fee in r.Fees) Row(fee.Description, Money(fee.Amount));
                        Row("TỔNG ĐÃ THANH TOÁN", Money(r.TotalPaid), true);
                    });

                    if (r.Payments.Count > 0)
                    {
                        col.Item().Text("Giao dịch").Bold();
                        foreach (var p in r.Payments)
                            col.Item().Text($"{MethodName(p.Method)} · {Money(p.Amount)} · {Time(p.PaidAt)} · {p.ProviderTransactionId ?? p.Reference}").FontSize(8);
                    }
                });

                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f);
                    col.Item().PaddingTop(4).Text(r.Note).FontSize(7);
                });
            })).GeneratePdf();
        }
    }
}
