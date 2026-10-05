using Optica.Api.Catalogo;
using Optica.Api.Configuracion;

namespace Optica.Tests;

public class CalculadoraPrecioVentaTests
{
    private static ParametrosNegocio Parametros(
        CondicionFiscal condicion = CondicionFiscal.ResponsableInscripto,
        decimal alicuota = 21m,
        decimal multiplo = 0.01m) => new()
    {
        CondicionFiscal = condicion,
        AlicuotaIva = alicuota,
        MultiploRedondeo = multiplo,
        TopeIdentificacion = 10_000_000m,
    };

    [Fact]
    public void AC13_costo_1000_margen_50_da_1500()
    {
        var precio = CalculadoraPrecioVenta.Calcular(1000m, 50m, Parametros());

        Assert.Equal(1500.00m, precio.VentaRedondeada);
    }

    [Fact]
    public void AC36_responsable_inscripto_desglosa_los_tres_pasos()
    {
        var precio = CalculadoraPrecioVenta.Calcular(1210m, 50m, Parametros());

        Assert.Equal(1000.00m, decimal.Round(precio.CostoSinIva!.Value, 2));
        Assert.Equal(1500.00m, decimal.Round(precio.VentaSinIva!.Value, 2));
        Assert.Equal(1815.00m, precio.VentaRedondeada);
    }

    [Fact]
    public void AC38_multiplo_50_redondea_1815_37_hacia_arriba_a_1850()
    {
        var precio = CalculadoraPrecioVenta.Calcular(1815.37m, 0m, Parametros(multiplo: 50m));

        Assert.Equal(1815.37m, precio.VentaCalculada);
        Assert.Equal(1850.00m, precio.VentaRedondeada);
    }

    [Fact]
    public void AC39_multiplo_0_01_redondea_1666_656_hacia_arriba_a_1666_66()
    {
        var precio = CalculadoraPrecioVenta.Calcular(1111.104m, 50m, Parametros());

        Assert.Equal(1666.656m, precio.VentaCalculada);
        Assert.Equal(1666.66m, precio.VentaRedondeada);
    }

    [Fact]
    public void AC72_alicuota_10_5_costo_1105_margen_50_da_1657_50()
    {
        var precio = CalculadoraPrecioVenta.Calcular(1105m, 50m, Parametros(alicuota: 10.5m));

        Assert.Equal(1000.00m, decimal.Round(precio.CostoSinIva!.Value, 2));
        Assert.Equal(1657.50m, precio.VentaRedondeada);
    }

    [Fact]
    public void AC83_monotributo_aplica_el_margen_sobre_el_costo_final()
    {
        var precio = CalculadoraPrecioVenta.Calcular(1210m, 50m, Parametros(CondicionFiscal.Monotributo));

        Assert.Equal(1815.00m, precio.VentaRedondeada);
        Assert.Null(precio.CostoSinIva);
        Assert.Null(precio.VentaSinIva);
    }

    [Theory]
    [InlineData(1500, 50, 1500)]   // ya es múltiplo: no sube
    [InlineData(1500.01, 50, 1550)]
    [InlineData(1815.37, 10, 1820)]
    [InlineData(1815.37, 1, 1816)]
    [InlineData(1815.371, 0.01, 1815.38)]
    [InlineData(0, 50, 0)]
    public void Redondea_hacia_arriba_al_multiplo(decimal valor, decimal multiplo, decimal esperado)
    {
        Assert.Equal(esperado, CalculadoraPrecioVenta.RedondearHaciaArriba(valor, multiplo));
    }

    [Fact]
    public void El_precio_redondeado_nunca_queda_por_debajo_del_calculado() // RF-56: el margen opera como piso
    {
        foreach (var multiplo in new[] { 0.01m, 0.05m, 1m, 10m, 50m, 100m })
        foreach (var costo in new[] { 0.01m, 1m, 99.99m, 1234.57m, 98765.43m })
        foreach (var margen in new[] { 0m, 12.5m, 33.33m, 50m, 100m })
        {
            var precio = CalculadoraPrecioVenta.Calcular(costo, margen, Parametros(multiplo: multiplo));

            Assert.True(precio.VentaRedondeada >= precio.VentaCalculada);
            Assert.True(precio.VentaRedondeada - precio.VentaCalculada < multiplo);
            Assert.Equal(0m, precio.VentaRedondeada % multiplo);
        }
    }

    [Theory]
    [InlineData(2)]    // con los tres pasos literales da 3,0000…01 → 3,01
    [InlineData(14)]
    [InlineData(44)]
    [InlineData(1000)] // con los tres pasos literales da 1499,9999…
    [InlineData(1105)]
    public void Un_resultado_exacto_no_suma_un_centavo_por_error_de_precision(decimal costo)
    {
        // Los tres pasos literales dejan residuos en decimal; si quedan apenas por
        // encima del valor exacto, el redondeo hacia arriba suma un centavo.
        foreach (var condicion in Enum.GetValues<CondicionFiscal>())
        {
            var precio = CalculadoraPrecioVenta.Calcular(costo, 50m, Parametros(condicion));

            Assert.Equal(costo * 1.5m, precio.VentaRedondeada);
        }
    }

    [Fact]
    public void Un_costo_negativo_redondea_hacia_arriba_sin_fallar() // RF-80 admite costos negativos desde la planilla
    {
        var precio = CalculadoraPrecioVenta.Calcular(-100m, 50m, Parametros(multiplo: 50m));

        Assert.Equal(-150m, precio.VentaRedondeada);
    }

    [Fact]
    public void Rechaza_un_multiplo_menor_a_0_01()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalculadoraPrecioVenta.Calcular(1000m, 50m, Parametros(multiplo: 0.001m)));
    }
}
