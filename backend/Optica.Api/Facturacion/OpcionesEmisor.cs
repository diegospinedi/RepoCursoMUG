namespace Optica.Api.Facturacion;

/// <summary>
/// Datos del emisor para el PDF de la factura (RF-30), sección "Emisor" de appsettings.
/// Se editan en el archivo, no en la interfaz. Los valores de fábrica dicen "COMPLETAR"
/// a propósito: hay que reemplazarlos por los datos reales de la óptica.
/// </summary>
public class OpcionesEmisor
{
    public string RazonSocial { get; set; } = "COMPLETAR: razón social";
    public string Domicilio { get; set; } = "COMPLETAR: domicilio comercial";
    /// <summary>11 dígitos, sin guiones.</summary>
    public string Cuit { get; set; } = "COMPLETAR";
    public string IngresosBrutos { get; set; } = "COMPLETAR";
    public string InicioActividades { get; set; } = "COMPLETAR";

    /// <summary>"30-71234567-1" si el CUIT tiene 11 dígitos; si no, el texto tal cual.</summary>
    public string CuitFormateado =>
        Cuit.Length == 11 && Cuit.All(char.IsAsciiDigit) ? $"{Cuit[..2]}-{Cuit[2..10]}-{Cuit[10]}" : Cuit;

    /// <summary>El CUIT como número, o 0 si todavía no se completó.</summary>
    public long CuitNumerico => long.TryParse(Cuit, out var cuit) ? cuit : 0;
}
