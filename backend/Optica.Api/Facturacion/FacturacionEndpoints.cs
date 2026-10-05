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

public static class FacturacionEndpoints
{
    public static void MapFacturacion(this IEndpointRouteBuilder app)
    {
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
