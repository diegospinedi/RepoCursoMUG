using Optica.Api.Arca;
using Optica.Api.Configuracion;
using Optica.Api.Presupuestos;

namespace Optica.Api.Facturacion;

/// <summary>Un dato impide facturar y la operadora puede corregirlo (se muestra tal cual).</summary>
public class FacturacionException(string mensaje) : Exception(mensaje);

/// <summary>
/// Arma la solicitud a ARCA a partir de un presupuesto Final (RF-25), sin que la
/// operadora vuelva a cargar datos (AC-16).
/// </summary>
public static class ArmadorComprobante
{
    /// <summary>Tabla de alícuotas de ARCA (FEParamGetTiposIva): porcentaje → código.</summary>
    private static readonly Dictionary<decimal, int> CodigosAlicuota = new()
    {
        [0m] = 3,
        [2.5m] = 9,
        [5m] = 8,
        [10.5m] = 4,
        [21m] = 5,
        [27m] = 6,
    };

    public static SolicitudComprobante Armar(Presupuesto presupuesto, ParametrosNegocio parametros, int puntoVenta,
        long numero, DateOnly fecha)
    {
        // RF-62: la API ya lo impide antes de llegar acá; esto es la última barrera.
        if (presupuesto.Estado != EstadoPresupuesto.Final)
            throw new InvalidOperationException($"El presupuesto {presupuesto.Numero} está en Borrador: no se puede facturar.");

        var total = presupuesto.Total; // AC-16: el total enviado es el del presupuesto
        if (total <= 0)
            throw new FacturacionException(
                $"El presupuesto {presupuesto.Numero} tiene total {total.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("es-AR"))}: no se puede facturar un importe cero.");

        var (tipoDocumento, numeroDocumento) = Receptor(presupuesto.Cliente, total, parametros.TopeIdentificacion);

        if (parametros.CondicionFiscal == CondicionFiscal.Monotributo)
        {
            // RF-76, RF-85: Factura C solo con el total, sin desglose de IVA.
            return new SolicitudComprobante(puntoVenta, TipoComprobante.FacturaC, numero, fecha, tipoDocumento,
                numeroDocumento, total, ImporteNeto: total, ImporteIva: 0m, Alicuotas: []);
        }

        // RF-26, RF-43, RF-46: Factura B con neto e IVA calculados desde el importe final,
        // con la misma alícuota que el catálogo. El IVA es la diferencia, así neto + IVA = total exacto.
        if (!CodigosAlicuota.TryGetValue(parametros.AlicuotaIva, out var codigoAlicuota))
            throw new FacturacionException(
                $"La alícuota de IVA configurada ({parametros.AlicuotaIva:0.##} %) no existe en ARCA. " +
                "Corregila en Configuración (valores válidos: 0, 2,5, 5, 10,5, 21 o 27).");
        var neto = Math.Round(total / (1 + parametros.AlicuotaIva / 100), 2, MidpointRounding.AwayFromZero);
        var iva = total - neto;

        return new SolicitudComprobante(puntoVenta, TipoComprobante.FacturaB, numero, fecha, tipoDocumento,
            numeroDocumento, total, neto, iva, [new AlicuotaIva(codigoAlicuota, neto, iva)]);
    }

    /// <summary>
    /// RF-27, RF-71: hasta el tope inclusive, Consumidor Final sin identificar aunque haya
    /// DNI (AC-41, AC-77); por encima del tope, el DNI del cliente (AC-21).
    /// </summary>
    private static (TipoDocumento, long) Receptor(Cliente cliente, decimal total, decimal tope)
    {
        if (total <= tope)
            return (TipoDocumento.SinIdentificar, 0);
        if (!long.TryParse(cliente.Dni, out var dni) || dni <= 0)
            throw new FacturacionException("El total supera el tope de identificación y el presupuesto no tiene un DNI válido.");
        return (TipoDocumento.Dni, dni);
    }
}
