using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Optica.Api.Catalogo;
using Xunit.Abstractions;

namespace Optica.Tests;

public class RecalculoCatalogoTests(ITestOutputHelper salida) : IAsyncLifetime
{
    private readonly AppDePrueba _app = new();
    private HttpClient _cliente = null!;

    public async Task InitializeAsync() => _cliente = await _app.ClienteConSesionAsync();

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private static object Configuracion(decimal alicuotaIva = 21m, string condicionFiscal = "ResponsableInscripto",
        decimal topeIdentificacion = 10_000_000m, decimal multiploRedondeo = 0.01m) =>
        new { alicuotaIva, condicionFiscal, topeIdentificacion, multiploRedondeo };

    private async Task<JsonElement> GrabarConfiguracion(object configuracion)
    {
        var respuesta = await _cliente.PutAsJsonAsync("/api/configuracion", configuracion);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<int> CrearArticulo(decimal precioCosto, decimal margenUtilidad)
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/articulos",
            new { codigoProveedor = "P-1", descripcion = "Artículo", precioCosto, margenUtilidad });
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetInt32();
    }

    private async Task<decimal> PrecioVenta(int codigo) =>
        (await _cliente.GetFromJsonAsync<JsonElement>($"/api/articulos/{codigo}")).GetProperty("precioVenta").GetDecimal();

    [Fact]
    public async Task AC85_cambiar_el_multiplo_recalcula_los_articulos_sin_editarlos()
    {
        var codigo = await CrearArticulo(1210m, 50m);
        Assert.Equal(1815.00m, await PrecioVenta(codigo));

        var grabada = await GrabarConfiguracion(Configuracion(multiploRedondeo: 50m));

        Assert.Equal(1850.00m, await PrecioVenta(codigo));
        Assert.Equal(1, grabada.GetProperty("preciosActualizados").GetInt32());
    }

    [Fact]
    public async Task Volver_al_multiplo_anterior_restituye_los_precios()
    {
        var codigo = await CrearArticulo(1210m, 50m);

        await GrabarConfiguracion(Configuracion(multiploRedondeo: 50m));
        await GrabarConfiguracion(Configuracion(multiploRedondeo: 0.01m));

        Assert.Equal(1815.00m, await PrecioVenta(codigo));
    }

    [Fact]
    public async Task Cambiar_solo_el_tope_no_recalcula_el_catalogo()
    {
        var codigo = await CrearArticulo(1210m, 50m);
        _app.ConBase(db =>
        {
            // Precio "viejo" forzado: si el cambio de tope recalculara, lo corregiría.
            db.Database.ExecuteSql($"UPDATE Articulos SET PrecioVenta = '1.00' WHERE Codigo = {codigo}");
            return 0;
        });

        var grabada = await GrabarConfiguracion(Configuracion(topeIdentificacion: 500_000m));

        Assert.Equal(1.00m, await PrecioVenta(codigo));
        Assert.Equal(0, grabada.GetProperty("preciosActualizados").GetInt32());
    }

    [Theory]
    [InlineData("alicuota")]
    [InlineData("condicion")]
    public async Task Cambiar_la_alicuota_o_la_condicion_tambien_recalcula(string cambio) // RF-86
    {
        var codigo = await CrearArticulo(1210m, 50m);
        _app.ConBase(db =>
        {
            db.Database.ExecuteSql($"UPDATE Articulos SET PrecioVenta = '1.00' WHERE Codigo = {codigo}");
            return 0;
        });

        await GrabarConfiguracion(cambio == "alicuota"
            ? Configuracion(alicuotaIva: 10.5m)
            : Configuracion(condicionFiscal: "Monotributo"));

        // Con las fórmulas de RF-21 y RF-84 el resultado es el mismo en ambos casos:
        // costo × (1 + margen). Lo que se verifica es que el precio se recalculó.
        Assert.Equal(1815.00m, await PrecioVenta(codigo));
    }

    [Fact]
    public async Task Recalcula_un_catalogo_de_10000_articulos() // RNF-12
    {
        _app.ConBase(db =>
        {
            var parametros = db.Parametros.Single();
            for (var i = 1; i <= 10_000; i++)
            {
                var articulo = new Articulo();
                articulo.Actualizar($"P-{i}", $"Artículo {i}", 1000m + i * 0.37m, 35m, parametros);
                db.Articulos.Add(articulo);
            }
            return db.SaveChanges();
        });

        var reloj = Stopwatch.StartNew();
        var grabada = await GrabarConfiguracion(Configuracion(multiploRedondeo: 100m));
        reloj.Stop();
        salida.WriteLine($"Recálculo de 10.000 artículos: {reloj.ElapsedMilliseconds} ms");

        Assert.Equal(10_000, grabada.GetProperty("preciosActualizados").GetInt32());
        Assert.Equal(0, _app.ConBase(db => db.Articulos.AsEnumerable().Count(a => a.PrecioVenta % 100m != 0)));
    }
}
