namespace Optica.Api.Arca;

/// <summary>
/// Operaciones de WSFEv1 que usa el sistema. Aislado en su propio módulo para absorber
/// cambios normativos de ARCA sin tocar el resto (riesgo del PRD).
/// Toda operación puede lanzar <see cref="ArcaNoDisponibleException"/>.
/// </summary>
public interface IServicioArca
{
    /// <summary>Último número autorizado del punto de venta y tipo (FECompUltimoAutorizado); 0 si no hay.</summary>
    Task<long> UltimoAutorizadoAsync(int puntoVenta, TipoComprobante tipo, CancellationToken cancelacion = default);

    /// <summary>Pide el CAE (FECAESolicitar).</summary>
    Task<ResultadoSolicitud> SolicitarCaeAsync(SolicitudComprobante solicitud, CancellationToken cancelacion = default);

    /// <summary>Consulta un comprobante (FECompConsultar); null si ARCA no lo tiene autorizado.</summary>
    Task<ComprobanteArca?> ConsultarAsync(int puntoVenta, TipoComprobante tipo, long numero, CancellationToken cancelacion = default);
}
