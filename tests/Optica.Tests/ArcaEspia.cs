using Optica.Api.Arca;

namespace Optica.Tests;

/// <summary>Registra en orden cada llamada a ARCA, para verificar qué se envió y qué no.</summary>
public class ArcaEspia(IServicioArca interno) : IServicioArca
{
    private readonly List<string> _llamadas = [];

    public IReadOnlyList<string> Llamadas
    {
        get { lock (_llamadas) return [.. _llamadas]; }
    }

    public int Solicitudes => Llamadas.Count(l => l.StartsWith("Solicitar"));

    private void Registrar(string llamada)
    {
        lock (_llamadas) _llamadas.Add(llamada);
    }

    public Task<long> UltimoAutorizadoAsync(int puntoVenta, TipoComprobante tipo, CancellationToken cancelacion = default)
    {
        Registrar($"Ultimo {tipo}");
        return interno.UltimoAutorizadoAsync(puntoVenta, tipo, cancelacion);
    }

    public Task<ResultadoSolicitud> SolicitarCaeAsync(SolicitudComprobante solicitud, CancellationToken cancelacion = default)
    {
        Registrar($"Solicitar {solicitud.Tipo} {solicitud.Numero}");
        return interno.SolicitarCaeAsync(solicitud, cancelacion);
    }

    public Task<ComprobanteArca?> ConsultarAsync(int puntoVenta, TipoComprobante tipo, long numero, CancellationToken cancelacion = default)
    {
        Registrar($"Consultar {tipo} {numero}");
        return interno.ConsultarAsync(puntoVenta, tipo, numero, cancelacion);
    }
}
