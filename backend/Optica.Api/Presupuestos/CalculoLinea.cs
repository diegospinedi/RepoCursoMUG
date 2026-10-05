namespace Optica.Api.Presupuestos;

/// <summary>
/// Cálculos de las líneas y del total del presupuesto (RF-13, RF-14, RF-15, RF-19, RF-44).
/// Todos los importes son finales, con IVA incluido (RF-18).
/// </summary>
public static class CalculoLinea
{
    /// <summary>2 decimales, mitad hacia arriba (RF-19): 3,335 → 3,34.</summary>
    public static decimal Redondear(decimal importe) => Math.Round(importe, 2, MidpointRounding.AwayFromZero);

    /// <summary>RF-13 + RF-44: Precio Unitario × (1 − Descuento / 100), redondeado.</summary>
    public static decimal PrecioConDescuento(decimal precioUnitario, decimal porcentajeDescuento) =>
        Redondear(precioUnitario * (1 - porcentajeDescuento / 100));

    /// <summary>RF-14 + RF-44: se multiplica el precio con descuento ya redondeado, así la cuenta exhibida cierra.</summary>
    public static decimal PrecioFinal(decimal precioConDescuento, int cantidad) =>
        Redondear(precioConDescuento * cantidad);

    /// <summary>RF-15: suma de los precios finales ya redondeados.</summary>
    public static decimal Total(IEnumerable<LineaPresupuesto> lineas) => lineas.Sum(l => l.PrecioFinal);
}
