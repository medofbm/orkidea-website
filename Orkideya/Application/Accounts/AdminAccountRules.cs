namespace Orkideya.Application.Accounts;

/// <summary>
/// Who may manage administrators. The super admin (the store owner) is the account whose email equals the
/// configuration value "Admin:SuperAdminEmail" (environment variable Admin__SuperAdminEmail). When the value is empty,
/// nobody can add or remove administrators from the admin pages.
/// </summary>
public static class AdminAccountRules
{
    public const string AdminRole = "Admin";
    public const string SuperAdminEmailKey = "Admin:SuperAdminEmail";

    public const string NotSuperAdminMessage = "إضافة المديرين وحذفهم متاحة للمدير الرئيسي فقط.";
    public const string SuperAdminProtectedMessage = "المدير الرئيسي لا يمكن حذفه.";
    public const string SelfRemovalMessage = "لا يمكنك حذف نفسك من المديرين.";
    public const string NotAnAdminMessage = "هذا الحساب ليس مديرًا.";

    /// <summary>True when <paramref name="email"/> is the configured super admin email (case-insensitive, trimmed).</summary>
    public static bool IsSuperAdminEmail(string? email, string? configuredEmail)
    {
        var configured = configuredEmail?.Trim();
        var value = email?.Trim();
        return !string.IsNullOrEmpty(configured) && !string.IsNullOrEmpty(value) &&
            string.Equals(value, configured, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Why the caller may not remove the target admin, or null when removal is allowed. The caller must already be
    /// the super admin; this checks the target only: the super admin is never removed, nobody removes themselves.
    /// </summary>
    public static string? RemovalRefusal(string callerUserId, string targetUserId, string? targetEmail,
        bool targetIsAdmin, string? configuredEmail)
    {
        if (string.Equals(callerUserId, targetUserId, StringComparison.Ordinal)) return SelfRemovalMessage;
        if (IsSuperAdminEmail(targetEmail, configuredEmail)) return SuperAdminProtectedMessage;
        if (!targetIsAdmin) return NotAnAdminMessage;
        return null;
    }
}
