using Microsoft.EntityFrameworkCore;

namespace Optica.Api.Datos;

public class OpticaDbContext(DbContextOptions<OpticaDbContext> options) : DbContext(options)
{
    public DbSet<Acceso.Acceso> Accesos => Set<Acceso.Acceso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Acceso.Acceso>(e =>
        {
            e.ToTable("Acceso");
            e.Property(a => a.Id).ValueGeneratedNever();
            // Garantiza a nivel de base que haya una sola credencial.
            e.ToTable(t => t.HasCheckConstraint("CK_Acceso_Unico", $"Id = {Acceso.Acceso.IdUnico}"));
        });
    }
}
