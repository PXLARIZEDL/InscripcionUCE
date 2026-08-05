namespace Inscripciones.Dominio;

public class Inscripcion
{
    public Guid Id { get; set; }
    public string Matricula { get; set; } = default!;
    public string CodigoAsignatura { get; set; } = default!;

    // Copias deliberadas de datos que pertenecen a Academico.
    // No es caché: es el registro histórico de lo que se cursó.
    public string NombreAsignatura { get; set; } = default!;
    public int Creditos { get; set; }
    public string Docente { get; set; } = default!;

    public string Estado { get; set; } = "CONFIRMADA";
    public DateTime InscritaEn { get; set; }
}

public class SinCupoException(string codigo)
    : Exception($"La asignatura {codigo} no tiene cupos disponibles");