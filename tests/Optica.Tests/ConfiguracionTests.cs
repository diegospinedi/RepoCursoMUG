using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Optica.Tests;

public class ConfiguracionTests : IAsyncLifetime
{
    private readonly AppDePrueba _app = new();
    private HttpClient _cliente = null!;

    public async Task InitializeAsync() => _cliente = await _app.ClienteConSesionAsync();

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private static object Configuracion(
        decimal? alicuotaIva = 21m,
        string? condicionFiscal = "ResponsableInscripto",
        decimal? topeIdentificacion = 10_000_000m,
        decimal? multiploRedondeo = 0.01m) =>
        new { alicuotaIva, condicionFiscal, topeIdentificacion, multiploRedondeo };

    private Task<HttpResponseMessage> Grabar(object configuracion) =>
        _cliente.PutAsJsonAsync("/api/configuracion", configuracion);

    private static async Task<string> ErrorDeCampo(HttpResponseMessage respuesta, string campo)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("errors").GetProperty(campo)[0].GetString()!;
    }

    [Fact]
    public async Task Arranca_con_los_valores_iniciales()
    {
        var configuracion = await _cliente.GetFromJsonAsync<JsonElement>("/api/configuracion");

        Assert.Equal(21m, configuracion.GetProperty("alicuotaIva").GetDecimal());
        Assert.Equal("ResponsableInscripto", configuracion.GetProperty("condicionFiscal").GetString());
        Assert.Equal(0.01m, configuracion.GetProperty("multiploRedondeo").GetDecimal());
    }

    [Fact]
    public async Task Graba_y_devuelve_los_cuatro_parametros() // RF-63, AC-49
    {
        (await Grabar(Configuracion(10.5m, "Monotributo", 380_000.50m, 50m))).EnsureSuccessStatusCode();

        var configuracion = await _cliente.GetFromJsonAsync<JsonElement>("/api/configuracion");
        Assert.Equal(10.5m, configuracion.GetProperty("alicuotaIva").GetDecimal());
        Assert.Equal("Monotributo", configuracion.GetProperty("condicionFiscal").GetString());
        Assert.Equal(380_000.50m, configuracion.GetProperty("topeIdentificacion").GetDecimal());
        Assert.Equal(50m, configuracion.GetProperty("multiploRedondeo").GetDecimal());
    }

    [Fact]
    public async Task Rechaza_un_multiplo_menor_a_0_01() // AC-82, RF-72
    {
        var respuesta = await Grabar(Configuracion(multiploRedondeo: 0.001m));

        Assert.Equal("El valor mínimo es 0,01", await ErrorDeCampo(respuesta, "multiploRedondeo"));
        var guardada = await _cliente.GetFromJsonAsync<JsonElement>("/api/configuracion");
        Assert.Equal(0.01m, guardada.GetProperty("multiploRedondeo").GetDecimal());
    }

    [Fact]
    public async Task Rechaza_un_multiplo_con_mas_de_2_decimales()
    {
        var respuesta = await Grabar(Configuracion(multiploRedondeo: 0.015m));

        Assert.Contains("2 decimales", await ErrorDeCampo(respuesta, "multiploRedondeo"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public async Task Rechaza_una_alicuota_fuera_de_0_a_100(decimal alicuota)
    {
        var respuesta = await Grabar(Configuracion(alicuotaIva: alicuota));

        Assert.Contains("entre 0 y 100", await ErrorDeCampo(respuesta, "alicuotaIva"));
    }

    [Fact]
    public async Task Rechaza_una_condicion_fiscal_desconocida()
    {
        var respuesta = await Grabar(Configuracion(condicionFiscal: "Exento"));

        Assert.Contains("Responsable Inscripto o Monotributo", await ErrorDeCampo(respuesta, "condicionFiscal"));
    }

    [Fact]
    public async Task Rechaza_un_tope_negativo()
    {
        var respuesta = await Grabar(Configuracion(topeIdentificacion: -1m));

        Assert.Contains("mayor o igual a 0", await ErrorDeCampo(respuesta, "topeIdentificacion"));
    }

    [Fact]
    public async Task Informa_todos_los_campos_faltantes()
    {
        var respuesta = await Grabar(new { });

        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var campos = json.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order();
        Assert.Equal(["alicuotaIva", "condicionFiscal", "multiploRedondeo", "topeIdentificacion"], campos);
    }

    [Fact]
    public async Task Sin_sesion_no_devuelve_ni_graba_la_configuracion() // AC-79
    {
        var anonimo = _app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/configuracion")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.PutAsJsonAsync("/api/configuracion", Configuracion(alicuotaIva: 0m))).StatusCode);
    }
}
