using Microsoft.EntityFrameworkCore;
using Optica.Api.Datos;

namespace Optica.Api.Configuracion;

public record DatosConfiguracion(
    decimal? AlicuotaIva,
    string? CondicionFiscal,
    decimal? TopeIdentificacion,
    decimal? MultiploRedondeo);

/// <summary>Respuesta al grabar: la configuración y cuántos precios de venta cambiaron (RF-86).</summary>
public record ConfiguracionGrabada(
    decimal AlicuotaIva,
    string CondicionFiscal,
    decimal TopeIdentificacion,
    decimal MultiploRedondeo,
    int PreciosActualizados);

public static class ConfiguracionEndpoints
{
    public const decimal MultiploMinimo = 0.01m;

    public static void MapConfiguracion(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/configuracion");

        grupo.MapGet("/", async (OpticaDbContext db) => Results.Ok(ADatos(await db.Parametros.SingleAsync())));

        grupo.MapPut("/", async (DatosConfiguracion datos, OpticaDbContext db) =>
        {
            var errores = Validar(datos, out var condicion);
            if (errores.Count > 0)
                return Results.ValidationProblem(errores);

            var parametros = await db.Parametros.SingleAsync();
            var afectaPrecios = parametros.AlicuotaIva != datos.AlicuotaIva
                || parametros.CondicionFiscal != condicion
                || parametros.MultiploRedondeo != datos.MultiploRedondeo;

            parametros.AlicuotaIva = datos.AlicuotaIva!.Value;
            parametros.CondicionFiscal = condicion;
            parametros.TopeIdentificacion = datos.TopeIdentificacion!.Value;
            parametros.MultiploRedondeo = datos.MultiploRedondeo!.Value;

            // RF-86: el catálogo se recalcula en la misma transacción que el cambio de
            // configuración, así nunca quedan precios calculados con parámetros viejos.
            // Las líneas de presupuestos guardan su propio precio y no se tocan (RF-87).
            var preciosActualizados = 0;
            if (afectaPrecios)
            {
                await foreach (var articulo in db.Articulos.AsAsyncEnumerable())
                {
                    var anterior = articulo.PrecioVenta;
                    articulo.RecalcularPrecioVenta(parametros);
                    if (articulo.PrecioVenta != anterior)
                        preciosActualizados++;
                }
            }
            await db.SaveChangesAsync();

            return Results.Ok(new ConfiguracionGrabada(parametros.AlicuotaIva, parametros.CondicionFiscal.ToString(),
                parametros.TopeIdentificacion, parametros.MultiploRedondeo, preciosActualizados));
        });
    }

    private static DatosConfiguracion ADatos(ParametrosNegocio c) =>
        new(c.AlicuotaIva, c.CondicionFiscal.ToString(), c.TopeIdentificacion, c.MultiploRedondeo);

    /// <summary>Valida cada campo con un mensaje que indica cómo corregirlo (RF-35).</summary>
    private static Dictionary<string, string[]> Validar(DatosConfiguracion datos, out CondicionFiscal condicion)
    {
        var errores = new Dictionary<string, string[]>();

        if (datos.AlicuotaIva is not { } alicuota || alicuota < 0 || alicuota > 100)
            errores["alicuotaIva"] = ["Ingresá una alícuota de IVA entre 0 y 100, por ejemplo 21 o 10,5"];

        if (!Enum.TryParse(datos.CondicionFiscal, ignoreCase: false, out condicion) || !Enum.IsDefined(condicion))
            errores["condicionFiscal"] = ["Elegí Responsable Inscripto o Monotributo"];

        if (datos.TopeIdentificacion is not { } tope || tope < 0)
            errores["topeIdentificacion"] = ["Ingresá un tope mayor o igual a 0"];
        else if (decimal.Round(tope, 2) != tope)
            errores["topeIdentificacion"] = ["El tope puede tener como máximo 2 decimales"];

        // RF-72: 0,01 equivale a redondear solo a 2 decimales (RF-19); no hay valores menores.
        if (datos.MultiploRedondeo is not { } multiplo || multiplo < MultiploMinimo)
            errores["multiploRedondeo"] = ["El valor mínimo es 0,01"];
        else if (decimal.Round(multiplo, 2) != multiplo)
            errores["multiploRedondeo"] = ["El múltiplo puede tener como máximo 2 decimales, por ejemplo 0,01, 10 o 50"];

        return errores;
    }
}
