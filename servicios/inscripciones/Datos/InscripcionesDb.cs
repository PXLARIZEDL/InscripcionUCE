using Inscripciones.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Inscripciones.Datos;

public class InscripcionesDb(DbContextOptions<InscripcionesDb> options) : DbContext(options)
{
    public DbSet<Inscripcion> Inscripciones => Set<Inscripcion>();
    public DbSet<MensajeSaliente> MensajesSalientes => Set<MensajeSaliente>();
}