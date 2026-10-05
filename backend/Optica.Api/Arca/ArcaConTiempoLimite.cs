namespace Optica.Api.Arca;

/// <summary>
/// RNF-10: da por no disponible a ARCA tras el tiempo configurado (30 s) y no
/// reintenta por su cuenta. Envuelve cualquier implementación, real o simulada.
/// También traduce errores de red a <see cref="ArcaNoDisponibleException"/>.
/// </summary>
public class ArcaConTiempoLimite(IServicioArca interno, OpcionesArca opciones) : IServicioArca
{
    public Task<long> UltimoAutorizadoAsync(int puntoVenta, TipoComprobante tipo, CancellationToken cancelacion = default) =>
        ConLimiteAsync(c => interno.UltimoAutorizadoAsync(puntoVenta, tipo, c), cancelacion);

    public Task<ResultadoSolicitud> SolicitarCaeAsync(SolicitudComprobante solicitud, CancellationToken cancelacion = default) =>
        ConLimiteAsync(c => interno.SolicitarCaeAsync(solicitud, c), cancelacion);

    public Task<ComprobanteArca?> ConsultarAsync(int puntoVenta, TipoComprobante tipo, long numero, CancellationToken cancelacion = default) =>
        ConLimiteAsync(c => interno.ConsultarAsync(puntoVenta, tipo, numero, c), cancelacion);

    private async Task<T> ConLimiteAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken cancelacion)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        limite.CancelAfter(TimeSpan.FromSeconds(opciones.TiempoEsperaSegundos));
        try
        {
            return await operacion(limite.Token);
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            throw new ArcaNoDisponibleException(
                $"ARCA no respondió en {opciones.TiempoEsperaSegundos} segundos. Podés reintentar más tarde.");
        }
        catch (HttpRequestException e)
        {
            throw new ArcaNoDisponibleException("No se pudo conectar con ARCA. Revisá la conexión a internet y reintentá.", e);
        }
    }
}
