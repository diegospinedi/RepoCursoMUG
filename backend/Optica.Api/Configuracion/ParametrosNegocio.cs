namespace Optica.Api.Configuracion;

public enum CondicionFiscal
{
    ResponsableInscripto,
    Monotributo,
}

/// <summary>
/// Parámetros de negocio editables desde la pantalla de configuración (RF-63).
/// Hay una sola fila. El certificado de ARCA y el punto de venta NO van acá:
/// se resguardan fuera de la aplicación (RF-64).
/// </summary>
public class ParametrosNegocio
{
    public const int IdUnico = 1;

    public int Id { get; set; } = IdUnico;

    /// <summary>Alícuota única de IVA en porcentaje, por ejemplo 21 (RF-46).</summary>
    public decimal AlicuotaIva { get; set; }

    /// <summary>Define Factura B o C y la fórmula del precio de venta (RF-48).</summary>
    public CondicionFiscal CondicionFiscal { get; set; }

    /// <summary>Total a partir del cual se identifica al receptor con DNI (RF-49).</summary>
    public decimal TopeIdentificacion { get; set; }

    /// <summary>Múltiplo al que se redondea hacia arriba el precio de venta (RF-57).</summary>
    public decimal MultiploRedondeo { get; set; }
}
