using System.Text;
using RabbitMQ.Client;

namespace Inscripciones.Mensajeria;

public interface IEventBus//recibe un string no un objeto aqui el json ya viene serializado
{
    Task PublicarAsync(string routingKey, string payloadJson, CancellationToken ct = default);
}

public class RabbitMqEventBus : IEventBus, IAsyncDisposable
{
    public const string Exchange = "uce.eventos";

    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqEventBus> _log;
    private readonly SemaphoreSlim _candado = new(1, 1);
    private IConnection? _conexion;
    private IChannel? _canal;

    public RabbitMqEventBus(IConfiguration cfg, ILogger<RabbitMqEventBus> log)
    {
        _factory = new ConnectionFactory { Uri = new Uri(cfg["RabbitMq:Uri"]!) };
        _log = log;
    }

    private async Task<IChannel> CanalAsync(CancellationToken ct)
    {
        if (_canal is { IsOpen: true }) return _canal;

        await _candado.WaitAsync(ct);
        try
        {
            if (_canal is { IsOpen: true }) return _canal;

            _conexion = await _factory.CreateConnectionAsync(ct);
            _canal = await _conexion.CreateChannelAsync(cancellationToken: ct);

            // durable: el exchange sobrevive a un reinicio del broker.
            await _canal.ExchangeDeclareAsync(
                Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

            return _canal;
        }
        finally { _candado.Release(); }
    }

    public async Task PublicarAsync(string routingKey, string payloadJson, CancellationToken ct = default)
    {
        var canal = await CanalAsync(ct);

        var props = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,                        // el mensaje se escribe a disco
            MessageId = Guid.NewGuid().ToString()     // clave para idempotencia del consumidor
        };

        await canal.BasicPublishAsync(
            exchange: Exchange, routingKey: routingKey, mandatory: false,
            basicProperties: props, body: Encoding.UTF8.GetBytes(payloadJson),
            cancellationToken: ct);

        _log.LogInformation("Evento publicado: {RoutingKey}", routingKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is not null) await _canal.DisposeAsync();
        if (_conexion is not null) await _conexion.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}