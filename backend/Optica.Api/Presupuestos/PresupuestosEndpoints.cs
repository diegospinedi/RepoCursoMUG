using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Optica.Api.Datos;

namespace Optica.Api.Presupuestos;

public static class PresupuestosEndpoints
{
    private static readonly TimeZoneInfo HoraArgentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public static void MapPresupuestos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/presupuestos");

        grupo.MapGet("/{numero:int}", async (int numero, OpticaDbContext db) =>
            await db.Presupuestos.AsNoTracking().Include(p => p.Lineas).SingleOrDefaultAsync(p => p.Numero == numero) is { } presupuesto
                ? Results.Ok(AVista(presupuesto))
                : Results.Problem($"No existe el presupuesto {numero}.", statusCode: StatusCodes.Status404NotFound));

        grupo.MapPost("/", async (DatosPresupuesto datos, OpticaDbContext db, TimeProvider reloj) =>
        {
            var errores = ValidacionPresupuesto.Validar(datos);
            await ValidarArticulosExistentesAsync(db, datos, errores);
            if (errores.Count > 0)
                return Results.ValidationProblem(errores);

            var c = datos.Cliente!;
            var cliente = Cliente.Crear(c.Apellido!, c.Nombre!, c.Dni!, c.Domicilio, c.Email, c.Telefono);
            var lineas = datos.Lineas!.Select((l, i) => new LineaPresupuesto(i + 1, l!.CodigoArticulo!.Value, l.Descripcion!,
                l.PrecioUnitario!.Value, (int)l.Cantidad!.Value, l.PorcentajeDescuento!.Value)).ToList();
            var fecha = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), HoraArgentina).DateTime);

            var presupuesto = await GrabarConNumeroNuevoAsync(db, numero => new Presupuesto(numero, fecha, cliente, lineas));
            return Results.Created($"/api/presupuestos/{presupuesto.Numero}", AVista(presupuesto));
        });
    }

    /// <summary>
    /// Asigna el último número + 1 (RF-06) sin repetir números con dos sesiones
    /// grabando a la vez (RNF-13, AC-63). La transacción de Microsoft.Data.Sqlite
    /// arranca con BEGIN IMMEDIATE: toma el lock de escritura antes de leer el máximo,
    /// y la segunda sesión espera (no falla) hasta que la primera confirma. El índice
    /// único sobre Numero es la red de seguridad: si igual chocaran, se reintenta.
    /// </summary>
    private static async Task<Presupuesto> GrabarConNumeroNuevoAsync(OpticaDbContext db, Func<int, Presupuesto> crear)
    {
        const int intentos = 3;
        for (var intento = 1; ; intento++)
        {
            await using var transaccion = await db.Database.BeginTransactionAsync();
            var ultimo = await db.Presupuestos.MaxAsync(p => (int?)p.Numero) ?? 0;
            var presupuesto = crear(ultimo + 1);
            db.Presupuestos.Add(presupuesto);
            try
            {
                await db.SaveChangesAsync();
                await transaccion.CommitAsync();
                return presupuesto;
            }
            catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: 19 } && intento < intentos)
            {
                // 19 = SQLITE_CONSTRAINT: otro presupuesto tomó el número. Se reintenta con el siguiente.
                await transaccion.RollbackAsync();
                db.Entry(presupuesto).State = EntityState.Detached;
            }
        }
    }

    /// <summary>Cada línea tiene que corresponder a un artículo del catálogo (RF-10, RF-11).</summary>
    private static async Task ValidarArticulosExistentesAsync(OpticaDbContext db, DatosPresupuesto datos,
        Dictionary<string, string[]> errores)
    {
        if (datos.Lineas is null)
            return;
        var codigos = datos.Lineas.Where(l => l?.CodigoArticulo is not null).Select(l => l!.CodigoArticulo!.Value).Distinct().ToList();
        var existentes = await db.Articulos.Where(a => codigos.Contains(a.Codigo)).Select(a => a.Codigo).ToListAsync();
        for (var i = 0; i < datos.Lineas.Count; i++)
        {
            if (datos.Lineas[i]?.CodigoArticulo is { } codigo && !existentes.Contains(codigo))
                errores[$"lineas[{i}].codigoArticulo"] = [$"El artículo {codigo} no existe en el catálogo: elegí otro"];
        }
    }

    private static PresupuestoVista AVista(Presupuesto p) => new(p.Numero, p.Fecha, p.Estado.ToString(),
        new ClienteVista(p.Cliente.Apellido, p.Cliente.Nombre, p.Cliente.Dni, p.Cliente.Domicilio, p.Cliente.Email, p.Cliente.Telefono),
        p.Lineas.OrderBy(l => l.Orden).Select(l => new LineaVista(l.CodigoArticulo, l.Descripcion, l.PrecioUnitario,
            l.Cantidad, l.PorcentajeDescuento, l.PrecioConDescuento, l.PrecioFinal)).ToList(),
        p.Total);
}
