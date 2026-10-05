using System.Security.Cryptography;
using System.Text.Json;

namespace Optica.Api.Arca;

/// <summary>
/// Simulador de WSFEv1 para desarrollo sin certificado. Aplica las validaciones de
/// ARCA que importan para el flujo (número consecutivo y totales consistentes) y
/// guarda los comprobantes en un archivo para que sobrevivan a un reinicio.
/// Los códigos de error imitan los de ARCA pero no son una copia exacta.
/// </summary>
public class ArcaSimulado(OpcionesArca opciones, TimeProvider reloj) : IServicioArca
{
    public const int DiasValidezCae = 10;

    private static readonly SemaphoreSlim Candado = new(1, 1);
    private readonly string _archivo = opciones.Simulador.Archivo;

    public async Task<long> UltimoAutorizadoAsync(int puntoVenta, TipoComprobante tipo, CancellationToken cancelacion = default)
    {
        await SimularDemoraAsync(cancelacion);
        var comprobantes = await LeerAsync(cancelacion);
        return comprobantes.Where(c => c.PuntoVenta == puntoVenta && c.Tipo == tipo).Select(c => c.Numero).DefaultIfEmpty(0).Max();
    }

    public async Task<ComprobanteArca?> ConsultarAsync(int puntoVenta, TipoComprobante tipo, long numero,
        CancellationToken cancelacion = default)
    {
        await SimularDemoraAsync(cancelacion);
        var comprobantes = await LeerAsync(cancelacion);
        return comprobantes.SingleOrDefault(c => c.PuntoVenta == puntoVenta && c.Tipo == tipo && c.Numero == numero);
    }

    public async Task<ResultadoSolicitud> SolicitarCaeAsync(SolicitudComprobante s, CancellationToken cancelacion = default)
    {
        switch (opciones.Simulador.Modo)
        {
            case ModoSimulador.Rechazar:
                return new ComprobanteRechazado([new ErrorArca(10013, "Simulador: rechazo forzado por configuración.")]);
            case ModoSimulador.SinRespuesta:
                await Task.Delay(Timeout.Infinite, cancelacion);
                break;
        }

        await Candado.WaitAsync(cancelacion);
        ResultadoSolicitud resultado;
        try
        {
            var comprobantes = await LeerAsync(cancelacion);
            var errores = Validar(s, comprobantes);
            if (errores.Count > 0)
                return new ComprobanteRechazado(errores);

            var autorizado = new ComprobanteArca(s.PuntoVenta, s.Tipo, s.Numero, s.Fecha, s.ImporteTotal,
                Cae: string.Concat(Enumerable.Range(0, 14).Select(_ => RandomNumberGenerator.GetInt32(10))),
                VencimientoCae: s.Fecha.AddDays(DiasValidezCae));
            comprobantes.Add(autorizado);
            await GuardarAsync(comprobantes, cancelacion);
            resultado = new ComprobanteAutorizado(autorizado.Cae, autorizado.VencimientoCae);
        }
        finally
        {
            Candado.Release();
        }

        if (opciones.Simulador.Modo == ModoSimulador.AutorizarSinResponder)
            await Task.Delay(Timeout.Infinite, cancelacion);
        return resultado;
    }

    private List<ErrorArca> Validar(SolicitudComprobante s, List<ComprobanteArca> comprobantes)
    {
        var errores = new List<ErrorArca>();
        var ultimo = comprobantes.Where(c => c.PuntoVenta == s.PuntoVenta && c.Tipo == s.Tipo).Select(c => c.Numero).DefaultIfEmpty(0).Max();
        if (s.Numero != ultimo + 1)
            errores.Add(new ErrorArca(10016, $"El número de comprobante {s.Numero} no es el próximo a autorizar ({ultimo + 1})."));

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (Math.Abs(s.Fecha.DayNumber - hoy.DayNumber) > 5)
            errores.Add(new ErrorArca(10017, "La fecha del comprobante no puede diferir en más de 5 días de la fecha de envío."));

        if (s.ImporteTotal != s.ImporteNeto + s.ImporteIva)
            errores.Add(new ErrorArca(10048, "El importe total debe ser igual a la suma del neto y el IVA."));
        if (s.Alicuotas.Sum(a => a.Importe) != s.ImporteIva)
            errores.Add(new ErrorArca(10051, "La suma de los importes de las alícuotas debe ser igual al IVA."));
        if (s.Tipo == TipoComprobante.FacturaC && (s.ImporteIva != 0 || s.Alicuotas.Count > 0))
            errores.Add(new ErrorArca(10071, "Una Factura C no debe informar IVA."));
        if (s.Tipo == TipoComprobante.FacturaB && s.Alicuotas.Count == 0)
            errores.Add(new ErrorArca(10070, "Una Factura B debe informar las alícuotas de IVA."));
        if (s.TipoDocumento == TipoDocumento.SinIdentificar && s.NumeroDocumento != 0)
            errores.Add(new ErrorArca(10015, "Con receptor sin identificar el número de documento debe ser 0."));
        return errores;
    }

    /// <summary>Respeta la cancelación como lo haría una llamada de red.</summary>
    private static Task SimularDemoraAsync(CancellationToken cancelacion) => Task.Delay(1, cancelacion);

    private async Task<List<ComprobanteArca>> LeerAsync(CancellationToken cancelacion)
    {
        if (!File.Exists(_archivo))
            return [];
        await using var archivo = File.OpenRead(_archivo);
        return await JsonSerializer.DeserializeAsync<List<ComprobanteArca>>(archivo, cancellationToken: cancelacion) ?? [];
    }

    private async Task GuardarAsync(List<ComprobanteArca> comprobantes, CancellationToken cancelacion)
    {
        var temporal = _archivo + ".tmp";
        await using (var archivo = File.Create(temporal))
            await JsonSerializer.SerializeAsync(archivo, comprobantes, new JsonSerializerOptions { WriteIndented = true }, cancelacion);
        File.Move(temporal, _archivo, overwrite: true);
    }
}
