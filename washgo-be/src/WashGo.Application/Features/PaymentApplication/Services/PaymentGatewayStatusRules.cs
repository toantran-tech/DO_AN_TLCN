namespace WashGo.Application.Features.PaymentApplication.Services;

/// <summary>Unknown/processing/reversal results must not release a session for another charge.</summary>
public static class PaymentGatewayStatusRules
{
    // https://developers.momo.vn/v3/docs/payment/api/result-handling/resultcode/
    public static bool IsMomoFinalFailure(int? code) => code is
        1001 or 1002 or 1003 or 1004 or 1005 or 1006 or 1007 or 1017 or 1026;

    // https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html
    // 01 unfinished, 04 reversal, 05/06 refund and 07 suspected transactions need verification.
    public static bool IsVnPayFinalFailure(string response, string status) => status == "02" &&
        (response is "09" or "10" or "11" or "12" or "13" or "24" or "51" or "65" or "75" or "79");
}
