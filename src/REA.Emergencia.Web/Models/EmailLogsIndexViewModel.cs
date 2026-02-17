namespace REA.Emergencia.Web.Models;

public sealed class EmailLogsIndexViewModel
{
    public IReadOnlyList<EmailLogItemViewModel> Logs { get; set; } = Array.Empty<EmailLogItemViewModel>();
}

public sealed class EmailLogItemViewModel
{
    public DateTime SentAtUtc { get; set; }
    public string Recipients { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}
