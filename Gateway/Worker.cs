using MimeKit;
using SendGrid;
using SendGrid.Helpers.Mail;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System.Buffers;
using static System.StringComparer;

namespace Gateway;

public class Worker : BackgroundService
{
    private class SampleMessageStore : MessageStore
    {
        private readonly string apiKey;

        public SampleMessageStore(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public override async Task<SmtpResponse> SaveAsync(
            ISessionContext context, IMessageTransaction transaction,
            ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
        {
            await using var stream = new MemoryStream();

            var position = buffer.GetPosition(0);

            while (buffer.TryGet(ref position, out var memory))
                await stream.WriteAsync(memory, cancellationToken);

            stream.Position = 0;

            var mm = await MimeMessage.LoadAsync(stream, cancellationToken);

            var client = new SendGridClient(apiKey);

            var sgm = new SendGridMessage()
            {
                From = mm.From.FirstOrDefault()?.ToEmailAddress(),
                Subject = mm.Subject,
                Attachments = GetAttachments(mm.Attachments),
                Headers = GetHeaders(mm.Headers),
                HtmlContent = mm.HtmlBody,
                PlainTextContent = mm.TextBody,
                ReplyTo = mm.ReplyTo.FirstOrDefault()?.ToEmailAddress(),
            };

            AddEmailAddresses(mm.To, (e, n) => sgm.AddTo(e, n));
            AddEmailAddresses(mm.Cc, (e, n) => sgm.AddCc(e, n));
            AddEmailAddresses(mm.Bcc, (e, n) => sgm.AddBcc(e, n));

            var response = await client.SendEmailAsync(sgm, cancellationToken);

            return SmtpResponse.Ok;
        }

        private static Dictionary<string, string> GetHeaders(HeaderList source)
        {
            var headers = new Dictionary<string, string>();

            foreach (var header in source)
            {
                if (!badHeaders.Contains(header.Field, OrdinalIgnoreCase))
                    headers.Add(header.Id.ToString(), header.Value);
            }

            return headers;
        }

        private static void AddEmailAddresses(
            InternetAddressList addresses, Action<string, string> addAddress)
        {
            foreach (var a in addresses)
                addAddress(a!.ToString()!, a.Name.StringOrNull());
        }

        private static string GetContent(MimeEntity? entity)
        {
            if (entity == null)
                return null!;

            var stream = new MemoryStream();

            entity.WriteTo(stream);

            return Convert.ToBase64String(stream.ToArray());
        }

        private static List<Attachment> GetAttachments(
            IEnumerable<MimeEntity> entities)
        {
            if (entities == null)
                return null!;

            var attachments = new List<Attachment>();

            foreach (var entity in entities)
            {
                var attachment = new Attachment()
                {
                    Content = GetContent(entity),
                    ContentId = entity.ContentId,
                    Disposition = entity.ContentDisposition.Disposition,
                    Filename = entity.ContentType.Name,
                    Type = entity.ContentType.MimeType
                };

                attachments.Add(attachment);
            }

            return attachments;
        }
    }

    private static readonly HashSet<string> badHeaders = new()
    {
        "x-sg-id",
        "x-sg-eid",
        "received",
        "dkim-signature",
        "content-type",
        "content-transfer-encoding",
        "to",
        "from",
        "subject",
        "reply-to",
        "cc",
        "bcc"
    };

    private readonly ILogger<Worker> logger;
    private readonly string host;
    private readonly int port;
    private readonly string apiKey;

    public Worker(ILogger<Worker> logger, IConfiguration config)
    {
        this.logger = logger;

        host = config["Values:Host"];
        port = int.Parse(config["Values:Port"]);
        apiKey = config["Values:SendGridApiKey"];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = new SmtpServerOptionsBuilder()
            .ServerName(host)
            .Port(port)
            .Build();

        var provider = new SmtpServer.ComponentModel.ServiceProvider();

        provider.Add(new SampleMessageStore(apiKey));

        var server = new SmtpServer.SmtpServer(options, provider);

        await server.StartAsync(stoppingToken);
    }
}