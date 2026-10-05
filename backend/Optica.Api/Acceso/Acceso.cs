namespace Optica.Api.Acceso;

/// <summary>
/// Credencial única de la aplicación (RNF-04). Hay una sola fila: no existe
/// módulo de usuarios. La contraseña se guarda solo como hash con sal (RNF-14).
/// </summary>
public class Acceso
{
    public const int IdUnico = 1;

    public int Id { get; set; } = IdUnico;
    public required string HashContrasena { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHastaUtc { get; set; }
}
