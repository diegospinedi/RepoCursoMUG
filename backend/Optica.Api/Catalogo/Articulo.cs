using Optica.Api.Configuracion;
using Optica.Api.Datos;

namespace Optica.Api.Catalogo;

/// <summary>Artículo del catálogo (RF-20). Los precios son finales, con IVA incluido.</summary>
public class Articulo
{
    /// <summary>Código autonumérico que asigna la base.</summary>
    public int Codigo { get; set; }

    public string CodigoProveedor { get; private set; } = "";
    public string Descripcion { get; private set; } = "";

    /// <summary>Precio final del folleto del proveedor, con IVA incluido.</summary>
    public decimal PrecioCosto { get; private set; }

    public decimal MargenUtilidad { get; private set; }

    /// <summary>Calculado y ya redondeado (RF-58, RF-69); la operadora no lo carga.</summary>
    public decimal PrecioVenta { get; private set; }

    /// <summary>Código en el proveedor y descripción normalizados, para buscar (RF-11).</summary>
    public string TextoBusqueda { get; private set; } = "";

    public void Actualizar(string codigoProveedor, string descripcion, decimal precioCosto, decimal margenUtilidad,
        ParametrosNegocio parametros)
    {
        CodigoProveedor = codigoProveedor.Trim();
        Descripcion = descripcion.Trim();
        TextoBusqueda = Datos.TextoBusqueda.Normalizar($"{CodigoProveedor} {Descripcion}");
        PrecioCosto = precioCosto;
        MargenUtilidad = margenUtilidad;
        RecalcularPrecioVenta(parametros);
    }

    /// <summary>RF-45: el precio de venta se recalcula siempre que cambia el costo o el margen.</summary>
    public void RecalcularPrecioVenta(ParametrosNegocio parametros) =>
        PrecioVenta = CalculadoraPrecioVenta.Calcular(PrecioCosto, MargenUtilidad, parametros).VentaRedondeada;
}
