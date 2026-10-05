using Optica.Api.Datos;

namespace Optica.Api.Presupuestos;

public enum EstadoPresupuesto
{
    Borrador,
    Final,
}

/// <summary>
/// Datos del cliente guardados dentro del presupuesto (RF-05, RF-38): no hay
/// ABM de clientes. Son datos personales (Ley 25.326): no loguearlos.
/// </summary>
public class Cliente
{
    public string Apellido { get; private set; } = "";
    public string Nombre { get; private set; } = "";

    /// <summary>Solo dígitos, sin puntos ni espacios ("23456789").</summary>
    public string Dni { get; private set; } = "";

    public string? Domicilio { get; private set; }
    public string? Email { get; private set; }
    public string? Telefono { get; private set; }

    /// <summary>Apellido y nombre normalizados, para buscar sin mayúsculas ni acentos (RF-82).</summary>
    public string ApellidoBusqueda { get; private set; } = "";
    public string NombreBusqueda { get; private set; } = "";

    public static Cliente Crear(string apellido, string nombre, string dni, string? domicilio, string? email, string? telefono) => new()
    {
        Apellido = apellido.Trim(),
        Nombre = nombre.Trim(),
        Dni = SoloDigitos(dni),
        Domicilio = Opcional(domicilio),
        Email = Opcional(email),
        Telefono = Opcional(telefono),
        ApellidoBusqueda = TextoBusqueda.Normalizar(apellido),
        NombreBusqueda = TextoBusqueda.Normalizar(nombre),
    };

    public static string SoloDigitos(string texto) => new(texto.Where(char.IsAsciiDigit).ToArray());

    private static string? Opcional(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>
/// Línea del presupuesto (RF-12). Guarda una copia del código, la descripción y el
/// precio del artículo al momento de cargarla: si después cambia el catálogo, la
/// línea conserva su precio (RF-87).
/// </summary>
public class LineaPresupuesto
{
    public int Orden { get; private set; }
    public int CodigoArticulo { get; private set; }
    public string Descripcion { get; private set; } = "";
    public decimal PrecioUnitario { get; private set; }
    public int Cantidad { get; private set; }
    public decimal PorcentajeDescuento { get; private set; }
    public decimal PrecioConDescuento { get; private set; }
    public decimal PrecioFinal { get; private set; }

    private LineaPresupuesto() { }

    public LineaPresupuesto(int orden, int codigoArticulo, string descripcion, decimal precioUnitario, int cantidad,
        decimal porcentajeDescuento)
    {
        Orden = orden;
        CodigoArticulo = codigoArticulo;
        Descripcion = descripcion.Trim();
        PrecioUnitario = precioUnitario;
        Cantidad = cantidad;
        PorcentajeDescuento = porcentajeDescuento;
        PrecioConDescuento = CalculoLinea.PrecioConDescuento(precioUnitario, porcentajeDescuento);
        PrecioFinal = CalculoLinea.PrecioFinal(PrecioConDescuento, cantidad);
    }
}

public class Presupuesto
{
    public int Id { get; private set; }

    /// <summary>Último número asignado + 1, al grabar por primera vez (RF-06).</summary>
    public int Numero { get; private set; }

    /// <summary>Fecha de creación en hora de Argentina, para buscar por fecha (RF-03).</summary>
    public DateOnly Fecha { get; private set; }

    public EstadoPresupuesto Estado { get; private set; } = EstadoPresupuesto.Borrador;

    public Cliente Cliente { get; private set; } = null!;

    private readonly List<LineaPresupuesto> _lineas = [];
    public IReadOnlyList<LineaPresupuesto> Lineas => _lineas;

    /// <summary>Suma de los precios finales de las líneas (RF-15).</summary>
    public decimal Total { get; private set; }

    private Presupuesto() { }

    public Presupuesto(int numero, DateOnly fecha, Cliente cliente, IEnumerable<LineaPresupuesto> lineas)
    {
        Numero = numero;
        Fecha = fecha;
        Cliente = cliente;
        ReemplazarLineas(lineas);
    }

    /// <summary>Solo en Borrador (RF-07, RF-08).</summary>
    public void Modificar(Cliente cliente, IEnumerable<LineaPresupuesto> lineas)
    {
        ExigirBorrador();
        Cliente = cliente;
        ReemplazarLineas(lineas);
    }

    /// <summary>Pasa a Final. No hay camino de vuelta a Borrador (RF-67).</summary>
    public void Finalizar()
    {
        ExigirBorrador();
        Estado = EstadoPresupuesto.Final;
    }

    private void ExigirBorrador()
    {
        if (Estado == EstadoPresupuesto.Final)
            throw new InvalidOperationException($"El presupuesto {Numero} está en estado Final y no se puede modificar.");
    }

    private void ReemplazarLineas(IEnumerable<LineaPresupuesto> lineas)
    {
        _lineas.Clear();
        _lineas.AddRange(lineas);
        Total = CalculoLinea.Total(_lineas);
    }
}
