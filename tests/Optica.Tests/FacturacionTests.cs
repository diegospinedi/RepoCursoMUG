using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Optica.Api.Arca;
using Optica.Api.Datos;

namespace Optica.Tests;

public class FacturacionTests : IAsyncLifetime
{
    private readonly AppDePrueba _app = new();
    private HttpClient _cliente = null!;

    public async Task InitializeAsync() => _cliente = await _app.ClienteConSesionAsync();

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private int PuntoVenta => _app.Services.GetRequiredService<OpcionesArca>().PuntoVenta;

    private async Task Configurar(string condicion = "ResponsableInscripto", decimal tope = 10_000_000m, decimal alicuota = 21m) =>
        (await _cliente.PutAsJsonAsync("/api/configuracion", new
        {
            alicuotaIva = alicuota, condicionFiscal = condicion, topeIdentificacion = tope, multiploRedondeo = 0.01m,
        })).EnsureSuccessStatusCode();

    private async Task<int> Articulo(decimal precio)
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/articulos",
            new { codigoProveedor = "X", descripcion = $"Artículo de {precio}", precioCosto = precio, margenUtilidad = 0m });
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetInt32();
    }

    /// <summary>Presupuesto con una línea por precio; en Final salvo que se pida Borrador.</summary>
    private async Task<int> Presupuesto(decimal[] precios, bool final = true)
    {
        var lineas = new List<object>();
        foreach (var precio in precios)
            lineas.Add(new { codigoArticulo = await Articulo(precio), cantidad = 1, porcentajeDescuento = 0 });
        var cliente = new { apellido = "González", nombre = "María", dni = "23456789", domicilio = (string?)null, email = (string?)null, telefono = (string?)null };

        var creado = await _cliente.PostAsJsonAsync("/api/presupuestos", new { cliente, lineas });
        var numero = (await creado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("numero").GetInt32();
        if (final)
            (await _cliente.PutAsJsonAsync($"/api/presupuestos/{numero}", new { cliente, lineas, estado = "Final" })).EnsureSuccessStatusCode();
        return numero;
    }

    private Task<int> Presupuesto(decimal precio = 1815m) => Presupuesto([precio]);

    private Task<HttpResponseMessage> Facturar(int numero) => _cliente.PostAsync($"/api/presupuestos/{numero}/factura", null);

    private static async Task<JsonElement> Json(HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    private int Facturas => _app.ConBase(db => db.Facturas.Count());
    private int Pendientes => _app.ConBase(db => db.EmisionesPendientes.Count());

    // ---- Emisión normal ----

    [Fact]
    public async Task AC18_guarda_la_factura_con_numero_CAE_y_vencimiento_vinculada_al_presupuesto()
    {
        var numero = await Presupuesto();

        var respuesta = await Facturar(numero);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var factura = await Json(respuesta);
        Assert.Equal($"{PuntoVenta:D4}-00000001", factura.GetProperty("comprobante").GetString());
        Assert.Matches("^[0-9]{14}$", factura.GetProperty("cae").GetString());
        Assert.Equal(numero, factura.GetProperty("numeroPresupuesto").GetInt32());
        Assert.True(factura.TryGetProperty("vencimientoCae", out _));

        var presupuesto = await _cliente.GetFromJsonAsync<JsonElement>($"/api/presupuestos/{numero}");
        Assert.Equal(factura.GetProperty("cae").GetString(), presupuesto.GetProperty("factura").GetProperty("cae").GetString());
        Assert.Equal(0, Pendientes);
    }

    [Fact]
    public async Task AC16_la_factura_tiene_las_lineas_del_presupuesto_y_su_total()
    {
        var numero = await Presupuesto([1815m, 3630m, 9000m]);

        var factura = await Json(await Facturar(numero));

        Assert.Equal(3, factura.GetProperty("lineas").GetArrayLength());
        Assert.Equal(14445m, factura.GetProperty("importeTotal").GetDecimal());
        var enArca = await _app.SimuladorArca.ConsultarAsync(PuntoVenta, TipoComprobante.FacturaB, 1);
        Assert.Equal(14445m, enArca!.ImporteTotal);
    }

    [Fact]
    public async Task Numera_consecutivo_segun_el_ultimo_autorizado_en_ARCA() // RF-89
    {
        var primero = await Json(await Facturar(await Presupuesto()));
        var segundo = await Json(await Facturar(await Presupuesto()));

        Assert.Equal(1, primero.GetProperty("numero").GetInt64());
        Assert.Equal(2, segundo.GetProperty("numero").GetInt64());
    }

    [Fact]
    public async Task AC31_AC47_Responsable_Inscripto_emite_Factura_B_con_neto_e_IVA()
    {
        var factura = await Json(await Facturar(await Presupuesto(1815m)));

        Assert.Equal("B", factura.GetProperty("letra").GetString());
        Assert.Equal(1500m, factura.GetProperty("importeNeto").GetDecimal());
        Assert.Equal(315m, factura.GetProperty("importeIva").GetDecimal());
    }

    [Fact]
    public async Task AC73_AC84_Monotributo_emite_Factura_C_sin_desglose()
    {
        await Configurar("Monotributo");

        var factura = await Json(await Facturar(await Presupuesto(1815m)));

        Assert.Equal("C", factura.GetProperty("letra").GetString());
        Assert.Equal(1815m, factura.GetProperty("importeTotal").GetDecimal());
        Assert.Equal(0m, factura.GetProperty("importeIva").GetDecimal());
    }

    [Fact]
    public async Task AC21_AC49_aplica_el_tope_configurado_para_identificar_al_receptor()
    {
        await Configurar(tope: 1000m);

        var factura = await Json(await Facturar(await Presupuesto(1815m)));

        Assert.Equal("DNI 23456789", factura.GetProperty("receptor").GetString());
    }

    // ---- Lo que no se factura ----

    [Fact]
    public async Task AC17_un_Borrador_se_rechaza_sin_enviar_nada_a_ARCA()
    {
        var numero = await Presupuesto([1815m], final: false);

        var respuesta = await Facturar(numero);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Empty(_app.Arca.Llamadas);
        Assert.Equal(0, Facturas);
    }

    [Fact]
    public async Task Un_presupuesto_ya_facturado_no_se_vuelve_a_facturar()
    {
        var numero = await Presupuesto();
        await Facturar(numero);

        var respuesta = await Facturar(numero);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("ya tiene la Factura B", (await Json(respuesta)).GetProperty("detail").GetString());
        Assert.Equal(1, _app.Arca.Solicitudes);
    }

    [Fact]
    public async Task Dos_clics_simultaneos_emiten_una_sola_factura()
    {
        var numero = await Presupuesto();

        var respuestas = await Task.WhenAll(Facturar(numero), Facturar(numero));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, _app.Arca.Solicitudes);
    }

    [Fact]
    public async Task Dos_presupuestos_facturados_a_la_vez_reciben_numeros_distintos()
    {
        var a = await Presupuesto();
        var b = await Presupuesto();

        var respuestas = await Task.WhenAll(Facturar(a), Facturar(b));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal([1L, 2L], _app.ConBase(db => db.Facturas.Select(f => f.Numero).OrderBy(n => n).ToList()));
    }

    [Fact]
    public async Task Una_alicuota_que_ARCA_no_admite_se_informa_sin_pedir_CAE()
    {
        await Configurar(alicuota: 19m);

        var respuesta = await Facturar(await Presupuesto());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Contains("Configuración", (await Json(respuesta)).GetProperty("detail").GetString());
        Assert.Equal(0, _app.Arca.Solicitudes);
        Assert.Equal(0, Pendientes);
    }

    // ---- Rechazo y falta de respuesta ----

    [Fact]
    public async Task AC20_si_ARCA_rechaza_no_registra_nada_muestra_el_error_y_permite_reintentar()
    {
        var numero = await Presupuesto();
        _app.ModoArca = ModoSimulador.Rechazar;

        var rechazo = await Facturar(numero);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, rechazo.StatusCode);
        var error = (await Json(rechazo)).GetProperty("erroresArca")[0];
        Assert.True(error.GetProperty("codigo").GetInt32() > 0);
        Assert.NotEmpty(error.GetProperty("mensaje").GetString()!);
        Assert.Equal(0, Facturas);
        Assert.Equal(0, Pendientes); // RF-53: no registrada

        _app.ModoArca = ModoSimulador.Normal;
        Assert.Equal(HttpStatusCode.Created, (await Facturar(numero)).StatusCode);
    }

    [Fact]
    public async Task AC60_si_ARCA_no_responde_abandona_la_espera_sin_reintentar_y_muestra_el_motivo()
    {
        var numero = await Presupuesto();
        _app.ModoArca = ModoSimulador.SinRespuesta;

        var respuesta = await Facturar(numero);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Contains("no respondió", (await Json(respuesta)).GetProperty("detail").GetString());
        Assert.Equal(1, _app.Arca.Solicitudes); // RNF-10: sin reintento automático
        Assert.Equal(0, Facturas);
        Assert.Equal(1, Pendientes); // RF-89: el número quedó registrado

        var presupuesto = await _cliente.GetFromJsonAsync<JsonElement>($"/api/presupuestos/{numero}");
        Assert.True(presupuesto.GetProperty("facturacionPendiente").GetBoolean());
    }

    [Fact]
    public async Task AC65_al_reintentar_verifica_que_no_se_autorizo_y_recien_entonces_pide_el_CAE()
    {
        var numero = await Presupuesto();
        _app.ModoArca = ModoSimulador.SinRespuesta;
        await Facturar(numero);
        _app.ModoArca = ModoSimulador.Normal;

        var respuesta = await Facturar(numero);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var llamadas = _app.Arca.Llamadas.SkipWhile(l => !l.StartsWith("Consultar")).ToList();
        Assert.Equal(["Consultar FacturaB 1", "Ultimo FacturaB", "Solicitar FacturaB 1"], llamadas);
        Assert.Equal(0, Pendientes);
    }

    [Fact]
    public async Task AC64_si_ya_estaba_autorizado_guarda_su_CAE_y_no_emite_otro()
    {
        var numero = await Presupuesto(3630m);
        _app.ModoArca = ModoSimulador.AutorizarSinResponder;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Facturar(numero)).StatusCode);
        _app.ModoArca = ModoSimulador.Normal;

        var respuesta = await Facturar(numero);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var enArca = await _app.SimuladorArca.ConsultarAsync(PuntoVenta, TipoComprobante.FacturaB, 1);
        Assert.Equal(enArca!.Cae, (await Json(respuesta)).GetProperty("cae").GetString());
        Assert.Equal(1, _app.Arca.Solicitudes); // la del primer intento: no hubo una segunda
        Assert.Equal(1, await _app.SimuladorArca.UltimoAutorizadoAsync(PuntoVenta, TipoComprobante.FacturaB));
        Assert.Equal(0, Pendientes);
    }

    [Fact]
    public async Task AC89_si_el_numero_figura_autorizado_con_otro_importe_no_emite_ni_recupera_y_avisa()
    {
        var numero = await Presupuesto(1815m);
        _app.ModoArca = ModoSimulador.SinRespuesta;
        await Facturar(numero);
        _app.ModoArca = ModoSimulador.Normal;
        // Otro sistema (o el sitio de ARCA) usó ese número para otro importe.
        await _app.SimuladorArca.SolicitarCaeAsync(new SolicitudComprobante(PuntoVenta, TipoComprobante.FacturaB, 1,
            FechaArgentina.Hoy(_app.Reloj), TipoDocumento.SinIdentificar, 0, 121m, 100m, 21m, [new AlicuotaIva(5, 100m, 21m)]));

        var respuesta = await Facturar(numero);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("revisá el punto de venta", (await Json(respuesta)).GetProperty("detail").GetString());
        Assert.Equal(0, Facturas);
        Assert.Equal(1, _app.Arca.Solicitudes); // solo el primer intento
        Assert.Equal(1, Pendientes); // sigue avisando hasta que se revise
    }

    [Fact]
    public async Task Si_ARCA_tampoco_responde_al_consultar_sigue_pendiente()
    {
        var numero = await Presupuesto();
        _app.ModoArca = ModoSimulador.SinRespuesta;
        await Facturar(numero);

        // En modo SinRespuesta la consulta sí responde; la solicitud nueva vuelve a quedar sin respuesta.
        var respuesta = await Facturar(numero);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal(1, Pendientes);
        Assert.Equal(0, Facturas);
    }

    [Fact]
    public async Task Facturar_un_presupuesto_inexistente_responde_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Facturar(999)).StatusCode);
        Assert.Empty(_app.Arca.Llamadas);
    }

    [Fact]
    public async Task Sin_sesion_no_factura() // AC-79
    {
        var numero = await Presupuesto();

        var respuesta = await _app.CreateClient().PostAsync($"/api/presupuestos/{numero}/factura", null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Empty(_app.Arca.Llamadas);
    }
}
