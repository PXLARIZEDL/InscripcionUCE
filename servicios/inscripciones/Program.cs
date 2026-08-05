using System.Text.Json;
using Inscripciones.Datos;
using Inscripciones.Dominio;
using Inscripciones.Integracion;
using Inscripciones.Mensajeria;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<InscripcionesDb>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Inscripciones")));

// Cliente tipado + resiliencia estándar de .NET 8:
// timeout, 3 reintentos con backoff exponencial y jitter, y circuit breaker.
builder.Services.AddHttpClient<IReservaCupos, AcademicoClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Servicios:Academico"]!);
})
.AddStandardResilienceHandler();

builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>();
builder.Services.AddHostedService<PublicadorOutbox>();

builder.Services.AddHealthChecks()
       .AddDbContextCheck<InscripcionesDb>("postgres");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<InscripcionesDb>().Database.EnsureCreated();

app.MapPost("/inscripciones", async (
    CrearInscripcionDto dto,
    IReservaCupos academico,
    InscripcionesDb db,
    ILogger<Program> log,
    CancellationToken ct) =>
{
    // 1. SINCRÓNICO: el estudiante espera la respuesta.
    CupoReservadoDto? cupo;
    try
    {
        cupo = await academico.ReservarAsync(dto.CodigoAsignatura, ct);
    }
    catch (SinCupoException)
    {
        return Results.Conflict(new { error = "Sin cupos disponibles", dto.CodigoAsignatura });
    }
    catch (Exception ex)   // red caída, timeout, circuito abierto
    {
        log.LogError(ex, "Academico no está disponible");
        return Results.Problem(
            "El sistema académico no está disponible. Intenta de nuevo.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (cupo is null)
        return Results.BadRequest(new { error = "La asignatura no existe", dto.CodigoAsignatura });

    // 2. Escritura local: aquí soy dueño y tengo transacción real.
    var inscripcion = new Inscripcion
    {
        Id = Guid.NewGuid(),
        Matricula = dto.Matricula,
        CodigoAsignatura = cupo.Codigo,
        NombreAsignatura = cupo.Nombre,
        Creditos = cupo.Creditos,
        Docente = cupo.Docente,
        InscritaEn = DateTime.UtcNow
    };

    var evento = new InscripcionConfirmada(
        inscripcion.Id, inscripcion.Matricula, inscripcion.NombreAsignatura,
        inscripcion.Creditos, inscripcion.Docente, inscripcion.InscritaEn);

    // 3. OUTBOX: el mensaje se guarda en la MISMA transacción que la inscripción.
    var mensaje = new MensajeSaliente
    {
        Id = Guid.NewGuid(),
        RoutingKey = "inscripcion.confirmada",
        Payload = JsonSerializer.Serialize(evento, JsonOpciones.Web),
        CreadoEn = DateTime.UtcNow
    };

    db.Inscripciones.Add(inscripcion);
    db.MensajesSalientes.Add(mensaje);
    await db.SaveChangesAsync(ct);   // ← una sola transacción, dos filas

    return Results.Created($"/inscripciones/{inscripcion.Id}", inscripcion);
});

app.MapGet("/inscripciones", async (InscripcionesDb db) =>
    await db.Inscripciones.OrderByDescending(i => i.InscritaEn).Take(20).ToListAsync());

app.MapGet("/inscripciones/{id:guid}", async (Guid id, InscripcionesDb db) =>
    await db.Inscripciones.FindAsync(id) is Inscripcion i ? Results.Ok(i) : Results.NotFound());

app.MapHealthChecks("/health");

app.Run();

public record CrearInscripcionDto(string Matricula, string CodigoAsignatura);

public record InscripcionConfirmada(
    Guid InscripcionId, string Matricula, string NombreAsignatura,
    int Creditos, string Docente, DateTime OcurridoEn);

public static class JsonOpciones
{
    // camelCase explícito. Sin esto C# publica "InscripcionId" y el consumidor
    // en Python busca "inscripcionId". Falla en runtime, nunca en compilación.
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}