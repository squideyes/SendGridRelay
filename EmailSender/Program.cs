using System.Net.Mail;
using System.Text;

var client = new SmtpClient("localhost", 11311);

var message = new MailMessage()
{
    Subject = "Subject Text",
    From = new MailAddress("from@test.com"),
    Body = "It Works!"
};

message.To.Add("to@test.com");

var stream = new MemoryStream(
    Encoding.UTF8.GetBytes("Body Text"));

var attachment = new Attachment(
    stream, "Text.txt", "text/plain");

message.Attachments.Add(attachment);

client.Send(message);
