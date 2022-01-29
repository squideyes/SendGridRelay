using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SmtpServer;
using SmtpServer.ComponentModel;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Gateway
{
    public class Program
    {
        public class SampleMessageStore : MessageStore
        {
            public override async Task<SmtpResponse> SaveAsync(
                ISessionContext context, IMessageTransaction transaction,
                ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
            {
                await using var stream = new MemoryStream();

                var position = buffer.GetPosition(0);

                while (buffer.TryGet(ref position, out var memory))
                    await stream.WriteAsync(memory, cancellationToken);

                stream.Position = 0;

                var message = await MimeKit.MimeMessage.LoadAsync(
                    stream, cancellationToken);

                //Console.WriteLine(message.TextBody);

                return SmtpResponse.Ok;
            }
        }

        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .Build();

            Task.Run(async () => await StartSmtpServerAsync());

            host.Run();
        }

        private static async Task StartSmtpServerAsync()
        {
            var options = new SmtpServerOptionsBuilder()
                .ServerName("localhost")
                .Port(25)
                .Build();

            var provider = new SmtpServer.ComponentModel.ServiceProvider();

            provider.Add(new SampleMessageStore());

            var server = new SmtpServer.SmtpServer(options, provider);

            await server.StartAsync(CancellationToken.None);
        }
    }
}