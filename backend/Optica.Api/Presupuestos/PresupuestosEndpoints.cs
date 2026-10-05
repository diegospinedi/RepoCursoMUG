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
            await db.Presupuestos.AsNoTracking().SingleOrDefaultAsync(p => p.Numero == numero) is { } presupuesto
                ? Results.Ok(AVista(presupuesto))
                : Results.Problem($"No existe el presupuesto {numero}.", statusCode: StatusCodes.Status404NotFound));

        grupo.MapPost("/", async (DatosPresupuesto datos, OpticaDbContext db, TimeProvider reloj) =>
        {
            if (ValidacionPresupuesto.Validar(datos) is { Count: > 0 } errores)
                return Results.ValidationProblem(errores);

            var c = datos.Cliente!;
            var cliente = Cliente.Crear(c.Apellido!, c.Nombre!, c.Dni!, c.Domicilio, c.Email, c.Telefono);
            var fecha = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), HoraArgentina).DateTime);

            var presupuesto = await GrabarConNumeroNuevoAsync(db, numero => new Presupuesto(numero, fecha, cliente));
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

    private static PresupuestoVista AVista(Presupuesto p) => new(p.Numero, p.Fecha, p.Estado.ToString(),
        new ClienteVista(p.Cliente.Apellido, p.Cliente.Nombre, p.Cliente.Dni, p.Cliente.Domicilio, p.Cliente.Email, p.Cliente.Telefono));
}
