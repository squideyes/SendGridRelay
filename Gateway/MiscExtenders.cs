using MimeKit;
using SendGrid.Helpers.Mail;
using System.Text;
using static System.StringComparer;

namespace Gateway;

public static class MiscExtenders
{
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

    public static R AsFunc<T, R>(this T value, Func<T, R> func) => func(value);

    public static string StringOrNull(this string value) =>
        string.IsNullOrWhiteSpace(value) ? null! : value;

    public static EmailAddress ToEmailAddress(this InternetAddress address) =>
        new(address.ToString(), address.Name.StringOrNull());

    public static SendGridMessage ToSendGridMessage(this MimeMessage mm)
    {
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

        return sgm;
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

        return ((MimePart)entity).Content.Stream.AsBase64String();
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

    public static string AsBase64String(this Stream stream)
    {
        var text = new StreamReader(stream).ReadToEnd();

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    }
}