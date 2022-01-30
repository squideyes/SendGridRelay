using SmtpServer;

namespace Gateway;

public class Worker : BackgroundService
{
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

        provider.Add(new Relay(logger, apiKey));

        var server = new SmtpServer.SmtpServer(options, provider);

        await server.StartAsync(stoppingToken);
    }
}