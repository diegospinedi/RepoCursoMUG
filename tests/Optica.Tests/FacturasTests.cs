using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Optica.Api.Arca;
using Optica.Api.Configuracion;
using Optica.Api.Facturacion;
using Optica.Api.Presupuestos;

namespace Optica.Tests;

public class FacturasTests : IAsyncLifetime
{
    private readonly AppDePrueba _app = new();
    private HttpClient _cliente = null!;
    private int _ultimoPresupuesto;

    public async Task InitializeAsync() => _cliente = await _app.ClienteConSesionAsync();

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Graba directo en la base un presupuesto Final y su factura, para controlar fecha y número.</summary>
    private void Facturada(string fecha, string apellido, string dni, int puntoVenta = 3, long numero = 0, decimal total = 1815m) =>
        _app.ConBase(db =>
        {
            var dia = DateOnly.Parse(fecha);
            var presupuesto = new Presupuesto(++_ultimoPresupuesto, dia, Cliente.Crear(apellido, "María", dni, null, null, null),
                [new LineaPresupuesto(1, 1, "Armazón", total, 1, 0m)]);
            // Como en la app: nace en Borrador y después pasa a Final (la base no admite
            // agregar líneas a un presupuesto que ya está en Final).
            db.Presupuestos.Add(presupuesto);
            db.SaveChanges();
            presupuesto.Finalizar();
            db.SaveChanges();
            var solicitud = new SolicitudComprobante(puntoVenta, TipoComprobante.FacturaB, numero == 0 ? _ultimoPresupuesto : numero,
                dia, TipoDocumento.SinIdentificar, 0, total, 1500m, total - 1500m, [new AlicuotaIva(5, 1500m, total - 1500m)]);
            db.Facturas.Add(new Factura(presupuesto.Id, solicitud, 21m, CondicionFiscal.ResponsableInscripto, "76123456789012",
                dia.AddDays(10)));
            return db.SaveChanges();
        });

    private async Task<List<JsonElement>> Buscar(string consulta) =>
        (await _cliente.GetFromJsonAsync<JsonElement>($"/api/facturas?{consulta}")).GetProperty("facturas").EnumerateArray().ToList();

    [Fact]
    public async Task AC22_combina_apellido_sin_acentos_y_fecha_desde()
    {
        Facturada("2026-03-10", "González", "23456789");
        Facturada("2026-03-20", "González", "23456789");
        Facturada("2026-03-20", "Gómez", "30111222");

        var resultado = await Buscar("apellido=gonzalez&desde=2026-03-15");

        var factura = Assert.Single(resultado);
        Assert.Equal("González", factura.GetProperty("apellido").GetString());
        Assert.Equal("2026-03-20", factura.GetProperty("fecha").GetString());
    }

    [Theory]
    [InlineData("34561")]
    [InlineData("0003-00034561")]
    [InlineData("3-0003")]
    public async Task AC88_busca_por_parte_del_numero_de_comprobante_ignorando_el_guion(string texto)
    {
        Facturada("2026-03-10", "González", "23456789", puntoVenta: 3, numero: 34561);
        Facturada("2026-03-10", "Gómez", "30111222", puntoVenta: 3, numero: 12);

        var resultado = await Buscar($"comprobante={Uri.EscapeDataString(texto)}");

        Assert.Equal("0003-00034561", Assert.Single(resultado).GetProperty("comprobante").GetString());
    }

    [Fact]
    public async Task AC87_busca_facturas_por_DNI_parcial()
    {
        Facturada("2026-03-10", "González", "23.456.789");
        Facturada("2026-03-10", "Gómez", "30111222");

        var resultado = await Buscar("dni=3456");

        Assert.Equal("González", Assert.Single(resultado).GetProperty("apellido").GetString());
    }

    [Fact]
    public async Task AC33_lista_las_facturas_con_los_datos_de_la_grilla_de_la_mas_nueva_a_la_mas_vieja()
    {
        Facturada("2026-03-10", "Gómez", "30111222");
        Facturada("2026-03-20", "González", "23456789");

        var resultado = await Buscar("");

        Assert.Equal(["González", "Gómez"], resultado.Select(f => f.GetProperty("apellido").GetString()));
        var primera = resultado[0];
        Assert.Equal("B", primera.GetProperty("letra").GetString());
        Assert.Equal("76123456789012", primera.GetProperty("cae").GetString());
        Assert.Equal(1815m, primera.GetProperty("importeTotal").GetDecimal());
        Assert.Equal(2, primera.GetProperty("numeroPresupuesto").GetInt32());
    }

    [Fact]
    public async Task Rechaza_un_comprobante_sin_numeros()
    {
        var respuesta = await _cliente.GetAsync("/api/facturas?comprobante=abc");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task AC32_la_API_no_ofrece_modificar_ni_eliminar_facturas()
    {
        Facturada("2026-03-10", "González", "23456789");

        foreach (var (metodo, ruta) in new[]
        {
            (HttpMethod.Put, "/api/facturas"), (HttpMethod.Delete, "/api/facturas"),
            (HttpMethod.Put, "/api/presupuestos/1/factura"), (HttpMethod.Delete, "/api/presupuestos/1/factura"),
            (HttpMethod.Patch, "/api/presupuestos/1/factura"),
        })
        {
            var respuesta = await _cliente.SendAsync(new HttpRequestMessage(metodo, ruta));
            Assert.Equal(HttpStatusCode.MethodNotAllowed, respuesta.StatusCode);
        }
    }

    [Fact]
    public async Task AC32_la_base_rechaza_modificar_o_eliminar_una_factura_con_CAE()
    {
        Facturada("2026-03-10", "González", "23456789");

        foreach (var sql in new[] { "UPDATE Facturas SET Cae = '1'", "UPDATE Facturas SET ImporteTotal = '1'", "DELETE FROM Facturas" })
        {
            var error = Assert.ThrowsAny<Exception>(() => _app.ConBase(db => db.Database.ExecuteSqlRaw(sql)));
            Assert.Contains("no se puede modificar ni eliminar", error.Message);
        }
        Assert.Equal("76123456789012", _app.ConBase(db => db.Facturas.Single().Cae));
    }

    [Fact]
    public async Task Sin_sesion_no_lista_facturas() // AC-79
    {
        Facturada("2026-03-10", "González", "23456789");

        var respuesta = await _app.CreateClient().GetAsync("/api/facturas");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.DoesNotContain("González", await respuesta.Content.ReadAsStringAsync());
    }
}
