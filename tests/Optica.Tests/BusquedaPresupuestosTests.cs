using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Optica.Api.Presupuestos;
using Xunit.Abstractions;

namespace Optica.Tests;

public class BusquedaPresupuestosTests(ITestOutputHelper salida) : IAsyncLifetime
{
    private readonly AppDePrueba _app = new();
    private HttpClient _cliente = null!;
    private int _ultimoNumero;

    public async Task InitializeAsync() => _cliente = await _app.ClienteConSesionAsync();

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Graba presupuestos directo en la base, para controlar la fecha.</summary>
    private void Cargar(params (string Fecha, string Apellido, string Nombre, string Dni)[] presupuestos) =>
        _app.ConBase(db =>
        {
            foreach (var (fecha, apellido, nombre, dni) in presupuestos)
                db.Presupuestos.Add(new Presupuesto(++_ultimoNumero, DateOnly.Parse(fecha),
                    Cliente.Crear(apellido, nombre, dni, null, null, null), [new LineaPresupuesto(1, 1, "Estuche", 4500m, 1, 0m)]));
            return db.SaveChanges();
        });

    private async Task<List<(int Numero, string Fecha, string Apellido)>> Buscar(string consulta)
    {
        var json = await _cliente.GetFromJsonAsync<JsonElement>($"/api/presupuestos?{consulta}");
        return json.GetProperty("presupuestos").EnumerateArray()
            .Select(p => (p.GetProperty("numero").GetInt32(), p.GetProperty("fecha").GetString()!, p.GetProperty("apellido").GetString()!))
            .ToList();
    }

    [Fact]
    public async Task AC03_busca_por_apellido_parcial_sin_mayusculas_ni_acentos()
    {
        Cargar(("2026-03-10", "González", "María", "23456789"), ("2026-03-10", "Gómez", "Juan", "30111222"));

        var resultado = await Buscar("apellido=ONZALEZ");

        Assert.Equal(["González"], resultado.Select(r => r.Apellido));
    }

    [Fact]
    public async Task AC68_combina_apellido_y_fecha_desde_sin_hasta()
    {
        Cargar(("2026-03-10", "González", "María", "23456789"), ("2026-03-20", "González", "María", "23456789"),
            ("2026-03-20", "Gómez", "Juan", "30111222"));

        var resultado = await Buscar($"apellido={Uri.EscapeDataString("González")}&desde=2026-03-15");

        Assert.Equal([("2026-03-20", "González")], resultado.Select(r => (r.Fecha, r.Apellido)));
    }

    [Fact]
    public async Task AC69_el_rango_de_fechas_incluye_ambos_limites()
    {
        Cargar(("2026-03-09", "A", "A", "11111111"), ("2026-03-10", "B", "B", "22222222"),
            ("2026-03-20", "C", "C", "33333333"), ("2026-03-21", "D", "D", "44444444"));

        var resultado = await Buscar("desde=2026-03-10&hasta=2026-03-20");

        Assert.Equal(["2026-03-20", "2026-03-10"], resultado.Select(r => r.Fecha));
    }

    [Fact]
    public async Task Filtra_solo_con_fecha_hasta()
    {
        Cargar(("2026-03-09", "A", "A", "11111111"), ("2026-03-21", "B", "B", "22222222"));

        Assert.Equal(["2026-03-09"], (await Buscar("hasta=2026-03-10")).Select(r => r.Fecha));
    }

    [Theory]
    [InlineData("3456")]
    [InlineData("23.456")]
    [InlineData("23456789")]
    public async Task AC87_busca_por_DNI_parcial_ignorando_puntos(string dni)
    {
        Cargar(("2026-03-10", "González", "María", "23.456.789"), ("2026-03-10", "Gómez", "Juan", "30111222"));

        var resultado = await Buscar($"dni={Uri.EscapeDataString(dni)}");

        Assert.Equal(["González"], resultado.Select(r => r.Apellido));
    }

    [Fact]
    public async Task Busca_por_nombre_sin_acentos()
    {
        Cargar(("2026-03-10", "González", "María José", "23456789"), ("2026-03-10", "González", "Mario", "30111222"));

        var resultado = await Buscar("nombre=maria");

        Assert.Single(resultado);
    }

    [Fact]
    public async Task Todos_los_filtros_se_combinan_con_Y() // RF-81
    {
        Cargar(("2026-03-10", "González", "María", "23456789"), ("2026-03-10", "González", "Juan", "23456789"),
            ("2026-03-10", "Pérez", "María", "23456789"), ("2026-04-10", "González", "María", "23456789"),
            ("2026-03-10", "González", "María", "99999999"));

        var resultado = await Buscar("apellido=gonz&nombre=MAR&dni=2345&desde=2026-03-01&hasta=2026-03-31");

        Assert.Equal([1], resultado.Select(r => r.Numero));
    }

    [Fact]
    public async Task Sin_filtros_lista_del_mas_nuevo_al_mas_viejo_con_los_datos_de_la_grilla()
    {
        Cargar(("2026-03-10", "A", "A", "11111111"), ("2026-03-11", "B", "B", "22222222"));

        var json = await _cliente.GetFromJsonAsync<JsonElement>("/api/presupuestos");

        var primero = json.GetProperty("presupuestos")[0];
        Assert.Equal(2, primero.GetProperty("numero").GetInt32());
        Assert.Equal("Borrador", primero.GetProperty("estado").GetString());
        Assert.Equal("22222222", primero.GetProperty("dni").GetString());
        Assert.Equal(4500m, primero.GetProperty("total").GetDecimal());
        Assert.Equal(2, json.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("desde=10/03/2026", "desde", "fecha válida")]
    [InlineData("desde=2026-03-20&hasta=2026-03-10", "hasta", "no puede ser anterior")]
    [InlineData("dni=abc", "dni", "solo los números")]
    public async Task Valida_los_filtros(string consulta, string campo, string mensaje)
    {
        var respuesta = await _cliente.GetAsync($"/api/presupuestos?{consulta}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Contains(mensaje, errores.GetProperty(campo)[0].GetString());
    }

    [Fact]
    public async Task Sin_sesion_no_devuelve_datos_de_clientes() // AC-79
    {
        Cargar(("2026-03-10", "González", "María", "23456789"));

        var respuesta = await _app.CreateClient().GetAsync("/api/presupuestos?apellido=gonzalez");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.DoesNotContain("González", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RNF01_busca_por_apellido_entre_10000_presupuestos()
    {
        var apellidos = new[] { "González", "Gómez", "Pérez", "Rodríguez", "Fernández", "López", "Martínez", "García" };
        _app.ConBase(db =>
        {
            for (var i = 1; i <= 10_000; i++)
                db.Presupuestos.Add(new Presupuesto(i, new DateOnly(2026, 1, 1).AddDays(i % 365),
                    Cliente.Crear(apellidos[i % apellidos.Length], "Cliente", $"{20_000_000 + i}", null, null, null),
                    [new LineaPresupuesto(1, 1, "Estuche", 4500m, 1, 0m)]));
            return db.SaveChanges();
        });

        var tiempos = new List<long>();
        for (var i = 0; i < 20; i++)
        {
            var reloj = Stopwatch.StartNew();
            var resultado = await Buscar($"apellido={Uri.EscapeDataString(apellidos[i % apellidos.Length])}");
            tiempos.Add(reloj.ElapsedMilliseconds);
            Assert.Equal(50, resultado.Count);
        }
        tiempos.Sort();
        salida.WriteLine($"Búsqueda por apellido en 10.000 presupuestos: mediana {tiempos[10]} ms, p95 {tiempos[18]} ms");

        // RNF-01 mide hasta que la grilla se ve (< 2 s); la API tiene que dejar margen de sobra.
        Assert.True(tiempos[18] < 500, $"p95 de la API: {tiempos[18]} ms");
    }
}
