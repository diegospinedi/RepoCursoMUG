using Microsoft.EntityFrameworkCore;
using Optica.Api.Catalogo;
using Optica.Api.Configuracion;

namespace Optica.Api.Datos;

public class OpticaDbContext(DbContextOptions<OpticaDbContext> options) : DbContext(options)
{
    public DbSet<Acceso.Acceso> Accesos => Set<Acceso.Acceso>();
    public DbSet<ParametrosNegocio> Parametros => Set<ParametrosNegocio>();
    public DbSet<Articulo> Articulos => Set<Articulo>();

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

        modelBuilder.Entity<Articulo>(e =>
        {
            e.HasKey(a => a.Codigo);
            e.Property(a => a.Codigo).ValueGeneratedOnAdd();
            e.Property(a => a.CodigoProveedor).HasMaxLength(Catalogo.ArticulosEndpoints.LargoMaximoCodigoProveedor);
            e.Property(a => a.Descripcion).HasMaxLength(Catalogo.ArticulosEndpoints.LargoMaximoDescripcion);
            // La actualización por planilla (RF-47) busca por código en el proveedor.
            e.HasIndex(a => a.CodigoProveedor);
            e.HasIndex(a => a.Descripcion);
        });
    }
}
