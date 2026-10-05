using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Optica.Api.Arca;

namespace Optica.Tests;

public class ArcaSimuladoTests : IDisposable
{
    private const int PuntoVenta = 3;
    private readonly string _archivo = Path.Combine(Path.GetTempPath(), $"arca-simulado-{Guid.NewGuid():N}.json");
    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero));

    public void Dispose() => File.Delete(_archivo);

    private DateOnly Hoy => DateOnly.FromDateTime(_reloj.GetLocalNow().DateTime);

    private OpcionesArca Opciones(ModoSimulador modo = ModoSimulador.Normal) => new()
    {
        PuntoVenta = PuntoVenta,
        TiempoEsperaSegundos = 1,
        Simulador = new OpcionesSimulador { Modo = modo, Archivo = _archivo },
    };

    private IServicioArca Arca(ModoSimulador modo = ModoSimulador.Normal)
    {
        var opciones = Opciones(modo);
        return new ArcaConTiempoLimite(new ArcaSimulado(opciones, _reloj), opciones);
    }

    private SolicitudComprobante FacturaB(long numero, decimal total = 1210m) => new(PuntoVenta, TipoComprobante.FacturaB,
        numero, Hoy, TipoDocumento.SinIdentificar, 0, total, total / 1.21m, total - total / 1.21m,
        [new AlicuotaIva(5, total / 1.21m, total - total / 1.21m)]);

    private SolicitudComprobante FacturaC(long numero, decimal total = 1815m) => new(PuntoVenta, TipoComprobante.FacturaC,
        numero, Hoy, TipoDocumento.Dni, 23456789, total, total, 0m, []);

    [Fact]
    public async Task Sin_comprobantes_el_ultimo_autorizado_es_0()
    {
        Assert.Equal(0, await Arca().UltimoAutorizadoAsync(PuntoVenta, TipoComprobante.FacturaB));
    }

    [Fact]
    public async Task Autoriza_el_proximo_numero_con_CAE_y_vencimiento()
    {
        var resultado = await Arca().SolicitarCaeAsync(FacturaB(1));

        var autorizado = Assert.IsType<ComprobanteAutorizado>(resultado);
        Assert.Matches("^[0-9]{14}$", autorizado.Cae);
        Assert.Equal(Hoy.AddDays(ArcaSimulado.DiasValidezCae), autorizado.VencimientoCae);
        Assert.Equal(1, await Arca().UltimoAutorizadoAsync(PuntoVenta, TipoComprobante.FacturaB));
    }

    [Fact]
    public async Task Numera_por_separado_cada_tipo_de_comprobante()
    {
        await Arca().SolicitarCaeAsync(FacturaB(1));

        Assert.IsType<ComprobanteAutorizado>(await Arca().SolicitarCaeAsync(FacturaC(1)));
        Assert.Equal(1, await Arca().UltimoAutorizadoAsync(PuntoVenta, TipoComprobante.FacturaC));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public async Task Rechaza_un_numero_que_no_es_el_proximo(long numero)
    {
        var resultado = Assert.IsType<ComprobanteRechazado>(await Arca().SolicitarCaeAsync(FacturaB(numero)));

        Assert.Contains(resultado.Errores, e => e.Codigo == 10016);
    }

    [Fact]
    public async Task Conserva_los_comprobantes_entre_reinicios_y_permite_consultarlos()
    {
        var autorizado = (ComprobanteAutorizado)await Arca().SolicitarCaeAsync(FacturaB(1, 3630m));

        var consultado = await Arca().ConsultarAsync(PuntoVenta, TipoComprobante.FacturaB, 1); // otra instancia

        Assert.NotNull(consultado);
        Assert.Equal(3630m, consultado.ImporteTotal);
        Assert.Equal(autorizado.Cae, consultado.Cae);
        Assert.Null(await Arca().ConsultarAsync(PuntoVenta, TipoComprobante.FacturaB, 2));
    }

    [Fact]
    public async Task Rechaza_totales_que_no_cierran()
    {
        var solicitud = FacturaB(1) with { ImporteTotal = 1211m };

        var resultado = Assert.IsType<ComprobanteRechazado>(await Arca().SolicitarCaeAsync(solicitud));

        Assert.Contains(resultado.Errores, e => e.Codigo == 10048);
    }

    [Fact]
    public async Task Rechaza_una_Factura_C_con_IVA_y_una_Factura_B_sin_alicuotas()
    {
        var facturaCConIva = FacturaC(1) with { ImporteNeto = 1500m, ImporteIva = 315m, Alicuotas = [new AlicuotaIva(5, 1500m, 315m)] };
        var facturaBSinAlicuotas = FacturaB(1) with { ImporteNeto = 1210m, ImporteIva = 0m, Alicuotas = [] };

        Assert.Contains(Assert.IsType<ComprobanteRechazado>(await Arca().SolicitarCaeAsync(facturaCConIva)).Errores, e => e.Codigo == 10071);
        Assert.Contains(Assert.IsType<ComprobanteRechazado>(await Arca().SolicitarCaeAsync(facturaBSinAlicuotas)).Errores, e => e.Codigo == 10070);
    }

    [Fact]
    public async Task Modo_Rechazar_devuelve_un_error_con_codigo_y_descripcion() // AC-20
    {
        var resultado = Assert.IsType<ComprobanteRechazado>(await Arca(ModoSimulador.Rechazar).SolicitarCaeAsync(FacturaB(1)));

        var error = Assert.Single(resultado.Errores);
        Assert.True(error.Codigo > 0);
        Assert.NotEmpty(error.Mensaje);
        Assert.Equal(0, await Arca().UltimoAutorizadoAsync(PuntoVenta, TipoComprobante.FacturaB));
    }

    [Fact]
    public async Task Sin_respuesta_se_abandona_al_vencer_el_tiempo_y_no_queda_autorizado() // RNF-10, AC-60
    {
        var excepcion = await Assert.ThrowsAsync<ArcaNoDisponibleException>(() =>
            Arca(ModoSimulador.SinRespuesta).SolicitarCaeAsync(FacturaB(1)));

        Assert.Contains("no respondió en 1 segundos", excepcion.Message);
        Assert.Null(await Arca().ConsultarAsync(PuntoVenta, TipoComprobante.FacturaB, 1));
    }

    [Fact]
    public async Task Autorizar_sin_responder_deja_el_comprobante_autorizado_en_ARCA() // base de AC-64
    {
        await Assert.ThrowsAsync<ArcaNoDisponibleException>(() =>
            Arca(ModoSimulador.AutorizarSinResponder).SolicitarCaeAsync(FacturaB(1, 3630m)));

        var consultado = await Arca().ConsultarAsync(PuntoVenta, TipoComprobante.FacturaB, 1);
        Assert.NotNull(consultado);
        Assert.Equal(3630m, consultado.ImporteTotal);
    }

    [Theory]
    [InlineData("Homologacion")]
    [InlineData("Produccion")]
    public void Solo_se_puede_configurar_el_entorno_simulado(string entorno)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Arca:Entorno"] = entorno }).Build();

        var servicios = new ServiceCollection().AddSingleton<IConfiguration>(configuracion).AddArca().BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(() => servicios.GetRequiredService<IServicioArca>());

        Assert.Contains("Simulado", error.Message);
    }

    [Fact]
    public void La_API_usa_la_configuracion_de_ARCA_de_cada_entorno_de_prueba()
    {
        using var app = new AppDePrueba();

        var opciones = app.Services.GetRequiredService<OpcionesArca>();

        Assert.Contains("arca-simulado-test-", opciones.Simulador.Archivo);
    }
}
