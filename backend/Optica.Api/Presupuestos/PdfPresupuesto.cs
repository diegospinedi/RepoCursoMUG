using System.Globalization;
using Optica.Api.Recursos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Optica.Api.Presupuestos;

/// <summary>
/// PDF del presupuesto (RF-02, RF-36): logo y colores de la óptica (RF-55), importes
/// finales sin discriminar IVA (RF-42) y la leyenda "Precios finales, IVA incluido" (RF-37).
/// </summary>
public static class PdfPresupuesto
{
    public const string Leyenda = "Precios finales, IVA incluido";

    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    public static byte[] Generar(Presupuesto presupuesto) => Crear(presupuesto).GeneratePdf();

    /// <summary>El documento sin exportar: permite también generar imágenes para revisarlo.</summary>
    public static IDocument Crear(Presupuesto presupuesto)
    {
        if (presupuesto.Estado != EstadoPresupuesto.Final)
            throw new InvalidOperationException("Solo se genera el PDF de un presupuesto en estado Final (RF-09).");

        return Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(1.8f, Unit.Centimetre);
            pagina.PageColor(Marca.ColorFondo);
            pagina.DefaultTextStyle(t => t.FontFamily(Marca.FuenteTexto).FontSize(10).FontColor(Colors.Grey.Darken4));

            pagina.Header().Element(e => Encabezado(e, presupuesto));
            pagina.Content().PaddingVertical(14).Column(columna =>
            {
                columna.Spacing(14);
                columna.Item().Element(e => DatosCliente(e, presupuesto.Cliente));
                columna.Item().Element(e => Lineas(e, presupuesto));
                columna.Item().AlignRight().Text(Leyenda + ".").FontSize(9).Italic().FontColor(Colors.Grey.Darken2);
            });
            pagina.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"Presupuesto {presupuesto.Numero} · página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        }));
    }

    private static void Encabezado(IContainer contenedor, Presupuesto p)
    {
        contenedor.BorderBottom(3).BorderColor(Marca.ColorPrimario).PaddingBottom(8).Row(fila =>
        {
            fila.ConstantItem(150).Image(Marca.Logo).FitWidth();
            fila.RelativeItem().AlignRight().AlignMiddle().Column(columna =>
            {
                columna.Item().AlignRight().Text("PRESUPUESTO").FontFamily(Marca.FuenteTitulos).Bold().FontSize(20)
                    .FontColor(Marca.ColorPrimario).LetterSpacing(0.05f);
                columna.Item().AlignRight().Text($"Nº {p.Numero}").FontFamily(Marca.FuenteTitulos).SemiBold().FontSize(13)
                    .FontColor(Marca.ColorPrimario);
                columna.Item().AlignRight().Text($"Fecha: {p.Fecha.ToString("dd/MM/yyyy", Argentina)}");
            });
        });
    }

    private static void DatosCliente(IContainer contenedor, Cliente c)
    {
        contenedor.Background(Marca.ColorPrimarioSuave).Padding(10).Column(columna =>
        {
            columna.Spacing(3);
            columna.Item().Text("CLIENTE").FontFamily(Marca.FuenteTitulos).SemiBold().FontSize(9).FontColor(Marca.ColorPrimario)
                .LetterSpacing(0.05f);
            columna.Item().Text($"{c.Apellido}, {c.Nombre}").SemiBold().FontSize(12);
            columna.Item().Text($"DNI {FormatearDni(c.Dni)}");
            if (c.Domicilio is not null)
                columna.Item().Text($"Domicilio: {c.Domicilio}");
            var contacto = string.Join(" · ", new[] { c.Telefono, c.Email }.Where(x => x is not null));
            if (contacto != "")
                columna.Item().Text(contacto);
        });
    }

    private static void Lineas(IContainer contenedor, Presupuesto p)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(c =>
            {
                c.ConstantColumn(42);   // código
                c.RelativeColumn(4);    // descripción
                c.RelativeColumn(1.6f); // precio unitario
                c.ConstantColumn(34);   // cantidad
                c.ConstantColumn(44);   // % desc.
                c.RelativeColumn(1.6f); // precio c/desc.
                c.RelativeColumn(1.7f); // precio final
            });

            tabla.Header(h =>
            {
                foreach (var (titulo, derecha) in new[]
                {
                    ("Código", true), ("Descripción", false), ("Precio unit.", true), ("Cant.", true), ("Desc.", true),
                    ("Con desc.", true), ("Precio final", true),
                })
                {
                    var celda = h.Cell().Background(Marca.ColorPrimario).PaddingVertical(5).PaddingHorizontal(4);
                    (derecha ? celda.AlignRight() : celda).Text(titulo.ToUpperInvariant()).FontFamily(Marca.FuenteTitulos)
                        .SemiBold().FontSize(7.5f).FontColor(Marca.ColorFondo);
                }
            });

            foreach (var linea in p.Lineas.OrderBy(l => l.Orden))
            {
                Celda(tabla, linea.CodigoArticulo.ToString(), derecha: true);
                Celda(tabla, linea.Descripcion);
                Celda(tabla, Importe(linea.PrecioUnitario), derecha: true);
                Celda(tabla, linea.Cantidad.ToString(), derecha: true);
                Celda(tabla, linea.PorcentajeDescuento.ToString("0.##", Argentina) + " %", derecha: true);
                Celda(tabla, Importe(linea.PrecioConDescuento), derecha: true);
                Celda(tabla, Importe(linea.PrecioFinal), derecha: true);
            }

            tabla.Cell().ColumnSpan(5).BorderTop(1.5f).BorderColor(Marca.ColorPrimario).PaddingTop(6).AlignRight()
                .Text("TOTAL").FontFamily(Marca.FuenteTitulos).Bold().FontSize(12).FontColor(Marca.ColorPrimario);
            tabla.Cell().ColumnSpan(2).BorderTop(1.5f).BorderColor(Marca.ColorPrimario).PaddingTop(6).AlignRight()
                .Text(Importe(p.Total)).FontFamily(Marca.FuenteTitulos).Bold().FontSize(12).FontColor(Marca.ColorPrimario);
        });
    }

    private static void Celda(TableDescriptor tabla, string texto, bool derecha = false)
    {
        var celda = tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(4);
        (derecha ? celda.AlignRight() : celda).Text(texto);
    }

    /// <summary>$ 1.815,00 (formato argentino, RF-19).</summary>
    public static string Importe(decimal valor) => valor.ToString("C2", Argentina);

    private static string FormatearDni(string dni) =>
        long.TryParse(dni, out var numero) ? numero.ToString("#,0", Argentina) : dni;
}
