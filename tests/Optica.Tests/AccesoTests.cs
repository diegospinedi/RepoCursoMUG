using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Optica.Api.Acceso;

namespace Optica.Tests;

public class AccesoTests : IDisposable
{
    private const string Contrasena = "clave-segura-1";

    private readonly AppDePrueba _app = new();
    private readonly HttpClient _cliente;

    public AccesoTests()
    {
        _cliente = _app.CreateClient();
        _app.CrearBase();
    }

    public void Dispose() => _app.Dispose();

    private Task<HttpResponseMessage> DefinirContrasena(string contrasena) =>
        _cliente.PostAsJsonAsync("/api/acceso/contrasena-inicial", new { contrasena });

    private Task<HttpResponseMessage> Ingresar(string contrasena) =>
        _cliente.PostAsJsonAsync("/api/acceso/ingresar", new { contrasena });

    /// <summary>Endpoint que exige sesión, usado para comprobar si la sesión sigue viva.</summary>
    private Task<HttpResponseMessage> PedidoProtegido() => _cliente.PostAsync("/api/acceso/salir", null);

    private async Task DefinirContrasenaYSalir()
    {
        (await DefinirContrasena(Contrasena)).EnsureSuccessStatusCode();
        (await PedidoProtegido()).EnsureSuccessStatusCode();
    }

    private static async Task<string> ErrorDeCampo(HttpResponseMessage respuesta, string campo)
    {
        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("errors").GetProperty(campo)[0].GetString()!;
    }

    [Fact]
    public async Task Sin_sesion_la_api_responde_401() // AC-79
    {
        await DefinirContrasenaYSalir();

        var respuesta = await PedidoProtegido();

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Estado_informa_que_falta_definir_la_contrasena()
    {
        var estado = await _cliente.GetFromJsonAsync<JsonElement>("/api/acceso/estado");

        Assert.False(estado.GetProperty("contrasenaDefinida").GetBoolean());
        Assert.False(estado.GetProperty("sesionIniciada").GetBoolean());
    }

    [Fact]
    public async Task Rechaza_una_contrasena_de_7_caracteres() // AC-57
    {
        var respuesta = await DefinirContrasena("1234567");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("al menos 8 caracteres", await ErrorDeCampo(respuesta, "contrasena"));
        Assert.False(_app.ConBase(db => db.Accesos.Any()));
    }

    [Fact]
    public async Task La_contrasena_inicial_solo_se_define_desde_la_pc_local()
    {
        _app.IpOrigen = IPAddress.Parse("192.168.1.50");

        var respuesta = await DefinirContrasena(Contrasena);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.False(_app.ConBase(db => db.Accesos.Any()));
    }

    [Fact]
    public async Task La_contrasena_inicial_no_se_puede_redefinir()
    {
        await DefinirContrasenaYSalir();

        var respuesta = await DefinirContrasena("otra-clave-123");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Ingresar(Contrasena)).StatusCode);
    }

    [Fact]
    public async Task La_contrasena_se_guarda_solo_como_hash_con_sal() // AC-80, RNF-14
    {
        await DefinirContrasenaYSalir();

        var hash = _app.ConBase(db => db.Accesos.Single().HashContrasena);

        Assert.DoesNotContain(Contrasena, hash);
        Assert.StartsWith("pbkdf2-sha256$", hash);
        Assert.NotEqual(HasherContrasena.Hashear(Contrasena), HasherContrasena.Hashear(Contrasena));
    }

    [Fact]
    public async Task Ingresa_con_la_contrasena_correcta()
    {
        await DefinirContrasenaYSalir();

        Assert.Equal(HttpStatusCode.NoContent, (await Ingresar(Contrasena)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PedidoProtegido()).StatusCode);
    }

    [Fact]
    public async Task Contrasena_incorrecta_indica_el_campo_y_los_intentos_restantes() // RF-35
    {
        await DefinirContrasenaYSalir();

        var respuesta = await Ingresar("incorrecta");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Contains("quedan 4 intentos", await ErrorDeCampo(respuesta, "contrasena"));
    }

    [Fact]
    public async Task Bloquea_5_minutos_tras_5_fallos_y_se_libera_solo() // AC-58, RNF-08
    {
        await DefinirContrasenaYSalir();
        for (var i = 0; i < 5; i++)
            await Ingresar("incorrecta");

        var bloqueado = await Ingresar(Contrasena);
        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueado.StatusCode);
        Assert.Contains("bloqueado", await ErrorDeCampo(bloqueado, "contrasena"));

        _app.Reloj.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Ingresar(Contrasena)).StatusCode);

        _app.Reloj.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.NoContent, (await Ingresar(Contrasena)).StatusCode);
    }

    [Fact]
    public async Task Un_ingreso_correcto_reinicia_el_contador_de_fallos()
    {
        await DefinirContrasenaYSalir();
        for (var i = 0; i < 4; i++)
            await Ingresar("incorrecta");
        (await Ingresar(Contrasena)).EnsureSuccessStatusCode();

        var respuesta = await Ingresar("incorrecta");

        Assert.Contains("quedan 4 intentos", await ErrorDeCampo(respuesta, "contrasena"));
    }

    [Fact]
    public async Task La_sesion_vence_tras_60_minutos_sin_actividad() // AC-59, RNF-09
    {
        await DefinirContrasenaYSalir();
        (await Ingresar(Contrasena)).EnsureSuccessStatusCode();

        _app.Reloj.Advance(TimeSpan.FromMinutes(61));

        Assert.Equal(HttpStatusCode.Unauthorized, (await PedidoProtegido()).StatusCode);
    }

    [Fact]
    public async Task La_actividad_mantiene_viva_la_sesion()
    {
        await DefinirContrasenaYSalir();
        (await Ingresar(Contrasena)).EnsureSuccessStatusCode();

        for (var i = 0; i < 3; i++)
        {
            _app.Reloj.Advance(TimeSpan.FromMinutes(50));
            var estado = await _cliente.GetFromJsonAsync<JsonElement>("/api/acceso/estado");
            Assert.True(estado.GetProperty("sesionIniciada").GetBoolean());
        }
    }
}
