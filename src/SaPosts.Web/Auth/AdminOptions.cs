namespace SaPosts.Web.Auth;

/// <summary>
/// The single admin account. Required to create it on first start; when set on later starts,
/// the values replace the stored ones (the way to change or reset the password).
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string? Email { get; set; }

    public string? Password { get; set; }
}
