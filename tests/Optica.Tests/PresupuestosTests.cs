using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Optica.Api.Presupuestos;

namespace Optica.Tests;

public class PresupuestosTests : IAsyncLifetime
{
    private readonly AppDePrueba _app = new();
    private HttpClient _cliente = null!;

    public async Task InitializeAsync() => _cliente = await _app.ClienteConSesionAsync();

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private static object Datos(string? apellido = "González", string? nombre = "María", string? dni = "23.456.789",
        string? domicilio = "Calle 42 nº 767, La Plata", string? email = "maria@example.com", string? telefono = "221 555-1234") =>
        new { cliente = new { apellido, nombre, dni, domicilio, email, telefono } };

    private Task<HttpResponseMessage> Grabar(object datos) => _cliente.PostAsJsonAsync("/api/presupuestos", datos);

    private async Task<JsonElement> GrabarOk(object datos)
    {
        var respuesta = await Grabar(datos);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Errores(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
    }

    [Fact]
    public async Task AC23_guarda_los_seis_datos_del_cliente_y_los_devuelve_al_abrirlo()
    {
        var grabado = await GrabarOk(Datos());

        var leido = await _cliente.GetFromJsonAsync<JsonElement>($"/api/presupuestos/{grabado.GetProperty("numero").GetInt32()}");
        var cliente = leido.GetProperty("cliente");
        Assert.Equal("González", cliente.GetProperty("apellido").GetString());
        Assert.Equal("María", cliente.GetProperty("nombre").GetString());
        Assert.Equal("23456789", cliente.GetProperty("dni").GetString());
        Assert.Equal("Calle 42 nº 767, La Plata", cliente.GetProperty("domicilio").GetString());
        Assert.Equal("maria@example.com", cliente.GetProperty("email").GetString());
        Assert.Equal("221 555-1234", cliente.GetProperty("telefono").GetString());
    }

    [Fact]
    public async Task AC04_al_grabar_por_primera_vez_queda_en_Borrador()
    {
        var grabado = await GrabarOk(Datos());

        Assert.Equal("Borrador", grabado.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Registra_la_fecha_de_creacion_en_hora_de_Argentina()
    {
        // Las 02:30 UTC de un día son las 23:30 del día anterior en Argentina (UTC-3).
        var manana = _app.Reloj.GetUtcNow().UtcDateTime.Date.AddDays(1);
        _app.Reloj.SetUtcNow(new DateTimeOffset(manana.AddHours(2.5), TimeSpan.Zero));
        // Adelantar el reloj vence la sesión por inactividad: se vuelve a ingresar.
        (await _cliente.PostAsJsonAsync("/api/acceso/ingresar", new { contrasena = "clave-de-prueba" })).EnsureSuccessStatusCode();

        var grabado = await GrabarOk(Datos());

        Assert.Equal(DateOnly.FromDateTime(manana.AddDays(-1)).ToString("yyyy-MM-dd"), grabado.GetProperty("fecha").GetString());
    }

    [Fact]
    public async Task AC06_asigna_el_ultimo_numero_mas_uno()
    {
        _app.ConBase(db =>
        {
            db.Presupuestos.Add(new Presupuesto(154, new DateOnly(2026, 10, 1),
                Cliente.Crear("Pérez", "Juan", "30111222", null, null, null)));
            return db.SaveChanges();
        });

        var grabado = await GrabarOk(Datos());

        Assert.Equal(155, grabado.GetProperty("numero").GetInt32());
    }

    [Fact]
    public async Task AC63_grabaciones_simultaneas_reciben_numeros_distintos_y_consecutivos()
    {
        const int sesiones = 10;
        var respuestas = await Task.WhenAll(Enumerable.Range(0, sesiones).Select(_ => Grabar(Datos())));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var numeros = new List<int>();
        foreach (var r in respuestas)
            numeros.Add((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("numero").GetInt32());
        Assert.Equal(Enumerable.Range(1, sesiones), numeros.Order());
    }

    [Fact]
    public async Task AC34_sin_DNI_no_graba_e_indica_el_campo()
    {
        var respuesta = await Grabar(Datos(dni: null));

        var errores = await Errores(respuesta);
        Assert.Equal("Ingresá el DNI del cliente", errores.GetProperty("cliente.dni")[0].GetString());
        Assert.False(_app.ConBase(db => db.Presupuestos.Any()));
    }

    [Theory]
    [InlineData("cliente.apellido", "Ingresá el apellido del cliente")]
    [InlineData("cliente.nombre", "Ingresá el nombre del cliente")]
    public async Task AC44_sin_apellido_o_nombre_no_graba_e_indica_el_campo(string campo, string mensaje)
    {
        var respuesta = await Grabar(campo == "cliente.apellido" ? Datos(apellido: " ") : Datos(nombre: ""));

        var errores = await Errores(respuesta);
        Assert.Equal(mensaje, errores.GetProperty(campo)[0].GetString());
        Assert.False(_app.ConBase(db => db.Presupuestos.Any()));
    }

    [Fact]
    public async Task AC45_graba_sin_domicilio_email_ni_telefono()
    {
        var grabado = await GrabarOk(Datos(domicilio: null, email: "", telefono: "  "));

        var cliente = grabado.GetProperty("cliente");
        Assert.Equal(JsonValueKind.Null, cliente.GetProperty("domicilio").ValueKind);
        Assert.Equal(JsonValueKind.Null, cliente.GetProperty("email").ValueKind);
        Assert.Equal(JsonValueKind.Null, cliente.GetProperty("telefono").ValueKind);
    }

    [Theory]
    [InlineData("23.456.789", "23456789")]
    [InlineData("23 456 789", "23456789")]
    [InlineData("94123456", "94123456")]
    public async Task Guarda_el_DNI_sin_puntos_ni_espacios(string ingresado, string guardado)
    {
        var grabado = await GrabarOk(Datos(dni: ingresado));

        Assert.Equal(guardado, grabado.GetProperty("cliente").GetProperty("dni").GetString());
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567890")]
    [InlineData("23A456789")]
    public async Task Rechaza_un_DNI_invalido(string dni)
    {
        var errores = await Errores(await Grabar(Datos(dni: dni)));

        Assert.Contains("entre 6 y 9 números", errores.GetProperty("cliente.dni")[0].GetString());
    }

    [Theory]
    [InlineData("maria")]
    [InlineData("maria@")]
    [InlineData("maria@example")]
    public async Task Rechaza_un_email_invalido(string email)
    {
        var errores = await Errores(await Grabar(Datos(email: email)));

        Assert.Contains("nombre@dominio.com", errores.GetProperty("cliente.email")[0].GetString());
    }

    [Fact]
    public async Task Abrir_un_presupuesto_inexistente_responde_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync("/api/presupuestos/999")).StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_no_graba_ni_devuelve_datos_de_clientes() // AC-79
    {
        var grabado = await GrabarOk(Datos());
        var anonimo = _app.CreateClient();

        var lectura = await anonimo.GetAsync($"/api/presupuestos/{grabado.GetProperty("numero").GetInt32()}");
        Assert.Equal(HttpStatusCode.Unauthorized, lectura.StatusCode);
        Assert.DoesNotContain("González", await lectura.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/presupuestos", Datos())).StatusCode);
    }
}
