using System.Text.Json;
using Optica.Api.Arca;
using Optica.Api.Configuracion;

namespace Optica.Api.Facturacion;

/// <summary>
/// Comprobante autorizado por ARCA (RF-29). Se crea solo con CAE y no se modifica ni
/// elimina (RF-32). Las líneas y los datos del cliente son los del presupuesto de origen,
/// que está en Final y tampoco cambia.
/// </summary>
public class Factura
{
    public int Id { get; private set; }
    public int PresupuestoId { get; private set; }
    public int PuntoVenta { get; private set; }
    public TipoComprobante Tipo { get; private set; }
    public long Numero { get; private set; }
    public DateOnly Fecha { get; private set; }
    public TipoDocumento TipoDocumentoReceptor { get; private set; }
    public long NumeroDocumentoReceptor { get; private set; }
    public decimal ImporteTotal { get; private set; }
    public decimal ImporteNeto { get; private set; }
    public decimal ImporteIva { get; private set; }
    /// <summary>Porcentaje aplicado en una Factura B; null en una Factura C.</summary>
    public decimal? AlicuotaIva { get; private set; }
    /// <summary>Condición frente al IVA del emisor al momento de facturar (RF-30).</summary>
    public CondicionFiscal CondicionFiscalEmisor { get; private set; }
    public string Cae { get; private set; } = "";
    public DateOnly VencimientoCae { get; private set; }

    private Factura() { }

    public Factura(int presupuestoId, SolicitudComprobante s, decimal? alicuotaIva, CondicionFiscal condicion, string cae,
        DateOnly vencimientoCae)
    {
        PresupuestoId = presupuestoId;
        PuntoVenta = s.PuntoVenta;
        Tipo = s.Tipo;
        Numero = s.Numero;
        Fecha = s.Fecha;
        TipoDocumentoReceptor = s.TipoDocumento;
        NumeroDocumentoReceptor = s.NumeroDocumento;
        ImporteTotal = s.ImporteTotal;
        ImporteNeto = s.ImporteNeto;
        ImporteIva = s.ImporteIva;
        AlicuotaIva = alicuotaIva;
        CondicionFiscalEmisor = condicion;
        Cae = cae;
        VencimientoCae = vencimientoCae;
    }

    /// <summary>0003-00034561</summary>
    public static string FormatearNumero(int puntoVenta, long numero) => $"{puntoVenta:D4}-{numero:D8}";

    public static string Letra(TipoComprobante tipo) => tipo == TipoComprobante.FacturaB ? "B" : "C";
}

/// <summary>
/// RF-89: se graba ANTES de pedir el CAE, con el número que se solicita. Si ARCA responde
/// (autoriza o rechaza) se borra en la misma operación; solo queda si no hubo respuesta,
/// para que el reintento consulte ese número antes de volver a pedir (RF-65).
/// </summary>
public class EmisionPendiente
{
    public int Id { get; private set; }
    public int PresupuestoId { get; private set; }
    public int PuntoVenta { get; private set; }
    public TipoComprobante Tipo { get; private set; }
    public long Numero { get; private set; }
    public decimal ImporteTotal { get; private set; }
    public decimal? AlicuotaIva { get; private set; }
    public CondicionFiscal CondicionFiscalEmisor { get; private set; }
    public string SolicitudJson { get; private set; } = "";
    public DateTime RegistradaUtc { get; private set; }

    private EmisionPendiente() { }

    public EmisionPendiente(int presupuestoId, SolicitudComprobante solicitud, decimal? alicuotaIva, CondicionFiscal condicion,
        DateTime registradaUtc)
    {
        PresupuestoId = presupuestoId;
        PuntoVenta = solicitud.PuntoVenta;
        Tipo = solicitud.Tipo;
        Numero = solicitud.Numero;
        ImporteTotal = solicitud.ImporteTotal;
        AlicuotaIva = alicuotaIva;
        CondicionFiscalEmisor = condicion;
        SolicitudJson = JsonSerializer.Serialize(solicitud);
        RegistradaUtc = registradaUtc;
    }

    public SolicitudComprobante Solicitud => JsonSerializer.Deserialize<SolicitudComprobante>(SolicitudJson)!;
}
