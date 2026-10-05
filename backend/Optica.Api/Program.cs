using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Optica.Api.Acceso;
using Optica.Api.Configuracion;
using Optica.Api.Datos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<OpticaDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Optica")));
builder.Services.AddScoped<ServicioAcceso>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "optica.sesion";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        // RNF-09: la sesión vence tras 60 minutos sin actividad.
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = context =>
        {
            // Renueva en cada pedido, así el vencimiento cuenta exactamente desde la última actividad.
            context.ShouldRenew = true;
            return Task.CompletedTask;
        };
        // Es una API: sin sesión responde 401 en lugar de redirigir (AC-79).
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    // Todo endpoint exige sesión salvo los marcados con AllowAnonymous (AC-79).
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAcceso();
app.MapConfiguracion();

app.Run();
