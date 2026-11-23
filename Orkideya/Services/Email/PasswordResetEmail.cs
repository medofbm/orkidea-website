using System.Net;

namespace Orkideya.Services.Email;

/// <summary>Arabic RTL password-reset email (inline CSS, brand colours).</summary>
public static class PasswordResetEmail
{
    public const string Subject = "تغيير كلمة المرور - أوركيديا";

    public static string BuildLink(string publicBaseUrl, string email, string encodedToken) =>
        publicBaseUrl.TrimEnd('/') + "/account/reset-password?email=" + Uri.EscapeDataString(email) + "&token=" + encodedToken;

    public static string BuildHtml(string link)
    {
        var href = WebUtility.HtmlEncode(link);
        return $"""
<!DOCTYPE html>
<html lang="ar" dir="rtl"><head><meta charset="utf-8"></head>
<body style="margin:0;padding:0;background:#F6F0EA;font-family:Tahoma,Arial,sans-serif;direction:rtl;text-align:right;">
<div style="max-width:520px;margin:24px auto;background:#ffffff;border-radius:12px;overflow:hidden;">
<div style="background:#4E1258;color:#ffffff;padding:20px 24px;font-size:22px;font-weight:bold;">أوركيديا</div>
<div style="padding:24px;color:#2A1530;font-size:16px;line-height:1.8;">
<p style="margin:0 0 12px;">وصلنا طلب لتغيير كلمة المرور لحسابك.</p>
<p style="margin:0 0 20px;">اضغطي على الزر لاختيار كلمة مرور جديدة. الرابط صالح لمدة ساعتين فقط.</p>
<p style="text-align:center;margin:0 0 24px;"><a href="{href}" style="display:inline-block;background:#751D82;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:bold;">تغيير كلمة المرور</a></p>
<p style="margin:0;color:#6B5A72;font-size:14px;">إذا ما طلبتيش، تجاهلي الرسالة.</p>
</div></div></body></html>
""";
    }
}
