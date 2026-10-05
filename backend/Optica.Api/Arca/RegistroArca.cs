namespace Optica.Api.Arca;

public static class RegistroArca
{
    /// <summary>
    /// Registra el servicio de ARCA según la configuración "Arca". Por ahora solo existe
    /// el simulador: la conexión real (WSAA + WSFEv1) se agrega cuando haya certificado
    /// de homologación. Producción no se habilita desde el código de desarrollo (AGENTS.md).
    /// La configuración se lee al resolver (no al registrar) para respetar lo que se
    /// configure después, como hacen los tests; Program la valida al arrancar.
    /// </summary>
    public static IServiceCollection AddArca(this IServiceCollection servicios)
    {
        servicios.AddSingleton(sp => LeerOpciones(sp.GetRequiredService<IConfiguration>()));
        servicios.AddSingleton<ArcaSimulado>();
        servicios.AddSingleton<IServicioArca>(sp =>
            new ArcaConTiempoLimite(sp.GetRequiredService<ArcaSimulado>(), sp.GetRequiredService<OpcionesArca>()));
        return servicios;
    }

    public static OpcionesArca LeerOpciones(IConfiguration configuracion)
    {
        var opciones = configuracion.GetSection("Arca").Get<OpcionesArca>() ?? new OpcionesArca();
        if (opciones.Entorno != EntornoArca.Simulado)
            throw new InvalidOperationException(
                $"Arca:Entorno = {opciones.Entorno} no está disponible: la conexión real con ARCA todavía no está " +
                "implementada (falta el certificado de homologación). Usá Arca:Entorno = Simulado.");
        if (opciones.TiempoEsperaSegundos <= 0)
            throw new InvalidOperationException("Arca:TiempoEsperaSegundos debe ser mayor a 0.");
        if (opciones.PuntoVenta <= 0)
            throw new InvalidOperationException("Arca:PuntoVenta debe ser mayor a 0.");
        return opciones;
    }
}
