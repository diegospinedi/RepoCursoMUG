using Microsoft.EntityFrameworkCore;
using Optica.Api.Datos;

namespace Optica.Api.Acceso;

public enum ResultadoIngreso
{
    Correcto,
    Incorrecto,
    Bloqueado,
    SinContrasena,
}

public record IntentoIngreso(ResultadoIngreso Resultado, int IntentosRestantes = 0, DateTime? BloqueadoHastaUtc = null);

public class ServicioAcceso(OpticaDbContext db, TimeProvider reloj)
{
    public const int LongitudMinima = 8;
    public const int MaximoIntentos = 5;
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(5);

    public Task<bool> ContrasenaDefinidaAsync() => db.Accesos.AnyAsync();

    /// <summary>Devuelve el mensaje de error, o null si la contraseña es válida (RNF-04, AC-57).</summary>
    public static string? ValidarContrasenaNueva(string? contrasena) =>
        string.IsNullOrEmpty(contrasena) || contrasena.Length < LongitudMinima
            ? $"La contraseña debe tener al menos {LongitudMinima} caracteres"
            : null;

    public async Task DefinirContrasenaInicialAsync(string contrasena)
    {
        db.Accesos.Add(new Acceso { HashContrasena = HasherContrasena.Hashear(contrasena) });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Verifica la contraseña aplicando el bloqueo de RNF-08: tras 5 fallos
    /// consecutivos rechaza todo intento, incluso el correcto, durante 5 minutos.
    /// </summary>
    public async Task<IntentoIngreso> IngresarAsync(string? contrasena)
    {
        var acceso = await db.Accesos.SingleOrDefaultAsync();
        if (acceso is null)
            return new IntentoIngreso(ResultadoIngreso.SinContrasena);

        var ahora = reloj.GetUtcNow().UtcDateTime;
        if (acceso.BloqueadoHastaUtc > ahora)
            return new IntentoIngreso(ResultadoIngreso.Bloqueado, BloqueadoHastaUtc: acceso.BloqueadoHastaUtc);

        if (acceso.BloqueadoHastaUtc is not null)
        {
            // El bloqueo venció: se libera solo, sin intervención de un administrador.
            acceso.BloqueadoHastaUtc = null;
            acceso.IntentosFallidos = 0;
        }

        if (!string.IsNullOrEmpty(contrasena) && HasherContrasena.Verificar(contrasena, acceso.HashContrasena))
        {
            acceso.IntentosFallidos = 0;
            await db.SaveChangesAsync();
            return new IntentoIngreso(ResultadoIngreso.Correcto);
        }

        acceso.IntentosFallidos++;
        if (acceso.IntentosFallidos >= MaximoIntentos)
        {
            acceso.BloqueadoHastaUtc = ahora + DuracionBloqueo;
            await db.SaveChangesAsync();
            return new IntentoIngreso(ResultadoIngreso.Bloqueado, BloqueadoHastaUtc: acceso.BloqueadoHastaUtc);
        }

        await db.SaveChangesAsync();
        return new IntentoIngreso(ResultadoIngreso.Incorrecto, MaximoIntentos - acceso.IntentosFallidos);
    }
}
