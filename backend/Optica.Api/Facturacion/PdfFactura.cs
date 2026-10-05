using System.Globalization;
using Optica.Api.Arca;
using Optica.Api.Configuracion;
using Optica.Api.Presupuestos;
using Optica.Api.Recursos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Optica.Api.Facturacion;

/// <summary>
/// PDF de la factura con los datos de RF-30 (lista a validar con el contador de la óptica),
/// el logo y los colores de la marca (RF-55).
/// </summary>
public static class PdfFactura
{
    public const string LeyendaSimulado = "COMPROBANTE SIMULADO — SIN VALIDEZ FISCAL";
    public const string LeyendaTransparencia = "Régimen de Transparencia Fiscal al Consumidor (Ley 27.743)";

    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    public static byte[] Generar(Factura factura, Presupuesto presupuesto, OpcionesEmisor emisor, bool simulado) =>
        Crear(factura, presupuesto, emisor, simulado).GeneratePdf();

    public static IDocument Crear(Factura factura, Presupuesto presupuesto, OpcionesEmisor emisor, bool simulado)
    {
        var qr = QrArca.Png(QrArca.Url(factura, emisor.CuitNumerico));

        return Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(1.5f, Unit.Centimetre);
            pagina.PageColor(Marca.ColorFondo);
            pagina.DefaultTextStyle(t => t.FontFamily(Marca.FuenteTexto).FontSize(9.5f).FontColor(Colors.Grey.Darken4));

            if (simulado)
            {
                // Un CAE del simulador no tiene validez: el PDF no puede parecer una factura real.
                pagina.Background().AlignCenter().AlignMiddle().Rotate(-30).Column(c =>
                {
                    foreach (var renglon in new[] { "COMPROBANTE SIMULADO", "SIN VALIDEZ FISCAL" })
                        c.Item().AlignCenter().Text(renglon).FontFamily(Marca.FuenteTitulos).Bold().FontSize(34)
                            .FontColor(Colors.Red.Lighten4);
                });
            }

            pagina.Header().Element(e => Encabezado(e, factura, emisor, simulado));
            pagina.Content().PaddingVertical(10).Column(columna =>
            {
                columna.Spacing(10);
                columna.Item().Element(e => Receptor(e, factura, presupuesto.Cliente));
                columna.Item().Element(e => Lineas(e, presupuesto));
                columna.Item().Element(e => Totales(e, factura));
            });
            pagina.Footer().Element(e => PieCae(e, factura, qr, simulado));
        }));
    }

    private static void Encabezado(IContainer contenedor, Factura f, OpcionesEmisor emisor, bool simulado)
    {
        contenedor.Column(columna =>
        {
            if (simulado)
                columna.Item().Background(Colors.Red.Lighten4).Padding(4).AlignCenter().Text(LeyendaSimulado)
                    .FontFamily(Marca.FuenteTitulos).Bold().FontSize(9).FontColor(Colors.Red.Darken3);

            columna.Item().AlignRight().Text("ORIGINAL").FontFamily(Marca.FuenteTitulos).SemiBold().FontSize(8)
                .FontColor(Colors.Grey.Darken1);
            columna.Item().Border(1).BorderColor(Marca.ColorPrimario).Row(fila =>
            {
                fila.RelativeItem().Padding(8).Column(c =>
                {
                    c.Spacing(2);
                    c.Item().Width(120).Image(Marca.Logo).FitWidth();
                    c.Item().PaddingTop(4).Text(emisor.RazonSocial).FontFamily(Marca.FuenteTitulos).Bold().FontSize(11)
                        .FontColor(Marca.ColorPrimario);
                    c.Item().Text($"Domicilio comercial: {emisor.Domicilio}");
                    c.Item().Text($"Condición frente al IVA: {CondicionIva(f.CondicionFiscalEmisor)}").SemiBold();
                });

                // Recuadro con la letra del comprobante, como exige ARCA.
                fila.ConstantItem(62).BorderLeft(1).BorderRight(1).BorderColor(Marca.ColorPrimario).Column(c =>
                {
                    c.Item().Background(Marca.ColorPrimario).PaddingVertical(6).AlignCenter()
                        .Text(Factura.Letra(f.Tipo)).FontFamily(Marca.FuenteTitulos).Bold().FontSize(30).FontColor(Marca.ColorFondo);
                    c.Item().PaddingVertical(3).AlignCenter().Text($"Cód. {(int)f.Tipo:D2}").FontSize(8).SemiBold();
                });

                fila.RelativeItem().Padding(8).Column(c =>
                {
                    c.Spacing(2);
                    c.Item().Text("FACTURA").FontFamily(Marca.FuenteTitulos).Bold().FontSize(18).FontColor(Marca.ColorPrimario);
                    c.Item().Text(t =>
                    {
                        t.Span("Punto de venta: ").SemiBold();
                        t.Span($"{f.PuntoVenta:D4}   ");
                        t.Span("Comp. Nro: ").SemiBold();
                        t.Span($"{f.Numero:D8}");
                    });
                    c.Item().Text(t =>
                    {
                        t.Span("Fecha de emisión: ").SemiBold();
                        t.Span(f.Fecha.ToString("dd/MM/yyyy", Argentina));
                    });
                    c.Item().PaddingTop(4).Text($"CUIT: {emisor.CuitFormateado}");
                    c.Item().Text($"Ingresos Brutos: {emisor.IngresosBrutos}");
                    c.Item().Text($"Inicio de actividades: {emisor.InicioActividades}");
                });
            });
        });
    }

    /// <summary>RF-27, RF-71: Consumidor Final sin identificar o identificado con DNI.</summary>
    private static void Receptor(IContainer contenedor, Factura f, Cliente cliente)
    {
        contenedor.Background(Marca.ColorPrimarioSuave).Padding(8).Column(c =>
        {
            c.Spacing(2);
            if (f.TipoDocumentoReceptor == TipoDocumento.Dni)
            {
                c.Item().Text(t =>
                {
                    t.Span("DNI: ").SemiBold();
                    t.Span(f.NumeroDocumentoReceptor.ToString("#,0", Argentina));
                    t.Span("   Apellido y nombre: ").SemiBold();
                    t.Span($"{cliente.Apellido}, {cliente.Nombre}");
                });
                if (cliente.Domicilio is not null)
                    c.Item().Text(t =>
                    {
                        t.Span("Domicilio: ").SemiBold();
                        t.Span(cliente.Domicilio);
                    });
            }
            else
            {
                c.Item().Text("Receptor: Consumidor Final").SemiBold();
            }
            c.Item().Text(t =>
            {
                t.Span("Condición frente al IVA: ").SemiBold();
                t.Span("Consumidor Final");
                t.Span("   Condición de venta: ").SemiBold();
                t.Span("Contado");
            });
        });
    }

    private static void Lineas(IContainer contenedor, Presupuesto p)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(c =>
            {
                c.ConstantColumn(44);
                c.RelativeColumn(5);
                c.ConstantColumn(40);
                c.RelativeColumn(1.6f);
                c.ConstantColumn(44);
                c.RelativeColumn(1.7f);
            });
            tabla.Header(h =>
            {
                foreach (var (titulo, derecha) in new[]
                {
                    ("Código", true), ("Descripción", false), ("Cant.", true), ("Precio unit.", true), ("Desc.", true), ("Importe", true),
                })
                {
                    var celda = h.Cell().Background(Marca.ColorPrimario).PaddingVertical(5).PaddingHorizontal(4);
                    (derecha ? celda.AlignRight() : celda).Text(titulo.ToUpperInvariant()).FontFamily(Marca.FuenteTitulos)
                        .SemiBold().FontSize(7.5f).FontColor(Marca.ColorFondo);
                }
            });
            foreach (var l in p.Lineas.OrderBy(l => l.Orden))
            {
                Celda(tabla, l.CodigoArticulo.ToString(), true);
                Celda(tabla, l.Descripcion, false);
                Celda(tabla, l.Cantidad.ToString(), true);
                Celda(tabla, Importe(l.PrecioUnitario), true);
                Celda(tabla, l.PorcentajeDescuento.ToString("0.##", Argentina) + " %", true);
                Celda(tabla, Importe(l.PrecioFinal), true);
            }
        });
    }

    private static void Totales(IContainer contenedor, Factura f)
    {
        contenedor.AlignRight().Width(260).Column(c =>
        {
            c.Item().BorderTop(1.5f).BorderColor(Marca.ColorPrimario).PaddingTop(6).Row(fila =>
            {
                fila.RelativeItem().Text("TOTAL").FontFamily(Marca.FuenteTitulos).Bold().FontSize(13).FontColor(Marca.ColorPrimario);
                fila.RelativeItem().AlignRight().Text(Importe(f.ImporteTotal)).FontFamily(Marca.FuenteTitulos).Bold().FontSize(13)
                    .FontColor(Marca.ColorPrimario);
            });
            if (f.Tipo == TipoComprobante.FacturaB)
            {
                // Ley 27.743: en la Factura B a consumidor final se informa el IVA contenido.
                c.Item().PaddingTop(6).Text(LeyendaTransparencia).FontSize(8).SemiBold();
                c.Item().Row(fila =>
                {
                    fila.RelativeItem().Text("IVA contenido").FontSize(8);
                    fila.RelativeItem().AlignRight().Text(Importe(f.ImporteIva)).FontSize(8);
                });
            }
        });
    }

    private static void PieCae(IContainer contenedor, Factura f, byte[] qr, bool simulado)
    {
        contenedor.BorderTop(1).BorderColor(Marca.ColorPrimario).PaddingTop(8).Row(fila =>
        {
            fila.ConstantItem(90).Image(qr).FitArea();
            fila.RelativeItem().PaddingLeft(12).AlignMiddle().Column(c =>
            {
                c.Spacing(3);
                if (simulado)
                {
                    c.Item().Text("Comprobante simulado: no fue autorizado por ARCA").FontFamily(Marca.FuenteTitulos).SemiBold()
                        .FontColor(Colors.Red.Darken3);
                    c.Item().Text("El CAE lo generó el simulador de desarrollo y no existe en ARCA.").FontSize(8).FontColor(Colors.Grey.Darken1);
                }
                else
                {
                    c.Item().Text("Comprobante autorizado por ARCA").FontFamily(Marca.FuenteTitulos).SemiBold().FontColor(Marca.ColorPrimario);
                    c.Item().Text("Esta factura puede verificarse escaneando el código QR.").FontSize(8).FontColor(Colors.Grey.Darken1);
                }
            });
            fila.ConstantItem(190).AlignMiddle().Column(c =>
            {
                c.Spacing(3);
                c.Item().AlignRight().Text(t =>
                {
                    t.Span("CAE N°: ").SemiBold();
                    t.Span(f.Cae);
                });
                c.Item().AlignRight().Text(t =>
                {
                    t.Span("Fecha de vto. de CAE: ").SemiBold();
                    t.Span(f.VencimientoCae.ToString("dd/MM/yyyy", Argentina));
                });
            });
        });
    }

    private static void Celda(TableDescriptor tabla, string texto, bool derecha)
    {
        var celda = tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(4);
        (derecha ? celda.AlignRight() : celda).Text(texto);
    }

    private static string CondicionIva(CondicionFiscal condicion) =>
        condicion == CondicionFiscal.Monotributo ? "Responsable Monotributo" : "IVA Responsable Inscripto";

    private static string Importe(decimal valor) => valor.ToString("C2", Argentina);
}
