using Microsoft.EntityFrameworkCore;
using Optica.Api.Arca;
using Optica.Api.Datos;
using Optica.Api.Presupuestos;

namespace Optica.Api.Facturacion;

public record FacturaVista(
    string Letra,
    string Comprobante,
    int PuntoVenta,
    long Numero,
    DateOnly Fecha,
    string Receptor,
    decimal ImporteTotal,
    decimal ImporteNeto,
    decimal ImporteIva,
    decimal? AlicuotaIva,
    string Cae,
    DateOnly VencimientoCae,
    int NumeroPresupuesto,
    IReadOnlyList<LineaVista> Lineas);

public record ResumenFactura(string Letra, string Comprobante, DateOnly Fecha, string Apellido, string Nombre, string Dni,
    decimal ImporteTotal, string Cae, int NumeroPresupuesto);

public record PaginaFacturas(IReadOnlyList<ResumenFactura> Facturas, int Total, int Pagina, int TamanoPagina);

public static class FacturacionEndpoints
{
    public const int TamanoPagina = 50;

    public static void MapFacturacion(this IEndpointRouteBuilder app)
    {
        // RF-33, RF-54: lista y busca facturas por fecha, número de comprobante y datos del
        // cliente, con los filtros combinados con "Y" (RF-81). No hay endpoints para
        // modificar ni eliminar una factura (RF-32).
        app.MapGet("/api/facturas", async (string? desde, string? hasta, string? comprobante, string? apellido,
            string? nombre, string? dni, int? pagina, OpticaDbContext db) =>
        {
            var errores = new Dictionary<string, string[]>();
            var filtros = FiltrosBusqueda.Leer(desde, hasta, apellido, nombre, dni, errores);
            var comprobanteBuscado = FiltrosBusqueda.SoloDigitos(comprobante);
            if (!string.IsNullOrWhiteSpace(comprobante) && comprobanteBuscado == "")
                errores["comprobante"] = ["Ingresá los números del comprobante, por ejemplo 0003-00034561 o 34561"];
            if (errores.Count > 0)
                return Results.ValidationProblem(errores);

            var consulta = db.Facturas.AsNoTracking()
                .Join(db.Presupuestos, f => f.PresupuestoId, p => p.Id, (f, p) => new { Factura = f, Presupuesto = p });
            if (filtros.Desde is { } d)
                consulta = consulta.Where(x => x.Factura.Fecha >= d);
            if (filtros.Hasta is { } h)
                consulta = consulta.Where(x => x.Factura.Fecha <= h);
            if (comprobanteBuscado != "")
                consulta = consulta.Where(x => x.Factura.ComprobanteBusqueda.Contains(comprobanteBuscado));
            if (filtros.Apellido != "")
                consulta = consulta.Where(x => x.Presupuesto.Cliente.ApellidoBusqueda.Contains(filtros.Apellido));
            if (filtros.Nombre != "")
                consulta = consulta.Where(x => x.Presupuesto.Cliente.NombreBusqueda.Contains(filtros.Nombre));
            if (filtros.Dni != "")
                consulta = consulta.Where(x => x.Presupuesto.Cliente.Dni.Contains(filtros.Dni));

            var numeroPagina = Math.Max(pagina ?? 1, 1);
            var total = await consulta.CountAsync();
            var filas = await consulta
                .OrderByDescending(x => x.Factura.Fecha).ThenByDescending(x => x.Factura.Id)
                .Skip((numeroPagina - 1) * TamanoPagina).Take(TamanoPagina)
                .Select(x => new
                {
                    x.Factura.Tipo, x.Factura.PuntoVenta, x.Factura.Numero, x.Factura.Fecha, x.Factura.ImporteTotal, x.Factura.Cae,
                    x.Presupuesto.Cliente.Apellido, x.Presupuesto.Cliente.Nombre, x.Presupuesto.Cliente.Dni, NumeroPresupuesto = x.Presupuesto.Numero,
                })
                .ToListAsync();

            var facturas = filas.Select(f => new ResumenFactura(Factura.Letra(f.Tipo), Factura.FormatearNumero(f.PuntoVenta, f.Numero),
                f.Fecha, f.Apellido, f.Nombre, f.Dni, f.ImporteTotal, f.Cae, f.NumeroPresupuesto)).ToList();
            return Results.Ok(new PaginaFacturas(facturas, total, numeroPagina, TamanoPagina));
        });

        var grupo = app.MapGroup("/api/presupuestos/{numero:int}/factura");

        grupo.MapGet("/", async (int numero, OpticaDbContext db) =>
        {
            var presupuesto = await db.Presupuestos.AsNoTracking().Include(p => p.Lineas).SingleOrDefaultAsync(p => p.Numero == numero);
            if (presupuesto is null)
                return Results.Problem($"No existe el presupuesto {numero}.", statusCode: StatusCodes.Status404NotFound);
            var factura = await db.Facturas.AsNoTracking().SingleOrDefaultAsync(f => f.PresupuestoId == presupuesto.Id);
            return factura is null
                ? Results.Problem($"El presupuesto {numero} no tiene factura.", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(AVista(factura, presupuesto));
        });

        // RF-50: PDF de la factura con los datos de RF-30 y el QR de ARCA (AC-19).
        grupo.MapGet("/pdf", async (int numero, OpticaDbContext db, OpcionesEmisor emisor, OpcionesArca arca) =>
        {
            var presupuesto = await db.Presupuestos.AsNoTracking().Include(p => p.Lineas).SingleOrDefaultAsync(p => p.Numero == numero);
            var factura = presupuesto is null ? null
                : await db.Facturas.AsNoTracking().SingleOrDefaultAsync(f => f.PresupuestoId == presupuesto.Id);
            if (presupuesto is null || factura is null)
                return Results.Problem($"El presupuesto {numero} no tiene factura.", statusCode: StatusCodes.Status404NotFound);

            var pdf = PdfFactura.Generar(factura, presupuesto, emisor, simulado: arca.Entorno == EntornoArca.Simulado);
            return Results.File(pdf, "application/pdf",
                $"Factura-{Factura.Letra(factura.Tipo)}-{Factura.FormatearNumero(factura.PuntoVenta, factura.Numero)}.pdf");
        });

        // RF-25: factura el presupuesto. También es el reintento (RF-52).
        grupo.MapPost("/", async (int numero, OpticaDbContext db, ServicioFacturacion servicio, CancellationToken cancelacion) =>
        {
            if (!await db.Presupuestos.AnyAsync(p => p.Numero == numero, cancelacion))
                return Results.Problem($"No existe el presupuesto {numero}.", statusCode: StatusCodes.Status404NotFound);

            var resultado = await servicio.EmitirAsync(numero, cancelacion);
            switch (resultado)
            {
                case EmisionAutorizada autorizada:
                    var presupuesto = await db.Presupuestos.AsNoTracking().Include(p => p.Lineas).SingleAsync(p => p.Numero == numero, cancelacion);
                    return Results.Created($"/api/presupuestos/{numero}/factura", AVista(autorizada.Factura, presupuesto));
                case EmisionRechazada rechazada:
                    return Results.Problem("ARCA rechazó la factura. Corregí lo indicado y volvé a intentar.",
                        statusCode: StatusCodes.Status422UnprocessableEntity, title: "Rechazada por ARCA",
                        extensions: new Dictionary<string, object?> { ["erroresArca"] = rechazada.Errores });
                case EmisionSinRespuesta sinRespuesta:
                    return Results.Problem(sinRespuesta.Motivo, statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "ARCA no disponible", extensions: new Dictionary<string, object?> { ["pendiente"] = true });
                case EmisionInconsistente inconsistente:
                    return Results.Problem(inconsistente.Motivo, statusCode: StatusCodes.Status409Conflict,
                        title: "Revisá el punto de venta en ARCA");
                case EmisionNoPermitida noPermitida:
                    return Results.Problem(noPermitida.Motivo,
                        statusCode: noPermitida.Conflicto ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity);
                default:
                    throw new InvalidOperationException($"Resultado de emisión desconocido: {resultado}");
            }
        });
    }

    public static FacturaVista AVista(Factura f, Presupuesto p) => new(
        Factura.Letra(f.Tipo),
        Factura.FormatearNumero(f.PuntoVenta, f.Numero),
        f.PuntoVenta,
        f.Numero,
        f.Fecha,
        f.TipoDocumentoReceptor == TipoDocumento.Dni ? $"DNI {f.NumeroDocumentoReceptor}" : "Consumidor Final",
        f.ImporteTotal,
        f.ImporteNeto,
        f.ImporteIva,
        f.AlicuotaIva,
        f.Cae,
        f.VencimientoCae,
        p.Numero,
        p.Lineas.OrderBy(l => l.Orden).Select(l => new LineaVista(l.CodigoArticulo, l.Descripcion, l.PrecioUnitario,
            l.Cantidad, l.PorcentajeDescuento, l.PrecioConDescuento, l.PrecioFinal)).ToList());
}
