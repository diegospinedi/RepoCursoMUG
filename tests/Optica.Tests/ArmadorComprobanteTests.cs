using Microsoft.Extensions.Time.Testing;
using Optica.Api.Arca;
using Optica.Api.Configuracion;
using Optica.Api.Facturacion;
using Optica.Api.Presupuestos;

namespace Optica.Tests;

public class ArmadorComprobanteTests
{
    private const int PuntoVenta = 3;
    private static readonly DateOnly Hoy = new(2026, 10, 5);

    private static ParametrosNegocio Parametros(CondicionFiscal condicion = CondicionFiscal.ResponsableInscripto,
        decimal alicuota = 21m, decimal tope = 100_000m) => new()
    {
        CondicionFiscal = condicion,
        AlicuotaIva = alicuota,
        TopeIdentificacion = tope,
        MultiploRedondeo = 0.01m,
    };

    /// <summary>Presupuesto Final con una línea por importe (cantidad 1, sin descuento).</summary>
    private static Presupuesto Final(params decimal[] importes)
    {
        var lineas = importes.Select((importe, i) => new LineaPresupuesto(i + 1, i + 1, $"Artículo {i + 1}", importe, 1, 0m));
        var presupuesto = new Presupuesto(155, Hoy, Cliente.Crear("González", "María", "23.456.789", null, null, null), lineas);
        presupuesto.Finalizar();
        return presupuesto;
    }

    private static SolicitudComprobante Armar(Presupuesto presupuesto, ParametrosNegocio parametros) =>
        ArmadorComprobante.Armar(presupuesto, parametros, PuntoVenta, 34561, Hoy);

    [Fact]
    public void AC31_Responsable_Inscripto_emite_Factura_B()
    {
        Assert.Equal(TipoComprobante.FacturaB, Armar(Final(1815m), Parametros()).Tipo);
    }

    [Fact]
    public void AC73_Monotributo_emite_Factura_C()
    {
        Assert.Equal(TipoComprobante.FacturaC, Armar(Final(1815m), Parametros(CondicionFiscal.Monotributo)).Tipo);
    }

    [Fact]
    public void AC16_el_total_enviado_es_el_total_del_presupuesto_con_todas_sus_lineas()
    {
        var presupuesto = Final(1815m, 3630m, 9000m);

        var solicitud = Armar(presupuesto, Parametros());

        Assert.Equal(presupuesto.Total, solicitud.ImporteTotal);
        Assert.Equal(14445m, solicitud.ImporteTotal);
        Assert.Equal(PuntoVenta, solicitud.PuntoVenta);
        Assert.Equal(34561, solicitud.Numero);
        Assert.Equal(Hoy, solicitud.Fecha);
    }

    [Fact]
    public void AC47_AC48_Factura_B_discrimina_neto_e_IVA_con_la_alicuota_configurada()
    {
        var solicitud = Armar(Final(1815m), Parametros(alicuota: 21m));

        Assert.Equal(1500.00m, solicitud.ImporteNeto);
        Assert.Equal(315.00m, solicitud.ImporteIva);
        var alicuota = Assert.Single(solicitud.Alicuotas);
        Assert.Equal(new AlicuotaIva(5, 1500.00m, 315.00m), alicuota); // 5 = 21 % en ARCA
    }

    [Fact]
    public void AC48_con_alicuota_10_5_usa_el_codigo_de_ARCA_correspondiente()
    {
        var solicitud = Armar(Final(1657.50m), Parametros(alicuota: 10.5m));

        Assert.Equal(1500.00m, solicitud.ImporteNeto);
        Assert.Equal(157.50m, solicitud.ImporteIva);
        Assert.Equal(4, solicitud.Alicuotas[0].Id);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(0.01)]
    [InlineData(99999.99)]
    [InlineData(1234.57)]
    public void Neto_mas_IVA_es_exactamente_el_total(decimal total)
    {
        var solicitud = Armar(Final(total), Parametros());

        Assert.Equal(total, solicitud.ImporteNeto + solicitud.ImporteIva);
        Assert.Equal(decimal.Round(solicitud.ImporteNeto, 2), solicitud.ImporteNeto);
        Assert.True(Math.Abs(solicitud.ImporteNeto * 0.21m - solicitud.ImporteIva) <= 0.01m);
    }

    [Fact]
    public void AC84_Factura_C_va_solo_con_el_total_sin_desglose()
    {
        var solicitud = Armar(Final(1815m), Parametros(CondicionFiscal.Monotributo));

        Assert.Equal(1815.00m, solicitud.ImporteTotal);
        Assert.Equal(1815.00m, solicitud.ImporteNeto);
        Assert.Equal(0m, solicitud.ImporteIva);
        Assert.Empty(solicitud.Alicuotas);
    }

    [Fact]
    public void AC21_si_el_total_supera_el_tope_identifica_al_receptor_con_su_DNI()
    {
        var solicitud = Armar(Final(100_000.01m), Parametros(tope: 100_000m));

        Assert.Equal(TipoDocumento.Dni, solicitud.TipoDocumento);
        Assert.Equal(23456789, solicitud.NumeroDocumento);
    }

    [Fact]
    public void AC41_si_no_supera_el_tope_es_Consumidor_Final_sin_identificar_aunque_tenga_DNI()
    {
        var solicitud = Armar(Final(50_000m), Parametros(tope: 100_000m));

        Assert.Equal(TipoDocumento.SinIdentificar, solicitud.TipoDocumento);
        Assert.Equal(0, solicitud.NumeroDocumento);
    }

    [Fact]
    public void AC77_con_el_total_igual_al_tope_es_Consumidor_Final_sin_identificar()
    {
        var solicitud = Armar(Final(100_000m), Parametros(tope: 100_000m));

        Assert.Equal(TipoDocumento.SinIdentificar, solicitud.TipoDocumento);
    }

    [Fact]
    public void AC17_RF62_no_arma_la_factura_de_un_presupuesto_en_Borrador()
    {
        var borrador = new Presupuesto(1, Hoy, Cliente.Crear("A", "B", "11111111", null, null, null),
            [new LineaPresupuesto(1, 1, "X", 100m, 1, 0m)]);

        Assert.Throws<InvalidOperationException>(() => Armar(borrador, Parametros()));
    }

    [Fact]
    public void No_factura_un_total_de_cero()
    {
        var presupuesto = new Presupuesto(1, Hoy, Cliente.Crear("A", "B", "11111111", null, null, null),
            [new LineaPresupuesto(1, 1, "X", 100m, 1, 100m)]);
        presupuesto.Finalizar();

        var error = Assert.Throws<FacturacionException>(() => Armar(presupuesto, Parametros()));

        Assert.Contains("importe cero", error.Message);
    }

    [Fact]
    public void Avisa_si_la_alicuota_configurada_no_existe_en_ARCA()
    {
        var error = Assert.Throws<FacturacionException>(() => Armar(Final(1000m), Parametros(alicuota: 19m)));

        Assert.Contains("Corregila en Configuración", error.Message);
    }

    [Fact]
    public async Task El_simulador_de_ARCA_acepta_todos_los_comprobantes_armados()
    {
        var archivo = Path.Combine(Path.GetTempPath(), $"arca-armador-{Guid.NewGuid():N}.json");
        try
        {
            var opciones = new OpcionesArca { PuntoVenta = PuntoVenta, Simulador = new OpcionesSimulador { Archivo = archivo } };
            var arca = new ArcaSimulado(opciones, new FakeTimeProvider(new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero)));
            var numeros = new Dictionary<TipoComprobante, long>();

            foreach (var condicion in Enum.GetValues<CondicionFiscal>())
            foreach (var alicuota in new[] { 21m, 10.5m, 27m })
            foreach (var total in new[] { 0.01m, 1m, 999.99m, 1815m, 100_000m, 100_000.01m, 1_234_567.89m })
            {
                var parametros = Parametros(condicion, alicuota);
                var tipo = condicion == CondicionFiscal.Monotributo ? TipoComprobante.FacturaC : TipoComprobante.FacturaB;
                var numero = numeros[tipo] = numeros.GetValueOrDefault(tipo) + 1;

                var resultado = await arca.SolicitarCaeAsync(
                    ArmadorComprobante.Armar(Final(total), parametros, PuntoVenta, numero, Hoy));

                Assert.IsType<ComprobanteAutorizado>(resultado);
            }
        }
        finally
        {
            File.Delete(archivo);
        }
    }
}
