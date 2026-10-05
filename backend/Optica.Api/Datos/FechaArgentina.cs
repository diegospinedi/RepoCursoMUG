namespace Optica.Api.Datos;

/// <summary>La fecha del día en la hora de Argentina, sin importar la zona horaria de la PC.</summary>
public static class FechaArgentina
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public static DateOnly Hoy(TimeProvider reloj) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), Zona).DateTime);
}
