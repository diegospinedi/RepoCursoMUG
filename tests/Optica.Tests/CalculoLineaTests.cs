using Optica.Api.Presupuestos;

namespace Optica.Tests;

public class CalculoLineaTests
{
    private static LineaPresupuesto Linea(decimal precioUnitario, int cantidad, decimal descuento) =>
        new(1, 1, "Artículo", precioUnitario, cantidad, descuento);

    [Fact]
    public void AC26_sin_descuento_el_precio_con_descuento_es_el_unitario()
    {
        Assert.Equal(1000.00m, Linea(1000m, 1, 0m).PrecioConDescuento);
    }

    [Fact]
    public void AC35_descuento_del_10_por_ciento()
    {
        Assert.Equal(900.00m, Linea(1000m, 1, 10m).PrecioConDescuento);
    }

    [Fact]
    public void AC27_precio_final_es_precio_con_descuento_por_cantidad()
    {
        Assert.Equal(2700.00m, Linea(1000m, 3, 10m).PrecioFinal);
    }

    [Fact]
    public void AC29_redondea_mitad_hacia_arriba_y_multiplica_el_valor_redondeado()
    {
        var linea = Linea(6.67m, 3, 50m); // 6,67 × 0,5 = 3,335 sin redondear

        Assert.Equal(3.34m, linea.PrecioConDescuento);
        Assert.Equal(10.02m, linea.PrecioFinal); // 3,34 × 3, y no 3,335 × 3 = 10,005
    }

    [Fact]
    public void AC11_el_total_suma_precios_finales_redondeados()
    {
        Assert.Equal(2710.02m, CalculoLinea.Total([Linea(6.67m, 3, 50m), Linea(1000m, 3, 10m)]));
    }

    [Theory]
    [InlineData(0.005, 0.01)]
    [InlineData(0.015, 0.02)]
    [InlineData(2.675, 2.68)] // en double daría 2,67
    [InlineData(1.004, 1.00)]
    public void Redondea_a_2_decimales_mitad_hacia_arriba(decimal valor, decimal esperado)
    {
        Assert.Equal(esperado, CalculoLinea.Redondear(valor));
    }

    [Fact]
    public void Descuento_del_100_por_ciento_da_cero()
    {
        var linea = Linea(1815m, 2, 100m);

        Assert.Equal(0m, linea.PrecioConDescuento);
        Assert.Equal(0m, linea.PrecioFinal);
    }
}
