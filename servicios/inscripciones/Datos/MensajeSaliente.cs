namespace Inscripciones.Dominio;

public class MensajeSaliente
{
    public Guid Id { get; set; }
    public string RoutingKey { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public DateTime CreadoEn { get; set; }
    public DateTime? PublicadoEn { get; set; }
    public int Intentos { get; set; }
}