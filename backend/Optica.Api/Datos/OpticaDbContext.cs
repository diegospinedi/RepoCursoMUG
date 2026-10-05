using Microsoft.EntityFrameworkCore;
using Optica.Api.Configuracion;

namespace Optica.Api.Datos;

public class OpticaDbContext(DbContextOptions<OpticaDbContext> options) : DbContext(options)
{
    public DbSet<Acceso.Acceso> Accesos => Set<Acceso.Acceso>();
    public DbSet<ParametrosNegocio> Parametros => Set<ParametrosNegocio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Acceso.Acceso>(e =>
        {
            e.ToTable("Acceso");
            e.Property(a => a.Id).ValueGeneratedNever();
            // Garantiza a nivel de base que haya una sola credencial.
            e.ToTable(t => t.HasCheckConstraint("CK_Acceso_Unico", $"Id = {Acceso.Acceso.IdUnico}"));
        });

        modelBuilder.Entity<ParametrosNegocio>(e =>
        {
            e.ToTable("Configuracion", t => t.HasCheckConstraint("CK_Configuracion_Unica", $"Id = {ParametrosNegocio.IdUnico}"));
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.CondicionFiscal).HasConversion<string>().HasMaxLength(30);
            // Valores iniciales: la operadora los revisa en la pantalla de configuración.
            e.HasData(new ParametrosNegocio
            {
                AlicuotaIva = 21m,
                CondicionFiscal = CondicionFiscal.ResponsableInscripto,
                TopeIdentificacion = 10_000_000m,
                MultiploRedondeo = 0.01m,
            });
        });
    }
}
