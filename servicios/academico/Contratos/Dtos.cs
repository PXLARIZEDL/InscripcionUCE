namespace Academico.Contratos;

// Lo que el estudiante ve al buscar asignaturas
public record AsignaturaDto(
    string Codigo,
    string Nombre,
    int Creditos,
    string Docente,
    string Horario,
    int CupoTotal,
    int CupoDisponible);

// Lo que Inscripciones recibe al apartar un cupo
public record CupoReservadoDto(
    string Codigo,
    string Nombre,
    int Creditos,
    string Docente);