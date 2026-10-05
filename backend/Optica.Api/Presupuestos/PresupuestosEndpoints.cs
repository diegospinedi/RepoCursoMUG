using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Optica.Api.Datos;

namespace Optica.Api.Presupuestos;

public static class PresupuestosEndpoints
{
    public const int TamanoPagina = 50;

    private static readonly TimeZoneInfo HoraArgentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public static void MapPresupuestos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/presupuestos");

        // RF-03: busca por fecha y datos del cliente. Los filtros se combinan con "Y" (RF-81).
        grupo.MapGet("/", async (string? desde, string? hasta, string? apellido, string? nombre, string? dni, int? pagina,
            OpticaDbContext db) =>
        {
            var errores = new Dictionary<string, string[]>();
            var fechaDesde = LeerFecha(desde, "desde", errores);
            var fechaHasta = LeerFecha(hasta, "hasta", errores);
            if (fechaDesde > fechaHasta)
                errores["hasta"] = ["La fecha Hasta no puede ser anterior a la fecha Desde"];
            var dniBuscado = Cliente.SoloDigitos(dni ?? "");
            if (!string.IsNullOrWhiteSpace(dni) && dniBuscado == "")
                errores["dni"] = ["Ingresá solo los números del DNI, con o sin puntos"];
            if (errores.Count > 0)
                return Results.ValidationProblem(errores);

            var consulta = db.Presupuestos.AsNoTracking();
            // RF-83: rango inclusive, con uno solo de los dos límites si se quiere.
            if (fechaDesde is { } d)
                consulta = consulta.Where(p => p.Fecha >= d);
            if (fechaHasta is { } h)
                consulta = consulta.Where(p => p.Fecha <= h);
            // RF-82: coincidencia parcial sin mayúsculas ni acentos, sobre las columnas normalizadas.
            var apellidoBuscado = TextoBusqueda.Normalizar(apellido);
            if (apellidoBuscado != "")
                consulta = consulta.Where(p => p.Cliente.ApellidoBusqueda.Contains(apellidoBuscado));
            var nombreBuscado = TextoBusqueda.Normalizar(nombre);
            if (nombreBuscado != "")
                consulta = consulta.Where(p => p.Cliente.NombreBusqueda.Contains(nombreBuscado));
            // RF-88: coincidencia parcial ignorando puntos y guiones (el DNI se guarda solo con dígitos).
            if (dniBuscado != "")
                consulta = consulta.Where(p => p.Cliente.Dni.Contains(dniBuscado));

            var numeroPagina = Math.Max(pagina ?? 1, 1);
            var total = await consulta.CountAsync();
            var presupuestos = await consulta
                .OrderByDescending(p => p.Numero)
                .Skip((numeroPagina - 1) * TamanoPagina).Take(TamanoPagina)
                .Select(p => new ResumenPresupuesto(p.Numero, p.Fecha, p.Estado.ToString(), p.Cliente.Apellido,
                    p.Cliente.Nombre, p.Cliente.Dni, p.Total))
                .ToListAsync();

            return Results.Ok(new PaginaPresupuestos(presupuestos, total, numeroPagina, TamanoPagina));
        });

        grupo.MapGet("/{numero:int}", async (int numero, OpticaDbContext db) =>
            await db.Presupuestos.AsNoTracking().Include(p => p.Lineas).SingleOrDefaultAsync(p => p.Numero == numero) is { } presupuesto
                ? Results.Ok(AVista(presupuesto))
                : NoEncontrado(numero));

        grupo.MapPost("/", async (DatosPresupuesto datos, OpticaDbContext db, TimeProvider reloj) =>
        {
            var errores = ValidacionPresupuesto.Validar(datos);
            var lineas = await ArmarLineasAsync(db, datos.Lineas, existente: null, errores);
            if (errores.Count > 0)
                return Results.ValidationProblem(errores);

            var cliente = CrearCliente(datos.Cliente!);
            var fecha = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), HoraArgentina).DateTime);

            var presupuesto = await GrabarConNumeroNuevoAsync(db, numero => new Presupuesto(numero, fecha, cliente, lineas));
            return Results.Created($"/api/presupuestos/{presupuesto.Numero}", AVista(presupuesto));
        });

        grupo.MapPut("/{numero:int}", async (int numero, DatosPresupuesto datos, OpticaDbContext db) =>
        {
            // BEGIN IMMEDIATE: si otra sesión está finalizando este mismo presupuesto, se
            // espera a que termine y recién entonces se lee el estado. Así no hay forma de
            // pisar un presupuesto que pasó a Final en el medio (RF-08).
            await using var transaccion = await db.Database.BeginTransactionAsync();
            var presupuesto = await db.Presupuestos.Include(p => p.Lineas).SingleOrDefaultAsync(p => p.Numero == numero);
            if (presupuesto is null)
                return NoEncontrado(numero);
            if (presupuesto.Estado == EstadoPresupuesto.Final)
                return Results.Problem($"El presupuesto {numero} está en estado Final y no se puede modificar ni volver a Borrador.",
                    statusCode: StatusCodes.Status409Conflict);

            var errores = ValidacionPresupuesto.Validar(datos);
            if (!Enum.TryParse<EstadoPresupuesto>(datos.Estado, ignoreCase: false, out var estado) || !Enum.IsDefined(estado))
                errores["estado"] = ["Elegí el estado: Borrador o Final"];
            var lineas = await ArmarLineasAsync(db, datos.Lineas, presupuesto, errores);
            if (errores.Count > 0)
                return Results.ValidationProblem(errores);

            presupuesto.Modificar(CrearCliente(datos.Cliente!), lineas);
            await db.SaveChangesAsync();
            if (estado == EstadoPresupuesto.Final)
            {
                // Se graba en dos pasos porque la base rechaza cambios en las líneas de un
                // presupuesto Final (ver migración CierrePresupuestos): primero el contenido,
                // después el estado.
                presupuesto.Finalizar();
                await db.SaveChangesAsync();
            }
            await transaccion.CommitAsync();

            return Results.Ok(AVista(presupuesto));
        });
    }

    private static DateOnly? LeerFecha(string? texto, string campo, Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;
        if (DateOnly.TryParseExact(texto, "yyyy-MM-dd", out var fecha))
            return fecha;
        errores[campo] = ["Ingresá una fecha válida (dd/mm/aaaa)"];
        return null;
    }

    private static Cliente CrearCliente(DatosCliente c) =>
        Cliente.Crear(c.Apellido!, c.Nombre!, c.Dni!, c.Domicilio, c.Email, c.Telefono);

    /// <summary>
    /// Arma las líneas tomando la descripción y el precio unitario del lado del servidor:
    /// - Si el presupuesto ya tenía ese artículo, conserva la descripción y el precio
    ///   grabados, aunque el catálogo haya cambiado después (RF-87).
    /// - Si es nuevo, usa la descripción y el precio de venta guardados en el catálogo,
    ///   sin recalcularlo (RF-39, RF-73).
    /// La operadora no puede escribir el precio: así no hay errores de precio (objetivo del PRD).
    /// </summary>
    private static async Task<List<LineaPresupuesto>> ArmarLineasAsync(OpticaDbContext db, List<DatosLinea?>? datos,
        Presupuesto? existente, Dictionary<string, string[]> errores)
    {
        var lineas = new List<LineaPresupuesto>();
        if (datos is null)
            return lineas;

        var codigos = datos.Where(l => l?.CodigoArticulo is not null).Select(l => l!.CodigoArticulo!.Value).Distinct().ToList();
        var articulos = await db.Articulos.AsNoTracking().Where(a => codigos.Contains(a.Codigo))
            .ToDictionaryAsync(a => a.Codigo, a => (a.Descripcion, a.PrecioVenta));
        var grabadas = existente?.Lineas.GroupBy(l => l.CodigoArticulo)
            .ToDictionary(g => g.Key, g => (g.First().Descripcion, PrecioVenta: g.First().PrecioUnitario)) ?? [];

        for (var i = 0; i < datos.Count; i++)
        {
            if (datos[i]?.CodigoArticulo is not { } codigo)
                continue;

            if (!grabadas.TryGetValue(codigo, out var origen) && !articulos.TryGetValue(codigo, out origen))
            {
                errores[$"lineas[{i}].codigoArticulo"] = [$"El artículo {codigo} no existe en el catálogo: elegí otro"];
                continue;
            }
            if (origen.PrecioVenta < 0)
            {
                // RF-17. Un precio de venta negativo solo puede venir de la planilla (RF-80).
                errores[$"lineas[{i}].precioUnitario"] =
                    [$"El precio unitario no puede ser negativo: corregí el precio del artículo {codigo} en el catálogo"];
                continue;
            }

            var linea = datos[i]!;
            if (errores.Keys.Any(k => k.StartsWith($"lineas[{i}].")))
                continue;
            lineas.Add(new LineaPresupuesto(lineas.Count + 1, codigo, origen.Descripcion, origen.PrecioVenta,
                (int)linea.Cantidad!.Value, linea.PorcentajeDescuento!.Value));
        }
        return lineas;
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

    private static IResult NoEncontrado(int numero) =>
        Results.Problem($"No existe el presupuesto {numero}.", statusCode: StatusCodes.Status404NotFound);

    private static PresupuestoVista AVista(Presupuesto p) => new(p.Numero, p.Fecha, p.Estado.ToString(),
        new ClienteVista(p.Cliente.Apellido, p.Cliente.Nombre, p.Cliente.Dni, p.Cliente.Domicilio, p.Cliente.Email, p.Cliente.Telefono),
        p.Lineas.OrderBy(l => l.Orden).Select(l => new LineaVista(l.CodigoArticulo, l.Descripcion, l.PrecioUnitario,
            l.Cantidad, l.PorcentajeDescuento, l.PrecioConDescuento, l.PrecioFinal)).ToList(),
        p.Total);
}
