namespace SaPosts.Web.Contact;

/// <summary>
/// A message sent through the contact form. It is stored first and emailed afterwards by a
/// background service, so this table doubles as the outbox. Purged after the retention period.
/// </summary>
public sealed class ContactMessage
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public bool IsRead { get; set; }

    public DateTime? EmailSentAt { get; set; }

    public int EmailAttempts { get; set; }

    public string? LastEmailError { get; set; }
}
