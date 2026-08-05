using Inscripciones.Datos;
using Microsoft.EntityFrameworkCore;

namespace Inscripciones.Mensajeria;

public class PublicadorOutbox(
    IServiceScopeFactory scopeFactory,
    IEventBus bus,
    ILogger<PublicadorOutbox> log) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PublicarPendientesAsync(ct);
            }
            catch (Exception ex)
            {
                // Nunca dejamos morir el bucle: si RabbitMQ está caído,
                // logueamos y reintentamos en el próximo ciclo.
                log.LogWarning(ex, "No se pudieron publicar mensajes pendientes");
            }

            await Task.Delay(Intervalo, ct);
        }
    }

    private async Task PublicarPendientesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InscripcionesDb>();

        var pendientes = await db.MensajesSalientes
            .Where(m => m.PublicadoEn == null)
            .OrderBy(m => m.CreadoEn)
            .Take(20)
            .ToListAsync(ct);

        if (pendientes.Count == 0) return;

        foreach (var m in pendientes)
        {
            m.Intentos++;
            await bus.PublicarAsync(m.RoutingKey, m.Payload, ct);
            m.PublicadoEn = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Publicados {Cantidad} mensajes del outbox", pendientes.Count);
    }
}