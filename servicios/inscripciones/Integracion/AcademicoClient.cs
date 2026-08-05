using System.Net;
using System.Net.Http.Json;
using Inscripciones.Dominio;

namespace Inscripciones.Integracion;

// Contrato de lo que Academico devuelve. Es una copia de la FORMA del JSON,
// no una referencia a su código. Si Academico cambia su modelo interno,
// esto no se entera mientras el JSON siga igual.
public record CupoReservadoDto(string Codigo, string Nombre, int Creditos, string Docente);

public interface IReservaCupos
{
    Task<CupoReservadoDto?> ReservarAsync(string codigo, CancellationToken ct);
}

public class AcademicoClient(HttpClient http, ILogger<AcademicoClient> log) : IReservaCupos
{
    public async Task<CupoReservadoDto?> ReservarAsync(string codigo, CancellationToken ct)
    {
        var resp = await http.PostAsync($"/asignaturas/{codigo}/reservar", null, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            log.LogWarning("Academico no conoce la asignatura {Codigo}", codigo);
            return null;
        }

        if (resp.StatusCode == HttpStatusCode.Conflict)
            throw new SinCupoException(codigo);

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CupoReservadoDto>(cancellationToken: ct);
    }
}