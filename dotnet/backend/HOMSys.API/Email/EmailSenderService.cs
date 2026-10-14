using System.Net;
using System.Threading.Channels;
using Azure;
using Azure.Communication.Email;
using HOMSys.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace HOMSys.API.Email;

/// <summary>"Email" section of appsettings / Azure App Service settings (Email__ConnectionString …).</summary>
public class EmailOptions
{
    /// <summary>Master switch; off = notifications stay in-app (bell) only.</summary>
    public bool Enabled { get; set; }
    /// <summary>Azure Communication Services connection string (endpoint=…;accesskey=…). A secret — never in the repo.</summary>
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>Sender, a MailFrom address of the domain connected to the ACS resource, e.g. DoNotReply@xxxx.azurecomm.net.</summary>
    public string FromAddress { get; set; } = string.Empty;
    /// <summary>HOMSys site address for the email's "Open in HOMSys" link.</summary>
    public string WebUrl { get; set; } = "https://homsys.ramfoods.com";

    public bool IsConfigured => Enabled && ConnectionString.Length > 0 && FromAddress.Length > 0;
}

/// <summary>Bounded in-memory outbox (IEmailQueue). Drops everything while email is not configured.</summary>
public class EmailQueue(IOptions<EmailOptions> options) : IEmailQueue
{
    private readonly Channel<NotificationEmail> channel = Channel.CreateBounded<NotificationEmail>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<NotificationEmail> Reader => channel.Reader;

    public void Enqueue(NotificationEmail message)
    {
        if (!options.Value.IsConfigured) return;
        if (!message.To.Contains('@') || message.To.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return;
        channel.Writer.TryWrite(message);
    }
}

/// <summary>
/// Sends queued notification emails through Azure Communication Services Email, one at a time.
/// A failed send is logged and skipped — the in-app notification was already saved.
/// </summary>
public class EmailSenderService(EmailQueue queue, IOptions<EmailOptions> options, ILogger<EmailSenderService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.IsConfigured)
        {
            log.LogInformation("Email notifications are off (Email:Enabled / ConnectionString / FromAddress not set).");
            return;
        }

        var client = new EmailClient(o.ConnectionString);
        await foreach (var m in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var content = new EmailContent($"HOMSys: {m.Title}")
                {
                    PlainText = $"{m.Title}\n\n{m.Message}" + (m.LinkPath is null ? "" : $"\n\nOpen in HOMSys: {Url(o, m.LinkPath)}"),
                    Html = Html(o, m)
                };
                var email = new EmailMessage(o.FromAddress, new EmailRecipients([new EmailAddress(m.To, m.ToName)]), content);
                // Started: ACS queues it; we don't wait for final delivery.
                await client.SendAsync(WaitUntil.Started, email, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Email to {To} ({Title}) failed.", m.To, m.Title);
            }
        }
    }

    private static string Url(EmailOptions o, string path) => o.WebUrl.TrimEnd('/') + path;

    private static string Html(EmailOptions o, NotificationEmail m)
    {
        static string e(string s) => WebUtility.HtmlEncode(s);
        var button = m.LinkPath is null ? "" :
            $"""<p style="margin:24px 0"><a href="{e(Url(o, m.LinkPath))}" style="background:#800000;color:#fff;padding:10px 18px;border-radius:4px;text-decoration:none;font-weight:600">Open in HOMSys</a></p>""";
        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f1f1f;max-width:560px">
              <div style="border-top:4px solid #800000;padding-top:12px;color:#800000;font-weight:700">HOMSys</div>
              <h2 style="font-size:18px;margin:16px 0 8px">{e(m.Title)}</h2>
              <p style="margin:0">Hi {e(m.ToName)},</p>
              <p>{e(m.Message)}</p>
              {button}
              <p style="color:#888;font-size:12px">Automatic message from HOMSys — please do not reply.</p>
            </div>
            """;
    }
}
