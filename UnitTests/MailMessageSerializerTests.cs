using FluentAssertions;
using Gateway;
using MimeKit;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace UnitTests;

public class MailMessageSerializerTests
{
    [Fact]
    public void MimeMessageToEmailMessage()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Plain Text"));

        var attachment = new MimePart("text", "plain")
        {
            Content = new MimeContent(stream, ContentEncoding.Default),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = "Test.txt"
        };

        var bodyBuilder = new BodyBuilder()
        {
            HtmlBody = "<b>HTML Body</b>",
            TextBody = "Plain Text"
        };

        bodyBuilder.Attachments.Add(attachment);

        var mm = new MimeMessage()
        {
            Subject = "Subject",
            Body = bodyBuilder.ToMessageBody()
        };

        void AddMailboxAddress(InternetAddressList addreses, string prefix) =>
            addreses.Add(new MailboxAddress(prefix, $"{prefix}@test.com"));

        mm.From.Add(new MailboxAddress("from", "from@test.com"));

        AddMailboxAddress(mm.To, "to");
        AddMailboxAddress(mm.Cc, "cc");
        AddMailboxAddress(mm.Bcc, "bcc");
        AddMailboxAddress(mm.ReplyTo, "replyto");

        ////////////////////////////////////////////////////////

        var sgm = mm.ToSendGridMessage();

        sgm.Personalizations.Count.Should().Be(1);

        var p = sgm.Personalizations[0];

        void ValidateEmails(InternetAddressList source, List<EmailAddress> target)
        {
            source.Count.Should().Be(1);
            target.Count.Should().Be(1);

            target[0].Email.Should().Be(source[0].ToString());
            target[0].Name.Should().Be(source[0].Name);
        }

        sgm.From.Email.Should().Be(mm.From[0].ToString());
        sgm.From.Name.Should().Be(mm.From[0].Name);

        ValidateEmails(mm.To, p.Tos);
        ValidateEmails(mm.Cc, p.Ccs);
        ValidateEmails(mm.Bcc, p.Bccs);
        ValidateEmails(mm.ReplyTo, new List<EmailAddress> { sgm.ReplyTo });

        sgm.Subject.Should().Be(mm.Subject);

        sgm.HtmlContent.Should().Be(mm.HtmlBody);

        sgm.PlainTextContent.Should().Be(mm.TextBody);

        sgm.Attachments.Count.Should().Be(1);

        sgm.Attachments[0].Disposition.Should().Be(
            attachment.ContentDisposition.Disposition);

        sgm.Attachments[0].ContentId.Should().Be(
            attachment.ContentId);

        sgm.Attachments[0].Filename.Should().Be(
            attachment.FileName);

        sgm.Attachments[0].Type.Should().Be(
            attachment.ContentType.MimeType);

        attachment.Content.Stream.Position = 0;

        var content = attachment.Content.Stream.AsBase64String();

        sgm.Attachments[0].Content.Should().Be(content);

        foreach (var sgmHeader in sgm.Headers)
        {
            var mmHeader = mm.Headers.FirstOrDefault(
                h => h.Id.ToString() == sgmHeader.Key);

            if (mmHeader == null)
                throw new ArgumentOutOfRangeException(nameof(sgmHeader.Key));

            mmHeader.Value.Should().Be(sgmHeader.Value);
        }
    }
}