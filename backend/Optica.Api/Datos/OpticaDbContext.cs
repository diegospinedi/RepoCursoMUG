using Microsoft.EntityFrameworkCore;
using Optica.Api.Catalogo;
using Optica.Api.Configuracion;
using Optica.Api.Presupuestos;

namespace Optica.Api.Datos;

public class OpticaDbContext(DbContextOptions<OpticaDbContext> options) : DbContext(options)
{
    public DbSet<Acceso.Acceso> Accesos => Set<Acceso.Acceso>();
    public DbSet<ParametrosNegocio> Parametros => Set<ParametrosNegocio>();
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Presupuesto> Presupuestos => Set<Presupuesto>();

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

        modelBuilder.Entity<Presupuesto>(e =>
        {
            e.HasIndex(p => p.Numero).IsUnique();
            e.HasIndex(p => p.Fecha);
            e.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);
            e.OwnsOne(p => p.Cliente, c =>
            {
                c.Property(x => x.Apellido).HasMaxLength(ValidacionPresupuesto.LargoNombre);
                c.Property(x => x.Nombre).HasMaxLength(ValidacionPresupuesto.LargoNombre);
                c.Property(x => x.Dni).HasMaxLength(9);
                c.Property(x => x.Domicilio).HasMaxLength(ValidacionPresupuesto.LargoDomicilio);
                c.Property(x => x.Email).HasMaxLength(ValidacionPresupuesto.LargoEmail);
                c.Property(x => x.Telefono).HasMaxLength(ValidacionPresupuesto.LargoTelefono);
                c.HasIndex(x => x.ApellidoBusqueda);
                c.HasIndex(x => x.Dni);
            });
            e.Navigation(p => p.Cliente).IsRequired();
        });
    }
}
