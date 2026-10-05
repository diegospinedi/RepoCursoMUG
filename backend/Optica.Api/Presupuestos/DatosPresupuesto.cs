using System.Net.Mail;

namespace Optica.Api.Presupuestos;

public record DatosCliente(string? Apellido, string? Nombre, string? Dni, string? Domicilio, string? Email, string? Telefono);

/// <summary>
/// Línea tal como la envía la pantalla: el artículo elegido y lo que ajusta la
/// operadora (RF-40). La descripción y el precio unitario NO se reciben: los toma
/// el servidor del catálogo o de la línea ya grabada (ver PresupuestosEndpoints).
/// Cantidad es decimal para poder rechazar 2,5 con un mensaje por campo en lugar
/// de un error de formato.
/// </summary>
public record DatosLinea(int? CodigoArticulo, decimal? Cantidad, decimal? PorcentajeDescuento);

/// <summary>Estado solo se usa al modificar: un presupuesto nuevo siempre nace en Borrador (AC-04).</summary>
public record DatosPresupuesto(DatosCliente? Cliente, List<DatosLinea?>? Lineas, string? Estado = null);

public record ClienteVista(string Apellido, string Nombre, string Dni, string? Domicilio, string? Email, string? Telefono);

public record LineaVista(int CodigoArticulo, string Descripcion, decimal PrecioUnitario, int Cantidad,
    decimal PorcentajeDescuento, decimal PrecioConDescuento, decimal PrecioFinal);

public record PresupuestoVista(int Numero, DateOnly Fecha, string Estado, ClienteVista Cliente,
    IReadOnlyList<LineaVista> Lineas, decimal Total);

public static class ValidacionPresupuesto
{
    public const int LargoNombre = 100;
    public const int LargoDomicilio = 200;
    public const int LargoEmail = 150;
    public const int LargoTelefono = 30;
    public const int MaximoLineas = 100;

    /// <summary>
    /// Valida con un mensaje por campo que indica cómo corregirlo (RF-35). Apellido,
    /// nombre y DNI son obligatorios (RF-61); domicilio, email y teléfono no (RF-74).
    /// Las claves usan la ruta del campo ("cliente.dni") para ubicar el error en pantalla.
    /// </summary>
    public static Dictionary<string, string[]> Validar(DatosPresupuesto datos)
    {
        var errores = new Dictionary<string, string[]>();
        var c = datos.Cliente ?? new DatosCliente(null, null, null, null, null, null);

        Obligatorio(errores, "cliente.apellido", c.Apellido, "Ingresá el apellido del cliente", LargoNombre);
        Obligatorio(errores, "cliente.nombre", c.Nombre, "Ingresá el nombre del cliente", LargoNombre);

        if (string.IsNullOrWhiteSpace(c.Dni))
            errores["cliente.dni"] = ["Ingresá el DNI del cliente"];
        else if (!DniValido(c.Dni))
            errores["cliente.dni"] = ["El DNI debe tener entre 6 y 9 números; podés escribirlo con o sin puntos"];

        Opcional(errores, "cliente.domicilio", c.Domicilio, LargoDomicilio);
        Opcional(errores, "cliente.telefono", c.Telefono, LargoTelefono);
        if (Opcional(errores, "cliente.email", c.Email, LargoEmail) && !EmailValido(c.Email!))
            errores["cliente.email"] = ["Revisá el email: debe tener la forma nombre@dominio.com"];

        ValidarLineas(errores, datos.Lineas);
        return errores;
    }

    /// <summary>RF-16, RF-17, RF-59, RF-60 y al menos una línea (AC-01, AC-04).</summary>
    private static void ValidarLineas(Dictionary<string, string[]> errores, List<DatosLinea?>? lineas)
    {
        if (lineas is null || lineas.Count == 0)
        {
            errores["lineas"] = ["Agregá al menos un artículo al presupuesto"];
            return;
        }
        if (lineas.Count > MaximoLineas)
        {
            errores["lineas"] = [$"El presupuesto puede tener hasta {MaximoLineas} líneas"];
            return;
        }

        for (var i = 0; i < lineas.Count; i++)
        {
            var linea = lineas[i];
            var campo = $"lineas[{i}]";

            if (linea?.CodigoArticulo is null)
                errores[$"{campo}.codigoArticulo"] = ["Elegí un artículo del catálogo"];

            if (linea?.Cantidad is not { } cantidad || cantidad <= 0 || cantidad != decimal.Truncate(cantidad) || cantidad > 9999)
                errores[$"{campo}.cantidad"] = ["La cantidad debe ser un número entero mayor a 0"];

            if (linea?.PorcentajeDescuento is not { } descuento || descuento < 0 || descuento > 100)
                errores[$"{campo}.porcentajeDescuento"] = ["El descuento debe estar entre 0 y 100"];
            else if (decimal.Round(descuento, 2) != descuento)
                errores[$"{campo}.porcentajeDescuento"] = ["El descuento puede tener como máximo 2 decimales"];
        }
    }

    private static bool DniValido(string dni)
    {
        var sinSeparadores = dni.Replace(".", "").Replace(" ", "").Replace("-", "");
        return sinSeparadores.Length is >= 6 and <= 9 && sinSeparadores.All(char.IsAsciiDigit);
    }

    private static bool EmailValido(string email) =>
        MailAddress.TryCreate(email.Trim(), out var direccion) && direccion.Address == email.Trim() && direccion.Host.Contains('.');

    private static void Obligatorio(Dictionary<string, string[]> errores, string campo, string? valor, string mensaje, int largo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            errores[campo] = [mensaje];
        else if (valor.Trim().Length > largo)
            errores[campo] = [$"Puede tener hasta {largo} caracteres"];
    }

    /// <summary>Devuelve true si el campo tiene valor y no excede el largo.</summary>
    private static bool Opcional(Dictionary<string, string[]> errores, string campo, string? valor, int largo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return false;
        if (valor.Trim().Length <= largo)
            return true;
        errores[campo] = [$"Puede tener hasta {largo} caracteres"];
        return false;
    }
}
