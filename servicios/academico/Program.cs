using Academico.Contratos;
using Academico.Datos;
using Academico.Dominio;
using Microsoft.EntityFrameworkCore;
//esto solo cablea 
//no valida datos , eso esta en el Datos/
//no hace logica de negocio , eso esta en Dominio/
//no tiene forma json eso esta en contratos 
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AcademicoDb>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Academico")));

builder.Services.AddHealthChecks()
       .AddDbContextCheck<AcademicoDb>("postgres");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AcademicoDb>();
    db.Database.EnsureCreated();
    Semilla.Aplicar(db);
}

app.MapGet("/asignaturas", async (AcademicoDb db) =>
    await db.Asignaturas
        .OrderBy(a => a.Codigo)
        .Select(a => new AsignaturaDto(
            a.Codigo, a.Nombre, a.Creditos, a.Docente,
            a.Horario, a.CupoTotal, a.CupoTotal - a.CupoOcupado))
        .ToListAsync());

app.MapGet("/asignaturas/{codigo}", async (string codigo, AcademicoDb db) =>
    await db.Asignaturas.FindAsync(codigo) is Asignatura a
        ? Results.Ok(new AsignaturaDto(a.Codigo, a.Nombre, a.Creditos,
                                       a.Docente, a.Horario, a.CupoTotal, a.CupoDisponible))
        : Results.NotFound(new { error = "Asignatura no encontrada", codigo }));

// Única forma en que alguien externo puede modificar el cupo.
app.MapPost("/asignaturas/{codigo}/reservar", async (string codigo, AcademicoDb db) =>
{
    var a = await db.Asignaturas.FindAsync(codigo);

    if (a is null)
        return Results.NotFound(new { error = "Asignatura no encontrada", codigo });

    try
    {
        a.OcuparCupo();
    }
    catch (SinCupoException)
    {
        return Results.Conflict(new { error = "Sin cupos disponibles", codigo });
    }

    await db.SaveChangesAsync();

    return Results.Ok(new CupoReservadoDto(a.Codigo, a.Nombre, a.Creditos, a.Docente));
});

app.MapHealthChecks("/health");

app.Run();