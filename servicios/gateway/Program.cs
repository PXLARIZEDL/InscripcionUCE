var builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    servicio = "InscripcionUCE API Gateway",
    rutas = new[] { "/api/asignaturas", "/api/inscripciones", "/api/notificaciones" }
}));

app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();