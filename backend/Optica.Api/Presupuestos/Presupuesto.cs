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

public class Presupuesto
{
    public int Id { get; private set; }

    /// <summary>Último número asignado + 1, al grabar por primera vez (RF-06).</summary>
    public int Numero { get; private set; }

    /// <summary>Fecha de creación en hora de Argentina, para buscar por fecha (RF-03).</summary>
    public DateOnly Fecha { get; private set; }

    public EstadoPresupuesto Estado { get; private set; } = EstadoPresupuesto.Borrador;

    public Cliente Cliente { get; private set; } = null!;

    private Presupuesto() { }

    public Presupuesto(int numero, DateOnly fecha, Cliente cliente)
    {
        Numero = numero;
        Fecha = fecha;
        Cliente = cliente;
    }
}
