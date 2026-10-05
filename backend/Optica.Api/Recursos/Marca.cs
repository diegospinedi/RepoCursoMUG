using System.Text.Json;
using QuestPDF.Drawing;

namespace Optica.Api.Recursos;

/// <summary>
/// Logo, colores y fuentes de la óptica para los PDF (RF-55). El logo y los colores
/// se leen de Marca/ (copiados a la salida por el .csproj): no se repiten acá.
/// </summary>
public static class Marca
{
    public const string FuenteTitulos = "Montserrat";
    public const string FuenteTexto = "Barlow";

    private static readonly string Carpeta = Path.Combine(AppContext.BaseDirectory, "Recursos");

    public static byte[] Logo { get; } = File.ReadAllBytes(Path.Combine(Carpeta, "Marca", "logo.png"));

    public static string ColorPrimario { get; }
    public static string ColorFondo { get; }

    /// <summary>El primario al 8 % sobre el fondo, como --color-primario-suave del frontend.</summary>
    public static string ColorPrimarioSuave { get; }

    static Marca()
    {
        using var branding = JsonDocument.Parse(File.ReadAllText(Path.Combine(Carpeta, "Marca", "branding.json")));
        var colores = branding.RootElement.GetProperty("colores");
        ColorPrimario = colores.GetProperty("primario").GetString()!;
        ColorFondo = colores.GetProperty("fondo").GetString()!;
        ColorPrimarioSuave = Mezclar(ColorPrimario, ColorFondo, 0.08);
    }

    /// <summary>Registra las fuentes de la marca en QuestPDF. Se llama una vez al iniciar.</summary>
    public static void RegistrarFuentes()
    {
        QuestPDF.Settings.UseSystemFonts = false; // mismo resultado en cualquier PC
        foreach (var archivo in Directory.GetFiles(Path.Combine(Carpeta, "Fuentes"), "*.woff"))
        {
            using var fuente = File.OpenRead(archivo);
            FontManager.RegisterFontFromStream(fuente);
        }
    }

    private static string Mezclar(string color, string fondo, double proporcion)
    {
        static (int R, int G, int B) Leer(string hex) =>
            (Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));
        var (r1, g1, b1) = Leer(color);
        var (r2, g2, b2) = Leer(fondo);
        int Canal(int a, int b) => (int)Math.Round(a * proporcion + b * (1 - proporcion));
        return $"#{Canal(r1, r2):X2}{Canal(g1, g2):X2}{Canal(b1, b2):X2}";
    }
}
