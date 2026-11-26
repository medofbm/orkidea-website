using System.ComponentModel.DataAnnotations;

namespace Orkideya.Models;

/// <summary>
/// Simple account activity for the admin «الزبائن» page: when the account was created and when it last signed in.
/// One row per user (UserId is the key and a foreign key to AspNetUsers, deleted with the user).
/// Accounts created before this table existed have no row, or a row with CreatedAtUtc = null (shown as «غير معروف»).
/// </summary>
public class AccountActivity
{
    [Key, MaxLength(450)]
    public string UserId { get; set; } = "";

    /// <summary>When the account was created. Null when the account is older than this table.</summary>
    public DateTime? CreatedAtUtc { get; set; }

    /// <summary>Last successful sign-in through the store (password or verification code).</summary>
    public DateTime? LastSignInAtUtc { get; set; }

    /// <summary>Successful sign-ins counted since this table was added.</summary>
    public int SignInCount { get; set; }
}
