namespace Optica.Api.Arca;

// Tipos del web service de facturación electrónica de ARCA (WSFEv1). Los valores
// numéricos son los códigos de las tablas de parámetros de ARCA.

/// <summary>Tabla de tipos de comprobante (FEParamGetTiposCbte).</summary>
public enum TipoComprobante
{
    FacturaB = 6,
    FacturaC = 11,
}

/// <summary>Tabla de tipos de documento (FEParamGetTiposDoc).</summary>
public enum TipoDocumento
{
    Dni = 96,
    /// <summary>Consumidor Final sin identificar (RF-27).</summary>
    SinIdentificar = 99,
}

/// <summary>Alícuota de IVA informada en una Factura B (FEParamGetTiposIva).</summary>
public record AlicuotaIva(int Id, decimal BaseImponible, decimal Importe);

/// <summary>
/// Datos de un comprobante a autorizar (FECAESolicitar). En Factura C, ImporteNeto es
/// el total, ImporteIva es 0 y no hay alícuotas (RF-85).
/// </summary>
public record SolicitudComprobante(
    int PuntoVenta,
    TipoComprobante Tipo,
    long Numero,
    DateOnly Fecha,
    TipoDocumento TipoDocumento,
    long NumeroDocumento,
    decimal ImporteTotal,
    decimal ImporteNeto,
    decimal ImporteIva,
    IReadOnlyList<AlicuotaIva> Alicuotas)
{
    /// <summary>1 = Productos.</summary>
    public int Concepto => 1;

    /// <summary>Condición frente al IVA del receptor (RG 5616): 5 = Consumidor Final.</summary>
    public int CondicionIvaReceptor => 5;
}

/// <summary>Error u observación informado por ARCA, con su código y descripción (RF-51).</summary>
public record ErrorArca(int Codigo, string Mensaje);

public abstract record ResultadoSolicitud;

public sealed record ComprobanteAutorizado(string Cae, DateOnly VencimientoCae) : ResultadoSolicitud;

public sealed record ComprobanteRechazado(IReadOnlyList<ErrorArca> Errores) : ResultadoSolicitud;

/// <summary>Comprobante ya autorizado, según FECompConsultar (RF-65).</summary>
public record ComprobanteArca(int PuntoVenta, TipoComprobante Tipo, long Numero, DateOnly Fecha, decimal ImporteTotal,
    string Cae, DateOnly VencimientoCae);

/// <summary>
/// ARCA no respondió a tiempo o no se pudo conectar (RF-31, RNF-10). No dice si el
/// comprobante se autorizó o no: por eso el reintento consulta antes de volver a pedir (RF-65).
/// </summary>
public class ArcaNoDisponibleException(string motivo, Exception? causa = null) : Exception(motivo, causa);
