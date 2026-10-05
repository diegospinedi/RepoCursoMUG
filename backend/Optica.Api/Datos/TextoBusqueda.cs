using System.Globalization;
using System.Text;

namespace Optica.Api.Datos;

/// <summary>
/// Normaliza texto para búsquedas sin distinguir mayúsculas ni acentos
/// ("González" → "gonzalez"). Se guarda en una columna aparte porque SQLite
/// no compara sin acentos por su cuenta.
/// </summary>
public static class TextoBusqueda
{
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        var descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                resultado.Append(char.ToLowerInvariant(c));
        }
        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }
}
