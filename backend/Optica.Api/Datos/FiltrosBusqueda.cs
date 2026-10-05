namespace Optica.Api.Datos;

/// <summary>
/// Filtros comunes a las búsquedas de presupuestos y facturas (RF-03, RF-54), ya
/// validados y normalizados. Los vacíos quedan en null o "" y no filtran.
/// </summary>
public record FiltrosBusqueda(DateOnly? Desde, DateOnly? Hasta, string Apellido, string Nombre, string Dni)
{
    /// <summary>Lee los filtros y agrega a <paramref name="errores"/> los que no son válidos (RF-35).</summary>
    public static FiltrosBusqueda Leer(string? desde, string? hasta, string? apellido, string? nombre, string? dni,
        Dictionary<string, string[]> errores)
    {
        var fechaDesde = LeerFecha(desde, "desde", errores);
        var fechaHasta = LeerFecha(hasta, "hasta", errores);
        if (fechaDesde > fechaHasta)
            errores["hasta"] = ["La fecha Hasta no puede ser anterior a la fecha Desde"];

        var dniBuscado = SoloDigitos(dni);
        if (!string.IsNullOrWhiteSpace(dni) && dniBuscado == "")
            errores["dni"] = ["Ingresá solo los números del DNI, con o sin puntos"];

        // RF-82: sin mayúsculas ni acentos. RF-88: el DNI sin puntos ni guiones.
        return new FiltrosBusqueda(fechaDesde, fechaHasta, TextoBusqueda.Normalizar(apellido), TextoBusqueda.Normalizar(nombre), dniBuscado);
    }

    public static string SoloDigitos(string? texto) => new((texto ?? "").Where(char.IsAsciiDigit).ToArray());

    private static DateOnly? LeerFecha(string? texto, string campo, Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;
        if (DateOnly.TryParseExact(texto, "yyyy-MM-dd", out var fecha))
            return fecha;
        errores[campo] = ["Ingresá una fecha válida (dd/mm/aaaa)"];
        return null;
    }
}
