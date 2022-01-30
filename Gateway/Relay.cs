using MimeKit;
using SendGrid;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System.Buffers;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Gateway
{
    internal class Relay : MessageStore
    {
        private readonly ILogger logger;
        private readonly string apiKey;

        public Relay(ILogger logger, string apiKey)
        {
            this.logger = logger;
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

            var sgm = mm.ToSendGridMessage();

            var response = await client.SendEmailAsync(sgm, cancellationToken);

            // TODO: better message
            logger.LogInformation("Resent...");

            return SmtpResponse.Ok;
        }
    }
}
