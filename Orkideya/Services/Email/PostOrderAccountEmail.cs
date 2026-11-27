using System.Net;

namespace Orkideya.Services.Email;

/// <summary>Arabic RTL emails for "create an account after ordering" (same look as the password-reset email).</summary>
public static class PostOrderAccountEmail
{
    public const string SetPasswordSubject = "حسابك في أوركيديا - اختاري كلمة السر";
    public const string ExistingAccountSubject = "عندك حساب في أوركيديا";

    public static string BuildSetPasswordHtml(string link) => Wrap(
        "شكرًا على طلبك! 💜 جهزنا لك حساب تتابعي منه طلبك وطلباتك الجاية.",
        "اضغطي على الزر واختاري كلمة السر. الرابط صالح لمدة ساعتين فقط.",
        link, "اختاري كلمة السر",
        "لو ما طلبتيش، تجاهلي الرسالة.");

    public static string BuildExistingAccountHtml(string loginUrl) => Wrap(
        "عندك حساب عندنا بالفعل بنفس هذا الإيميل.",
        "سجلي دخول وتلقي طلباتك في مكان واحد. لو نسيتي كلمة السر، اختاري «نسيتي كلمة المرور؟» في صفحة الدخول.",
        loginUrl, "تسجيل الدخول",
        "لو ما طلبتيش، تجاهلي الرسالة.");

    private static string Wrap(string intro, string body, string link, string button, string footer)
    {
        var href = WebUtility.HtmlEncode(link);
        return $"""
<!DOCTYPE html>
<html lang="ar" dir="rtl"><head><meta charset="utf-8"></head>
<body style="margin:0;padding:0;background:#F6F0EA;font-family:Tahoma,Arial,sans-serif;direction:rtl;text-align:right;">
<div style="max-width:520px;margin:24px auto;background:#ffffff;border-radius:12px;overflow:hidden;">
<div style="background:#4E1258;color:#ffffff;padding:20px 24px;font-size:22px;font-weight:bold;">أوركيديا</div>
<div style="padding:24px;color:#2A1530;font-size:16px;line-height:1.8;">
<p style="margin:0 0 12px;">{intro}</p>
<p style="margin:0 0 20px;">{body}</p>
<p style="text-align:center;margin:0 0 24px;"><a href="{href}" style="display:inline-block;background:#751D82;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:bold;">{button}</a></p>
<p style="margin:0;color:#6B5A72;font-size:14px;">{footer}</p>
</div></div></body></html>
""";
    }
}
