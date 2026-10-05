using System.Security.Cryptography;

namespace Optica.Api.Acceso;

/// <summary>
/// PBKDF2-HMAC-SHA256 con sal aleatoria (RNF-14). Formato guardado:
/// "pbkdf2-sha256$iteraciones$sal$hash", ambos en Base64.
/// </summary>
public static class HasherContrasena
{
    private const string Algoritmo = "pbkdf2-sha256";
    private const int Iteraciones = 600_000; // recomendación OWASP para PBKDF2-SHA256
    private const int BytesSal = 16;
    private const int BytesHash = 32;

    public static string Hashear(string contrasena)
    {
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
        return $"{Algoritmo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string contrasena, string guardado)
    {
        var partes = guardado.Split('$');
        if (partes.Length != 4 || partes[0] != Algoritmo || !int.TryParse(partes[1], out var iteraciones))
            return false;

        var sal = Convert.FromBase64String(partes[2]);
        var esperado = Convert.FromBase64String(partes[3]);
        var calculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }
}
