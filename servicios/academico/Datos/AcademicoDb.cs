using Academico.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Academico.Datos;

public class AcademicoDb(DbContextOptions<AcademicoDb> options) : DbContext(options)
{
    public DbSet<Asignatura> Asignaturas => Set<Asignatura>();
}

public static class Semilla
{
    public static void Aplicar(AcademicoDb db)
    {//con esta liena el sistema reinicia en cada arraque 
        if (db.Asignaturas.Any()) return;
        //porque db context esTA APARTE DE EL PROGRAM CS ?
        //ASI PROGRAM NO TIENE QUE CAMBIAR SI CAMBIO A UNA BD REAL , SOLO CAMBIO EL CONTEXT Y YA
        db.Asignaturas.AddRange(
            new Asignatura
            {
                Codigo = "INF-022",
                Nombre = "Redes de Comunicación I",
                Creditos = 4,
                Docente = "Ivan Zorrilla",
                Horario = "Lun-Mié 18:00-20:00",
                CupoTotal = 30,
                CupoOcupado = 28
            },

            new Asignatura
            {
                Codigo = "INF-033",
                Nombre = "Programación Avanzada",
                Creditos = 4,
                Docente = "Richard Jiménez",
                Horario = "Mar-Jue 18:00-20:00",
                CupoTotal = 25,
                CupoOcupado = 10
            },

            new Asignatura
            {
                Codigo = "INF-041",
                Nombre = "Ingeniería de Software",
                Creditos = 3,
                Docente = "Richard Jiménez",
                Horario = "Vie 18:00-21:00",
                CupoTotal = 25,
                CupoOcupado = 25
            },

            new Asignatura
            {
                Codigo = "MAT-210",
                Nombre = "Estadística Aplicada",
                Creditos = 3,
                Docente = "Adolfo Rodríguez",
                Horario = "Sáb 08:00-11:00",
                CupoTotal = 40,
                CupoOcupado = 12
            });

        db.SaveChanges();
    }
}