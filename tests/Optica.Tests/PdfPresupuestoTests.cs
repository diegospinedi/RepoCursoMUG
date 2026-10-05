using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Optica.Tests;

public class PdfPresupuestoTests : IAsyncLifetime
{
    private readonly AppDePrueba _app = new();
    private HttpClient _cliente = null!;
    private int _numero;

    public async Task InitializeAsync()
    {
        _cliente = await _app.ClienteConSesionAsync();
        var articulo = await _cliente.PostAsJsonAsync("/api/articulos",
            new { codigoProveedor = "ABC-1", descripcion = "Armazón acetato negro", precioCosto = 1210m, margenUtilidad = 50m });
        var codigo = (await articulo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetInt32();
        var presupuesto = await _cliente.PostAsJsonAsync("/api/presupuestos", new
        {
            cliente = new { apellido = "González", nombre = "María", dni = "23456789", domicilio = "Calle 42 nº 767", email = "maria@example.com", telefono = "221 555-1234" },
            lineas = new[] { new { codigoArticulo = codigo, cantidad = 2, porcentajeDescuento = 10 } },
        });
        _numero = (await presupuesto.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("numero").GetInt32();
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private async Task Finalizar()
    {
        var actual = await _cliente.GetFromJsonAsync<JsonElement>($"/api/presupuestos/{_numero}");
        var lineas = actual.GetProperty("lineas").EnumerateArray().Select(l => new
        {
            codigoArticulo = l.GetProperty("codigoArticulo").GetInt32(),
            cantidad = l.GetProperty("cantidad").GetInt32(),
            porcentajeDescuento = l.GetProperty("porcentajeDescuento").GetDecimal(),
        });
        (await _cliente.PutAsJsonAsync($"/api/presupuestos/{_numero}",
            new { cliente = actual.GetProperty("cliente"), lineas, estado = "Final" })).EnsureSuccessStatusCode();
    }

    private async Task<(HttpResponseMessage Respuesta, PdfDocument Pdf)> DescargarFinal()
    {
        await Finalizar();
        var respuesta = await _cliente.GetAsync($"/api/presupuestos/{_numero}/pdf");
        respuesta.EnsureSuccessStatusCode();
        return (respuesta, PdfDocument.Open(await respuesta.Content.ReadAsByteArrayAsync()));
    }

    [Fact]
    public async Task AC08_en_Borrador_la_API_rechaza_el_pedido_sin_generar_el_PDF()
    {
        var respuesta = await _cliente.GetAsync($"/api/presupuestos/{_numero}/pdf");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.NotEqual("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AC09_en_Final_descarga_el_PDF_como_archivo()
    {
        var (respuesta, pdf) = await DescargarFinal();

        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", respuesta.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal($"Presupuesto-{_numero}.pdf", respuesta.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.True(pdf.NumberOfPages >= 1);
    }

    [Fact]
    public async Task AC37_muestra_la_leyenda_de_precios_finales_con_IVA_incluido()
    {
        var (_, pdf) = await DescargarFinal();

        Assert.Contains("Precios finales, IVA incluido", Texto(pdf));
    }

    [Fact]
    public async Task Incluye_numero_cliente_lineas_y_total_con_formato_argentino()
    {
        var (_, pdf) = await DescargarFinal();
        var texto = Texto(pdf);

        Assert.Contains($"Nº {_numero}", texto);
        Assert.Contains("González, María", texto);
        Assert.Contains("DNI 23.456.789", texto);
        Assert.Contains("Armazón acetato negro", texto);
        Assert.Contains("$ 1.815,00", texto);   // precio unitario
        Assert.Contains("$ 1.633,50", texto);   // con 10 % de descuento
        Assert.Contains("$ 3.267,00", texto);   // final y total
    }

    [Fact]
    public async Task AC28_no_discrimina_IVA()
    {
        var (_, pdf) = await DescargarFinal();
        var texto = string.Concat(pdf.GetPages().Select(p => p.Text)).Replace("Precios finales, IVA incluido", "");

        Assert.DoesNotContain("IVA", texto);
        Assert.DoesNotContain("Neto", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AC02_AC33_lleva_el_logo_el_color_primario_y_las_fuentes_de_la_marca()
    {
        var (_, pdf) = await DescargarFinal();
        var pagina = pdf.GetPage(1);

        Assert.NotEmpty(pagina.GetImages()); // logo

        var titulo = pagina.Letters.First(l => l.Value == "P" && l.FontName.Contains("Montserrat"));
        var (r, g, b) = titulo.Color.ToRGBValues();
        Assert.Equal((0x09, 0x03, 0xA0), ((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255)));

        var fuentes = pagina.Letters.Select(l => l.FontName).Distinct().ToList();
        Assert.Contains(fuentes, f => f.Contains("Montserrat"));
        Assert.Contains(fuentes, f => f.Contains("Barlow"));
    }

    [Fact]
    public async Task Presupuesto_inexistente_responde_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync("/api/presupuestos/999/pdf")).StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_no_entrega_el_PDF() // AC-79
    {
        await Finalizar();

        var respuesta = await _app.CreateClient().GetAsync($"/api/presupuestos/{_numero}/pdf");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    private static string Texto(PdfDocument pdf) =>
        string.Join("\n", pdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text)) + "\n" +
        string.Join("\n", pdf.GetPages().Select(p => p.Text));
}
