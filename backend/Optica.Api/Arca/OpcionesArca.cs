namespace Optica.Api.Arca;

public enum EntornoArca
{
    Simulado,
    Homologacion,
    Produccion,
}

public enum ModoSimulador
{
    /// <summary>Autoriza todo lo que cumpla las validaciones.</summary>
    Normal,
    /// <summary>Rechaza toda solicitud con un error de ARCA (AC-20).</summary>
    Rechazar,
    /// <summary>No responde: se agota el tiempo de espera sin autorizar (AC-60, AC-65).</summary>
    SinRespuesta,
    /// <summary>Autoriza pero no responde: el comprobante queda autorizado en ARCA (AC-64).</summary>
    AutorizarSinResponder,
}

/// <summary>
/// Configuración de ARCA (sección "Arca" de appsettings). Se edita en el archivo, no en
/// la interfaz: el certificado y el punto de venta no se exponen (RF-64).
/// </summary>
public class OpcionesArca
{
    public EntornoArca Entorno { get; set; } = EntornoArca.Simulado;

    /// <summary>Punto de venta habilitado para web services (RF-28).</summary>
    public int PuntoVenta { get; set; } = 1;

    /// <summary>RNF-10: se da por no disponible tras 30 segundos sin respuesta.</summary>
    public int TiempoEsperaSegundos { get; set; } = 30;

    public OpcionesSimulador Simulador { get; set; } = new();
}

public class OpcionesSimulador
{
    public ModoSimulador Modo { get; set; } = ModoSimulador.Normal;

    /// <summary>Archivo donde el simulador guarda los comprobantes "autorizados".</summary>
    public string Archivo { get; set; } = "arca-simulado.json";
}
