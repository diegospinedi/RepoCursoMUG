using Microsoft.EntityFrameworkCore;
using Optica.Api.Arca;
using Optica.Api.Configuracion;
using Optica.Api.Datos;
using Optica.Api.Presupuestos;

namespace Optica.Api.Facturacion;

public abstract record ResultadoEmision;
public sealed record EmisionAutorizada(Factura Factura) : ResultadoEmision;
/// <summary>ARCA rechazó: no se registró nada y se puede reintentar (RF-31, RF-51, RF-53).</summary>
public sealed record EmisionRechazada(IReadOnlyList<ErrorArca> Errores) : ResultadoEmision;
/// <summary>ARCA no respondió: queda pendiente de confirmar al reintentar (RF-52, RNF-10).</summary>
public sealed record EmisionSinRespuesta(string Motivo) : ResultadoEmision;
/// <summary>El presupuesto no se puede facturar (Borrador, ya facturado, dato inválido).</summary>
public sealed record EmisionNoPermitida(string Motivo, bool Conflicto) : ResultadoEmision;
/// <summary>RF-91, RF-92: el número registrado figura autorizado en ARCA con otro importe.</summary>
public sealed record EmisionInconsistente(string Motivo) : ResultadoEmision;

/// <summary>
/// Emite la factura de un presupuesto Final (RF-25) y resuelve los reintentos sin duplicar
/// comprobantes (RF-65, RF-66, RF-75, RF-89 a RF-92). Cada emisión termina autorizada con
/// CAE o no registrada (RF-53); una emisión sin respuesta queda pendiente hasta el reintento.
/// </summary>
public class ServicioFacturacion(OpticaDbContext db, IServicioArca arca, OpcionesArca opciones, TimeProvider reloj)
{
    /// <summary>
    /// Una emisión a la vez: el número sale de "último autorizado + 1", y dos emisiones en
    /// paralelo pedirían el mismo. Con dos operadoras, la espera es imperceptible.
    /// </summary>
    private static readonly SemaphoreSlim UnaEmisionALaVez = new(1, 1);

    public async Task<ResultadoEmision> EmitirAsync(int numeroPresupuesto, CancellationToken cancelacion = default)
    {
        await UnaEmisionALaVez.WaitAsync(cancelacion);
        try
        {
            return await EmitirConTurnoAsync(numeroPresupuesto, cancelacion);
        }
        finally
        {
            UnaEmisionALaVez.Release();
        }
    }

    private async Task<ResultadoEmision> EmitirConTurnoAsync(int numeroPresupuesto, CancellationToken cancelacion)
    {
        var presupuesto = await db.Presupuestos.Include(p => p.Lineas).SingleAsync(p => p.Numero == numeroPresupuesto, cancelacion);
        // RF-62, AC-17: en Borrador se rechaza sin enviar nada a ARCA.
        if (presupuesto.Estado != EstadoPresupuesto.Final)
            return new EmisionNoPermitida(
                $"El presupuesto {numeroPresupuesto} está en Borrador: pasalo a Final para facturarlo.", Conflicto: true);

        var existente = await db.Facturas.AsNoTracking().SingleOrDefaultAsync(f => f.PresupuestoId == presupuesto.Id, cancelacion);
        if (existente is not null)
            return new EmisionNoPermitida(
                $"El presupuesto {numeroPresupuesto} ya tiene la Factura {Factura.Letra(existente.Tipo)} " +
                $"{Factura.FormatearNumero(existente.PuntoVenta, existente.Numero)}.", Conflicto: true);

        try
        {
            var pendiente = await db.EmisionesPendientes.SingleOrDefaultAsync(e => e.PresupuestoId == presupuesto.Id, cancelacion);
            if (pendiente is not null)
            {
                var recuperado = await ResolverPendienteAsync(pendiente, cancelacion);
                if (recuperado is not null)
                    return recuperado;
            }
            return await EmitirNuevaAsync(presupuesto, cancelacion);
        }
        catch (ArcaNoDisponibleException e)
        {
            return new EmisionSinRespuesta(e.Message);
        }
    }

    /// <summary>
    /// RF-65: antes de volver a pedir, consulta si ARCA autorizó el número registrado.
    /// Devuelve null si no lo autorizó y hay que emitir de nuevo (AC-65).
    /// </summary>
    private async Task<ResultadoEmision?> ResolverPendienteAsync(EmisionPendiente pendiente, CancellationToken cancelacion)
    {
        var enArca = await arca.ConsultarAsync(pendiente.PuntoVenta, pendiente.Tipo, pendiente.Numero, cancelacion);
        if (enArca is null)
        {
            db.EmisionesPendientes.Remove(pendiente);
            await db.SaveChangesAsync(cancelacion);
            return null;
        }

        if (enArca.ImporteTotal != pendiente.ImporteTotal)
        {
            // RF-91, RF-92: no se emite ni se recupera nada; la operadora revisa en ARCA.
            return new EmisionInconsistente(
                $"El comprobante {Factura.Letra(pendiente.Tipo)} {Factura.FormatearNumero(pendiente.PuntoVenta, pendiente.Numero)} " +
                $"figura autorizado en ARCA con un importe distinto al de este presupuesto. No se emitió ninguna factura: " +
                "revisá el punto de venta en el sitio de ARCA antes de volver a intentar.");
        }

        // RF-66, RF-75, RF-90: ya estaba autorizado con el mismo importe: se guarda su CAE sin emitir otro.
        var factura = new Factura(pendiente.PresupuestoId, pendiente.Solicitud, pendiente.AlicuotaIva,
            pendiente.CondicionFiscalEmisor, enArca.Cae, enArca.VencimientoCae);
        db.Facturas.Add(factura);
        db.EmisionesPendientes.Remove(pendiente);
        await db.SaveChangesAsync(cancelacion);
        return new EmisionAutorizada(factura);
    }

    private async Task<ResultadoEmision> EmitirNuevaAsync(Presupuesto presupuesto, CancellationToken cancelacion)
    {
        var parametros = await db.Parametros.AsNoTracking().SingleAsync(cancelacion);
        var tipo = parametros.CondicionFiscal == CondicionFiscal.Monotributo ? TipoComprobante.FacturaC : TipoComprobante.FacturaB;
        var alicuota = tipo == TipoComprobante.FacturaB ? parametros.AlicuotaIva : (decimal?)null;

        SolicitudComprobante solicitud;
        try
        {
            // Se arma primero con un número provisorio para validar sin consultar a ARCA.
            ArmadorComprobante.Armar(presupuesto, parametros, opciones.PuntoVenta, 1, FechaArgentina.Hoy(reloj));
            var ultimo = await arca.UltimoAutorizadoAsync(opciones.PuntoVenta, tipo, cancelacion);
            solicitud = ArmadorComprobante.Armar(presupuesto, parametros, opciones.PuntoVenta, ultimo + 1, FechaArgentina.Hoy(reloj));
        }
        catch (FacturacionException e)
        {
            return new EmisionNoPermitida(e.Message, Conflicto: false);
        }

        // RF-89: el número queda registrado antes de enviar la solicitud.
        var pendiente = new EmisionPendiente(presupuesto.Id, solicitud, alicuota, parametros.CondicionFiscal, reloj.GetUtcNow().UtcDateTime);
        db.EmisionesPendientes.Add(pendiente);
        await db.SaveChangesAsync(cancelacion);

        // Si ARCA no responde, la excepción sube y la emisión queda pendiente (RF-52).
        var resultado = await arca.SolicitarCaeAsync(solicitud, cancelacion);

        db.EmisionesPendientes.Remove(pendiente);
        if (resultado is ComprobanteAutorizado autorizado)
        {
            var factura = new Factura(presupuesto.Id, solicitud, alicuota, parametros.CondicionFiscal, autorizado.Cae,
                autorizado.VencimientoCae);
            db.Facturas.Add(factura);
            await db.SaveChangesAsync(cancelacion);
            return new EmisionAutorizada(factura);
        }

        await db.SaveChangesAsync(cancelacion);
        return new EmisionRechazada(((ComprobanteRechazado)resultado).Errores);
    }
}
