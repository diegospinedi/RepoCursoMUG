using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Optica.Api.Arca;
using Optica.Api.Configuracion;
using Optica.Api.Facturacion;
using Optica.Api.Presupuestos;
using UglyToad.PdfPig;

namespace Optica.Tests;

public class PdfFacturaTests : IAsyncLifetime
{
    private readonly AppDePrueba _app = new();
    private HttpClient _cliente = null!;

    public async Task InitializeAsync() => _cliente = await _app.ClienteConSesionAsync();

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private async Task Configurar(string condicion = "ResponsableInscripto", decimal tope = 10_000_000m) =>
        (await _cliente.PutAsJsonAsync("/api/configuracion", new
        {
            alicuotaIva = 21m, condicionFiscal = condicion, topeIdentificacion = tope, multiploRedondeo = 0.01m,
        })).EnsureSuccessStatusCode();

    /// <summary>Crea, finaliza y factura un presupuesto de una línea; devuelve su número.</summary>
    private async Task<int> Facturado(decimal precio = 1815m, int cantidad = 2)
    {
        var articulo = await _cliente.PostAsJsonAsync("/api/articulos",
            new { codigoProveedor = "X", descripcion = "Armazón acetato negro", precioCosto = precio, margenUtilidad = 0m });
        var codigo = (await articulo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetInt32();
        var cliente = new { apellido = "González", nombre = "María", dni = "23456789", domicilio = "Calle 7 nº 1", email = (string?)null, telefono = (string?)null };
        var lineas = new[] { new { codigoArticulo = codigo, cantidad, porcentajeDescuento = 0 } };
        var creado = await _cliente.PostAsJsonAsync("/api/presupuestos", new { cliente, lineas });
        var numero = (await creado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("numero").GetInt32();
        (await _cliente.PutAsJsonAsync($"/api/presupuestos/{numero}", new { cliente, lineas, estado = "Final" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await _cliente.PostAsync($"/api/presupuestos/{numero}/factura", null)).StatusCode);
        return numero;
    }

    private async Task<(HttpResponseMessage Respuesta, PdfDocument Pdf, string Texto)> Descargar(int numero)
    {
        var respuesta = await _cliente.GetAsync($"/api/presupuestos/{numero}/factura/pdf");
        respuesta.EnsureSuccessStatusCode();
        var pdf = PdfDocument.Open(await respuesta.Content.ReadAsByteArrayAsync());
        // Palabra por palabra: Page.Text pega los renglones sin espacio ("autorizadopor").
        return (respuesta, pdf, string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text)));
    }

    [Fact]
    public async Task AC19_contiene_todos_los_datos_de_RF30()
    {
        var numero = await Facturado(1815m, 2);
        var factura = await _cliente.GetFromJsonAsync<JsonElement>($"/api/presupuestos/{numero}/factura");

        var (respuesta, pdf, texto) = await Descargar(numero);

        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"Factura-B-{factura.GetProperty("comprobante").GetString()}.pdf", respuesta.Content.Headers.ContentDisposition?.FileNameStar);
        // Emisor
        Assert.Contains("Óptica Sistema SRL", texto);
        Assert.Contains("Calle 42 nº 767, La Plata", texto);
        Assert.Contains("CUIT: 30-71234567-1", texto);
        Assert.Contains("IVA Responsable Inscripto", texto);
        // Comprobante
        Assert.Contains("FACTURA", texto);
        Assert.Contains("Cód. 06", texto);
        var puntoVenta = factura.GetProperty("puntoVenta").GetInt32();
        Assert.Contains($"Punto de venta: {puntoVenta:D4}", texto);
        Assert.Contains("Comp. Nro: 00000001", texto);
        Assert.Contains($"Fecha de emisión: {DateOnly.Parse(factura.GetProperty("fecha").GetString()!):dd/MM/yyyy}", texto);
        // Receptor (RF-27: total bajo el tope)
        Assert.Contains("Receptor: Consumidor Final", texto);
        // Líneas y total
        Assert.Contains("Armazón acetato negro", texto);
        Assert.Contains("$ 3.630,00", texto);
        // CAE, vencimiento y QR
        Assert.Contains($"CAE N°: {factura.GetProperty("cae").GetString()}", texto);
        Assert.Contains($"Fecha de vto. de CAE: {DateOnly.Parse(factura.GetProperty("vencimientoCae").GetString()!):dd/MM/yyyy}", texto);
        Assert.Equal(2, pdf.GetPage(1).GetImages().Count()); // logo y QR
    }

    [Fact]
    public async Task Factura_B_informa_el_IVA_contenido_Ley_27743()
    {
        var (_, _, texto) = await Descargar(await Facturado(1815m, 1));

        Assert.Contains(PdfFactura.LeyendaTransparencia, texto);
        Assert.Contains("$ 315,00", texto);
    }

    [Fact]
    public async Task Factura_C_de_monotributista_no_informa_IVA()
    {
        await Configurar("Monotributo");

        var (_, _, texto) = await Descargar(await Facturado());

        Assert.Contains("Cód. 11", texto);
        Assert.Contains("Responsable Monotributo", texto);
        Assert.DoesNotContain("IVA contenido", texto);
    }

    [Fact]
    public async Task AC21_sobre_el_tope_identifica_al_receptor_con_DNI_y_nombre()
    {
        await Configurar(tope: 1000m);

        var (_, _, texto) = await Descargar(await Facturado());

        Assert.Contains("DNI: 23.456.789", texto);
        Assert.Contains("González, María", texto);
    }

    [Fact]
    public async Task Con_el_simulador_el_PDF_dice_que_no_tiene_validez_fiscal()
    {
        var (_, _, texto) = await Descargar(await Facturado());

        Assert.Contains(PdfFactura.LeyendaSimulado, texto);
        Assert.Contains("Comprobante simulado: no fue autorizado", texto); // el renglón siguiente se intercala con el CAE al extraer
        Assert.DoesNotContain("Comprobante autorizado por ARCA", texto);
    }

    [Fact]
    public async Task Sin_factura_responde_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync("/api/presupuestos/999/factura/pdf")).StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_no_entrega_el_PDF() // AC-79
    {
        var numero = await Facturado();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _app.CreateClient().GetAsync($"/api/presupuestos/{numero}/factura/pdf")).StatusCode);
    }

    [Fact]
    public void El_QR_codifica_los_datos_del_comprobante_segun_la_especificacion_de_ARCA()
    {
        var presupuesto = new Presupuesto(1, new DateOnly(2026, 10, 5), Cliente.Crear("A", "B", "23456789", null, null, null),
            [new LineaPresupuesto(1, 1, "X", 1815m, 1, 0m)]);
        presupuesto.Finalizar();
        var solicitud = ArmadorComprobante.Armar(presupuesto,
            new ParametrosNegocio { AlicuotaIva = 21m, CondicionFiscal = CondicionFiscal.ResponsableInscripto, TopeIdentificacion = 1000m, MultiploRedondeo = 0.01m },
            3, 34561, new DateOnly(2026, 10, 5));
        var factura = new Factura(1, solicitud, 21m, CondicionFiscal.ResponsableInscripto, "76123456789012", new DateOnly(2026, 10, 15));

        var url = QrArca.Url(factura, 30712345671);

        Assert.StartsWith("https://www.afip.gob.ar/fe/qr/?p=", url);
        var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(url[QrArca.UrlBase.Length..]))).RootElement;
        Assert.Equal(1, json.GetProperty("ver").GetInt32());
        Assert.Equal("2026-10-05", json.GetProperty("fecha").GetString());
        Assert.Equal(30712345671, json.GetProperty("cuit").GetInt64());
        Assert.Equal(3, json.GetProperty("ptoVta").GetInt32());
        Assert.Equal(6, json.GetProperty("tipoCmp").GetInt32());
        Assert.Equal(34561, json.GetProperty("nroCmp").GetInt64());
        Assert.Equal(1815m, json.GetProperty("importe").GetDecimal());
        Assert.Equal("PES", json.GetProperty("moneda").GetString());
        Assert.Equal(1, json.GetProperty("ctz").GetInt32());
        Assert.Equal(96, json.GetProperty("tipoDocRec").GetInt32());
        Assert.Equal(23456789, json.GetProperty("nroDocRec").GetInt64());
        Assert.Equal("E", json.GetProperty("tipoCodAut").GetString());
        Assert.Equal(76123456789012, json.GetProperty("codAut").GetInt64());
    }
}
