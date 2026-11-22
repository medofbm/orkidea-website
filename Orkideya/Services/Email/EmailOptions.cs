namespace Orkideya.Services.Email;

/// <summary>SMTP settings, bound from the "Email" configuration section (environment variables Email__Smtp__Host, ...).</summary>
public sealed class EmailOptions
{
    public SmtpOptions Smtp { get; set; } = new();
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "";
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

/// <summary>Public site settings, bound from the "App" section (App__PublicBaseUrl).</summary>
public sealed class AppOptions
{
    public string PublicBaseUrl { get; set; } = "";
}
