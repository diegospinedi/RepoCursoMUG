using Optica.Api.Configuracion;

namespace Optica.Api.Catalogo;

/// <summary>
/// Desglose del cálculo del precio de venta. CostoSinIva y VentaSinIva solo
/// tienen sentido con Responsable Inscripto (RF-21 pasos a y b); con
/// Monotributo son null (RF-84).
/// </summary>
public record PrecioVenta(
    decimal? CostoSinIva,
    decimal? VentaSinIva,
    decimal VentaCalculada,
    decimal VentaRedondeada);

/// <summary>
/// Calcula el precio de venta final al consumidor, con IVA incluido (RF-21, RF-84),
/// y lo redondea hacia arriba al múltiplo comercial configurado (RF-56).
/// </summary>
public static class CalculadoraPrecioVenta
{
    public static PrecioVenta Calcular(decimal precioCosto, decimal margenUtilidad, ParametrosNegocio parametros)
    {
        if (parametros.MultiploRedondeo < 0.01m)
            throw new ArgumentOutOfRangeException(nameof(parametros), "El múltiplo de redondeo mínimo es 0,01 (RF-72).");

        // Con una alícuota única, los tres pasos de RF-21 se reducen a
        // costo × (1 + margen / 100): el IVA que se descuenta en (a) es el mismo que
        // se suma en (c). Se calcula así porque es exacto en decimal; aplicar los
        // tres pasos deja residuos que, si caen apenas por encima de un múltiplo, el
        // redondeo hacia arriba convierte en un centavo de más (costo 2, margen 50 %
        // e IVA 21 % da 3,0000…01 → 3,01 en lugar de 3,00).
        var ventaCalculada = precioCosto * (1 + margenUtilidad / 100);

        decimal? costoSinIva = null, ventaSinIva = null;
        if (parametros.CondicionFiscal == CondicionFiscal.ResponsableInscripto)
        {
            var factorIva = 1 + parametros.AlicuotaIva / 100;
            costoSinIva = precioCosto / factorIva;            // RF-21 (a)
            ventaSinIva = ventaCalculada / factorIva;          // RF-21 (b), sin redondear
        }

        return new PrecioVenta(costoSinIva, ventaSinIva, ventaCalculada,
            RedondearHaciaArriba(ventaCalculada, parametros.MultiploRedondeo));
    }

    /// <summary>
    /// Lleva el valor al múltiplo inmediato superior (o lo deja si ya es múltiplo),
    /// de modo que el precio nunca quede por debajo del calculado (RF-56).
    /// </summary>
    public static decimal RedondearHaciaArriba(decimal valor, decimal multiplo) =>
        Math.Ceiling(valor / multiplo) * multiplo;
}
