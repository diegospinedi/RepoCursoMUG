using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Optica.Api.Datos;

namespace Optica.Tests;

/// <summary>
/// Levanta la API con una base SQLite temporal propia, un reloj controlable
/// y una IP de origen configurable (TestServer no informa ninguna).
/// </summary>
public class AppDePrueba : WebApplicationFactory<Program>
{
    private readonly string _rutaBase = Path.Combine(Path.GetTempPath(), $"optica-test-{Guid.NewGuid():N}.db");

    public FakeTimeProvider Reloj { get; } = new(DateTimeOffset.UtcNow);
    public IPAddress IpOrigen { get; set; } = IPAddress.Loopback;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Optica", $"Data Source={_rutaBase};Pooling=False");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Reloj);
            services.AddSingleton<IStartupFilter>(new FiltroIpOrigen(() => IpOrigen));
        });
    }

    public void CrearBase()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OpticaDbContext>().Database.Migrate();
    }

    public T ConBase<T>(Func<OpticaDbContext, T> consulta)
    {
        using var scope = Services.CreateScope();
        return consulta(scope.ServiceProvider.GetRequiredService<OpticaDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(_rutaBase);
    }

    private class FiltroIpOrigen(Func<IPAddress> ip) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
        {
            app.Use((contexto, continuar) =>
            {
                contexto.Connection.RemoteIpAddress = ip();
                return continuar(contexto);
            });
            siguiente(app);
        };
    }
}
