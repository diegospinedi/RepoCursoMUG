using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Optica.Api.Acceso;

public record PedidoContrasena(string? Contrasena);

public static class AccesoEndpoints
{
    public static void MapAcceso(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/acceso");

        grupo.MapGet("/estado", async (HttpContext http, ServicioAcceso servicio) => Results.Ok(new
        {
            contrasenaDefinida = await servicio.ContrasenaDefinidaAsync(),
            sesionIniciada = http.User.Identity?.IsAuthenticated == true,
        })).AllowAnonymous();

        grupo.MapPost("/contrasena-inicial", async (PedidoContrasena pedido, HttpContext http, ServicioAcceso servicio) =>
        {
            // Solo desde la PC del local: evita que otro equipo de la red defina
            // la contraseña antes que las dueñas.
            if (http.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip))
                return Results.Problem("La contraseña inicial solo se puede definir desde la PC donde está instalado el sistema.",
                    statusCode: StatusCodes.Status403Forbidden);

            if (await servicio.ContrasenaDefinidaAsync())
                return Results.Problem("La contraseña ya está definida.", statusCode: StatusCodes.Status409Conflict);

            if (ServicioAcceso.ValidarContrasenaNueva(pedido.Contrasena) is { } error)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["contrasena"] = [error] });

            await servicio.DefinirContrasenaInicialAsync(pedido.Contrasena!);
            await IniciarSesionAsync(http);
            return Results.NoContent();
        }).AllowAnonymous();

        grupo.MapPost("/ingresar", async (PedidoContrasena pedido, HttpContext http, ServicioAcceso servicio, TimeProvider reloj) =>
        {
            var intento = await servicio.IngresarAsync(pedido.Contrasena);
            switch (intento.Resultado)
            {
                case ResultadoIngreso.Correcto:
                    await IniciarSesionAsync(http);
                    return Results.NoContent();
                case ResultadoIngreso.Incorrecto:
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["contrasena"] = [$"Contraseña incorrecta. Revisala y volvé a intentar (quedan {intento.IntentosRestantes} intentos)."],
                    }, statusCode: StatusCodes.Status401Unauthorized);
                case ResultadoIngreso.Bloqueado:
                    var minutos = (int)Math.Ceiling((intento.BloqueadoHastaUtc!.Value - reloj.GetUtcNow().UtcDateTime).TotalMinutes);
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["contrasena"] = [$"Acceso bloqueado por demasiados intentos fallidos. Esperá {minutos} minuto(s) y volvé a intentar."],
                    }, statusCode: StatusCodes.Status429TooManyRequests);
                default:
                    return Results.Problem("Todavía no se definió la contraseña.", statusCode: StatusCodes.Status409Conflict);
            }
        }).AllowAnonymous();

        grupo.MapPost("/salir", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });
    }

    private static Task IniciarSesionAsync(HttpContext http)
    {
        var identidad = new ClaimsIdentity([new Claim(ClaimTypes.Name, "operadora")], CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));
    }
}
