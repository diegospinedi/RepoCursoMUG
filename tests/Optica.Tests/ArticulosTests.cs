using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Optica.Tests;

public class ArticulosTests : IAsyncLifetime
{
    private readonly AppDePrueba _app = new();
    private HttpClient _cliente = null!;

    public async Task InitializeAsync() => _cliente = await _app.ClienteConSesionAsync();

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private record Articulo(int Codigo, string CodigoProveedor, string Descripcion, decimal PrecioCosto,
        decimal MargenUtilidad, decimal PrecioVenta);

    private static object Datos(string codigoProveedor = "ABC-1", string descripcion = "Armazón acetato negro",
        decimal? precioCosto = 1210m, decimal? margenUtilidad = 50m) =>
        new { codigoProveedor, descripcion, precioCosto, margenUtilidad };

    private async Task<Articulo> Crear(object datos)
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/articulos", datos);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<Articulo>())!;
    }

    private async Task<Articulo> Modificar(int codigo, object datos)
    {
        var respuesta = await _cliente.PutAsJsonAsync($"/api/articulos/{codigo}", datos);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<Articulo>())!;
    }

    private async Task<List<string>> Buscar(string texto)
    {
        var json = await _cliente.GetFromJsonAsync<JsonElement>($"/api/articulos?texto={Uri.EscapeDataString(texto)}");
        return json.GetProperty("articulos").EnumerateArray().Select(a => a.GetProperty("descripcion").GetString()!).ToList();
    }

    private static async Task<string> ErrorDeCampo(HttpResponseMessage respuesta, string campo)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("errors").GetProperty(campo)[0].GetString()!;
    }

    [Fact]
    public async Task AC30_asigna_codigo_autonumerico_y_guarda_el_precio_de_venta()
    {
        var primero = await Crear(Datos());
        var segundo = await Crear(Datos("XYZ-9", "Lente orgánico 1.50"));

        Assert.Equal(1, primero.Codigo);
        Assert.Equal(2, segundo.Codigo);
        Assert.Equal(1815.00m, primero.PrecioVenta); // RI, IVA 21, múltiplo 0,01 (AC-36)

        var leido = await _cliente.GetFromJsonAsync<Articulo>($"/api/articulos/{primero.Codigo}");
        Assert.Equal(primero, leido);
    }

    [Fact]
    public async Task AC70_cambiar_el_margen_recalcula_el_precio_de_venta()
    {
        var articulo = await Crear(Datos());

        var modificado = await Modificar(articulo.Codigo, Datos(margenUtilidad: 60m));

        Assert.Equal(1936.00m, modificado.PrecioVenta);
    }

    [Fact]
    public async Task AC71_cambiar_el_costo_recalcula_el_precio_de_venta()
    {
        var articulo = await Crear(Datos());

        var modificado = await Modificar(articulo.Codigo, Datos(precioCosto: 2420m));

        Assert.Equal(3630.00m, modificado.PrecioVenta);
    }

    [Fact]
    public async Task AC81_el_precio_de_venta_no_se_puede_modificar()
    {
        var articulo = await Crear(Datos());

        var respuesta = await _cliente.PutAsJsonAsync($"/api/articulos/{articulo.Codigo}",
            new { codigoProveedor = "ABC-1", descripcion = "Armazón acetato negro", precioCosto = 1210m, margenUtilidad = 50m, precioVenta = 1m });

        respuesta.EnsureSuccessStatusCode();
        var leido = await _cliente.GetFromJsonAsync<Articulo>($"/api/articulos/{articulo.Codigo}");
        Assert.Equal(1815.00m, leido!.PrecioVenta);
    }

    [Fact]
    public async Task Usa_la_configuracion_vigente_al_calcular()
    {
        (await _cliente.PutAsJsonAsync("/api/configuracion",
            new { alicuotaIva = 21m, condicionFiscal = "ResponsableInscripto", topeIdentificacion = 1m, multiploRedondeo = 50m }))
            .EnsureSuccessStatusCode();

        var articulo = await Crear(Datos(precioCosto: 1210.25m)); // 1815,375 → múltiplo de 50

        Assert.Equal(1850.00m, articulo.PrecioVenta);
    }

    [Fact]
    public async Task Busca_por_descripcion_sin_distinguir_mayusculas_ni_acentos() // RF-11
    {
        await Crear(Datos("A-1", "Armazón acetato negro"));
        await Crear(Datos("L-1", "Lente orgánico 1.50"));

        Assert.Equal(["Armazón acetato negro"], await Buscar("ARMAZON"));
        Assert.Equal(["Lente orgánico 1.50"], await Buscar("organico"));
    }

    [Fact]
    public async Task Busca_por_codigo_y_por_codigo_en_el_proveedor() // RF-11
    {
        await Crear(Datos("RB-WAY", "Armazón Wayfarer"));
        await Crear(Datos("ESS-ORG", "Lente orgánico"));

        Assert.Equal(["Lente orgánico"], await Buscar("2"));        // código autonumérico 2
        Assert.Equal(["Armazón Wayfarer"], await Buscar("rb-w"));   // parte del código en el proveedor
    }

    [Fact]
    public async Task Un_numero_busca_el_codigo_exacto_y_tambien_dentro_del_texto()
    {
        await Crear(Datos("RB-2140", "Armazón Wayfarer"));
        await Crear(Datos("ESS-ORG", "Lente orgánico"));

        Assert.Equal(["Armazón Wayfarer", "Lente orgánico"], await Buscar("2"));
    }

    [Fact]
    public async Task Pagina_los_resultados_de_a_50()
    {
        for (var i = 1; i <= 55; i++)
            await Crear(Datos($"P-{i}", $"Artículo {i:D2}"));

        var pagina2 = await _cliente.GetFromJsonAsync<JsonElement>("/api/articulos?pagina=2");

        Assert.Equal(55, pagina2.GetProperty("total").GetInt32());
        Assert.Equal(5, pagina2.GetProperty("articulos").GetArrayLength());
    }

    [Fact]
    public async Task Informa_los_campos_faltantes_con_su_correccion() // RF-35
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/articulos", new { });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal(["codigoProveedor", "descripcion", "margenUtilidad", "precioCosto"],
            errores.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("Ingresá el código del artículo en el proveedor", errores.GetProperty("codigoProveedor")[0].GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Rechaza_un_costo_menor_o_igual_a_0(decimal costo)
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/articulos", Datos(precioCosto: costo));

        Assert.Equal("Ingresá un precio de costo mayor a 0", await ErrorDeCampo(respuesta, "precioCosto"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.5)]
    public async Task Rechaza_un_margen_fuera_de_0_a_100(decimal margen)
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/articulos", Datos(margenUtilidad: margen));

        Assert.Equal("Ingresá un margen de utilidad entre 0 y 100", await ErrorDeCampo(respuesta, "margenUtilidad"));
    }

    [Fact]
    public async Task Modificar_un_articulo_inexistente_responde_404()
    {
        var respuesta = await _cliente.PutAsJsonAsync("/api/articulos/999", Datos());

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_no_lista_ni_graba_articulos() // AC-79
    {
        var anonimo = _app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/articulos")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/articulos", Datos())).StatusCode);
    }
}
