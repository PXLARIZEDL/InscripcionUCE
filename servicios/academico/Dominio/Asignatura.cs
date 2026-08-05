using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Academico.Dominio;

public class Asignatura//seccion ofertada en un semestre academico
                       //este servicio es el unico dueño de el cupo , nadie mas escribe ni lee en esta tabla 
{
    [Key] //EF Core busca por convencion una propiedad Id o AsignaturaId.
          //Uso Codigo (INF-022) porque es el identificador del negocio, no un id tecnico.
    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public int Creditos { get; set; }
    public string Docente { get; set; } = default!;
    public string Horario { get; set; } = default!;
    public int CupoTotal { get; set; }
    public int CupoOcupado { get; set; }
    [NotMapped] //esto es un calculo , sin esto intenta maper la bd y falla , este calculo se hace en memoria  
    public int CupoDisponible => CupoTotal - CupoOcupado;
    public bool TieneCupo() => CupoDisponible > 0;
    public void OcuparCupo()
    {
        if (!TieneCupo())
            throw new SinCupoException(Codigo);
        CupoOcupado++;
    }
}
public class SinCupoException(string codigo)
    : Exception($"La asignatura {codigo} no tiene cupos disponibles");