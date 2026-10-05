using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Optica.Api.Presupuestos;

namespace Optica.Tests;

public class PresupuestosTests : IAsyncLifetime
{
    private readonly AppDePrueba _app = new();
    private HttpClient _cliente = null!;

    private int _articulo;

    public async Task InitializeAsync()
    {
        _cliente = await _app.ClienteConSesionAsync();
        var respuesta = await _cliente.PostAsJsonAsync("/api/articulos",
            new { codigoProveedor = "ABC-1", descripcion = "Armazón acetato negro", precioCosto = 1210m, margenUtilidad = 50m });
        _articulo = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetInt32();
    }

    public Task DisposeAsync()
    {
        _app.Dispose();
        return Task.CompletedTask;
    }

    private object Datos(string? apellido = "González", string? nombre = "María", string? dni = "23.456.789",
        string? domicilio = "Calle 42 nº 767, La Plata", string? email = "maria@example.com", string? telefono = "221 555-1234",
        object[]? lineas = null) =>
        new { cliente = new { apellido, nombre, dni, domicilio, email, telefono }, lineas = lineas ?? [Linea()] };

    private object Linea(int? codigoArticulo = null, decimal? cantidad = 1m, decimal? porcentajeDescuento = 0m) =>
        new { codigoArticulo = codigoArticulo ?? _articulo, cantidad, porcentajeDescuento };

    /// <summary>Crea un artículo cuyo precio de venta es exactamente el indicado (margen 0, múltiplo 0,01).</summary>
    private async Task<int> ArticuloDePrecio(decimal precio, string descripcion = "Artículo")
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/articulos",
            new { codigoProveedor = "X", descripcion, precioCosto = precio, margenUtilidad = 0m });
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetInt32();
    }

    private Task<HttpResponseMessage> Grabar(object datos) => _cliente.PostAsJsonAsync("/api/presupuestos", datos);

    private async Task<JsonElement> GrabarOk(object datos)
    {
        var respuesta = await Grabar(datos);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Errores(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
    }

    [Fact]
    public async Task AC23_guarda_los_seis_datos_del_cliente_y_los_devuelve_al_abrirlo()
    {
        var grabado = await GrabarOk(Datos());

        var leido = await _cliente.GetFromJsonAsync<JsonElement>($"/api/presupuestos/{grabado.GetProperty("numero").GetInt32()}");
        var cliente = leido.GetProperty("cliente");
        Assert.Equal("González", cliente.GetProperty("apellido").GetString());
        Assert.Equal("María", cliente.GetProperty("nombre").GetString());
        Assert.Equal("23456789", cliente.GetProperty("dni").GetString());
        Assert.Equal("Calle 42 nº 767, La Plata", cliente.GetProperty("domicilio").GetString());
        Assert.Equal("maria@example.com", cliente.GetProperty("email").GetString());
        Assert.Equal("221 555-1234", cliente.GetProperty("telefono").GetString());
    }

    [Fact]
    public async Task AC04_al_grabar_por_primera_vez_queda_en_Borrador()
    {
        var grabado = await GrabarOk(Datos());

        Assert.Equal("Borrador", grabado.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Registra_la_fecha_de_creacion_en_hora_de_Argentina()
    {
        // Las 02:30 UTC de un día son las 23:30 del día anterior en Argentina (UTC-3).
        var manana = _app.Reloj.GetUtcNow().UtcDateTime.Date.AddDays(1);
        _app.Reloj.SetUtcNow(new DateTimeOffset(manana.AddHours(2.5), TimeSpan.Zero));
        // Adelantar el reloj vence la sesión por inactividad: se vuelve a ingresar.
        (await _cliente.PostAsJsonAsync("/api/acceso/ingresar", new { contrasena = "clave-de-prueba" })).EnsureSuccessStatusCode();

        var grabado = await GrabarOk(Datos());

        Assert.Equal(DateOnly.FromDateTime(manana.AddDays(-1)).ToString("yyyy-MM-dd"), grabado.GetProperty("fecha").GetString());
    }

    [Fact]
    public async Task AC06_asigna_el_ultimo_numero_mas_uno()
    {
        _app.ConBase(db =>
        {
            db.Presupuestos.Add(new Presupuesto(154, new DateOnly(2026, 10, 1),
                Cliente.Crear("Pérez", "Juan", "30111222", null, null, null), [new LineaPresupuesto(1, 1, "Estuche", 4500m, 1, 0m)]));
            return db.SaveChanges();
        });

        var grabado = await GrabarOk(Datos());

        Assert.Equal(155, grabado.GetProperty("numero").GetInt32());
    }

    [Fact]
    public async Task AC63_grabaciones_simultaneas_reciben_numeros_distintos_y_consecutivos()
    {
        const int sesiones = 10;
        var respuestas = await Task.WhenAll(Enumerable.Range(0, sesiones).Select(_ => Grabar(Datos())));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var numeros = new List<int>();
        foreach (var r in respuestas)
            numeros.Add((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("numero").GetInt32());
        Assert.Equal(Enumerable.Range(1, sesiones), numeros.Order());
    }

    [Fact]
    public async Task AC34_sin_DNI_no_graba_e_indica_el_campo()
    {
        var respuesta = await Grabar(Datos(dni: null));

        var errores = await Errores(respuesta);
        Assert.Equal("Ingresá el DNI del cliente", errores.GetProperty("cliente.dni")[0].GetString());
        Assert.False(_app.ConBase(db => db.Presupuestos.Any()));
    }

    [Theory]
    [InlineData("cliente.apellido", "Ingresá el apellido del cliente")]
    [InlineData("cliente.nombre", "Ingresá el nombre del cliente")]
    public async Task AC44_sin_apellido_o_nombre_no_graba_e_indica_el_campo(string campo, string mensaje)
    {
        var respuesta = await Grabar(campo == "cliente.apellido" ? Datos(apellido: " ") : Datos(nombre: ""));

        var errores = await Errores(respuesta);
        Assert.Equal(mensaje, errores.GetProperty(campo)[0].GetString());
        Assert.False(_app.ConBase(db => db.Presupuestos.Any()));
    }

    [Fact]
    public async Task AC45_graba_sin_domicilio_email_ni_telefono()
    {
        var grabado = await GrabarOk(Datos(domicilio: null, email: "", telefono: "  "));

        var cliente = grabado.GetProperty("cliente");
        Assert.Equal(JsonValueKind.Null, cliente.GetProperty("domicilio").ValueKind);
        Assert.Equal(JsonValueKind.Null, cliente.GetProperty("email").ValueKind);
        Assert.Equal(JsonValueKind.Null, cliente.GetProperty("telefono").ValueKind);
    }

    [Theory]
    [InlineData("23.456.789", "23456789")]
    [InlineData("23 456 789", "23456789")]
    [InlineData("94123456", "94123456")]
    public async Task Guarda_el_DNI_sin_puntos_ni_espacios(string ingresado, string guardado)
    {
        var grabado = await GrabarOk(Datos(dni: ingresado));

        Assert.Equal(guardado, grabado.GetProperty("cliente").GetProperty("dni").GetString());
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567890")]
    [InlineData("23A456789")]
    public async Task Rechaza_un_DNI_invalido(string dni)
    {
        var errores = await Errores(await Grabar(Datos(dni: dni)));

        Assert.Contains("entre 6 y 9 números", errores.GetProperty("cliente.dni")[0].GetString());
    }

    [Theory]
    [InlineData("maria")]
    [InlineData("maria@")]
    [InlineData("maria@example")]
    public async Task Rechaza_un_email_invalido(string email)
    {
        var errores = await Errores(await Grabar(Datos(email: email)));

        Assert.Contains("nombre@dominio.com", errores.GetProperty("cliente.email")[0].GetString());
    }

    [Fact]
    public async Task Abrir_un_presupuesto_inexistente_responde_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync("/api/presupuestos/999")).StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_no_graba_ni_devuelve_datos_de_clientes() // AC-79
    {
        var grabado = await GrabarOk(Datos());
        var anonimo = _app.CreateClient();

        var lectura = await anonimo.GetAsync($"/api/presupuestos/{grabado.GetProperty("numero").GetInt32()}");
        Assert.Equal(HttpStatusCode.Unauthorized, lectura.StatusCode);
        Assert.DoesNotContain("González", await lectura.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/presupuestos", Datos())).StatusCode);
    }

    [Fact]
    public async Task AC24_AC25_guarda_las_lineas_con_sus_siete_campos()
    {
        var articulo = await ArticuloDePrecio(1000m, "Lente orgánico");

        var grabado = await GrabarOk(Datos(lineas: [Linea(articulo, 3m, 10m)]));

        var leido = await _cliente.GetFromJsonAsync<JsonElement>($"/api/presupuestos/{grabado.GetProperty("numero").GetInt32()}");
        var linea = Assert.Single(leido.GetProperty("lineas").EnumerateArray());
        Assert.Equal(articulo, linea.GetProperty("codigoArticulo").GetInt32());
        Assert.Equal("Lente orgánico", linea.GetProperty("descripcion").GetString());
        Assert.Equal(1000m, linea.GetProperty("precioUnitario").GetDecimal());
        Assert.Equal(3, linea.GetProperty("cantidad").GetInt32());
        Assert.Equal(10m, linea.GetProperty("porcentajeDescuento").GetDecimal());
        Assert.Equal(900.00m, linea.GetProperty("precioConDescuento").GetDecimal());
        Assert.Equal(2700.00m, linea.GetProperty("precioFinal").GetDecimal());
        Assert.Equal(2700.00m, leido.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task AC40_el_precio_unitario_es_el_precio_de_venta_guardado_en_el_catalogo()
    {
        (await _cliente.PutAsJsonAsync("/api/configuracion", Configuracion(multiploRedondeo: 50m))).EnsureSuccessStatusCode();
        var articulo = await ArticuloDePrecio(1815.37m); // se guarda redondeado a 1850

        var grabado = await GrabarOk(Datos(lineas: [Linea(articulo)]));

        Assert.Equal(1850.00m, grabado.GetProperty("lineas")[0].GetProperty("precioUnitario").GetDecimal());
    }

    [Fact]
    public async Task Ignora_precios_y_descripciones_que_mande_la_pantalla()
    {
        var linea = new { codigoArticulo = _articulo, descripcion = "Otra cosa", precioUnitario = 1m, cantidad = 2m,
            porcentajeDescuento = 0m, precioConDescuento = 1m, precioFinal = 1m };

        var grabado = await GrabarOk(Datos(lineas: [linea]));

        Assert.Equal("Armazón acetato negro", grabado.GetProperty("lineas")[0].GetProperty("descripcion").GetString());
        Assert.Equal(3630.00m, grabado.GetProperty("total").GetDecimal()); // 1815 × 2
    }

    [Fact]
    public async Task AC11_el_total_suma_los_precios_finales_ya_redondeados()
    {
        var lineas = new[] { Linea(await ArticuloDePrecio(6.67m), 3m, 50m), Linea(await ArticuloDePrecio(1000m), 3m, 10m) };

        var grabado = await GrabarOk(Datos(lineas: lineas));

        var grabadas = grabado.GetProperty("lineas").EnumerateArray().ToList();
        Assert.Equal(3.34m, grabadas[0].GetProperty("precioConDescuento").GetDecimal());
        Assert.Equal(10.02m, grabadas[0].GetProperty("precioFinal").GetDecimal());
        Assert.Equal(2710.02m, grabado.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Conserva_el_orden_de_las_lineas()
    {
        var lineas = new[] { Linea(await ArticuloDePrecio(30m)), Linea(await ArticuloDePrecio(10m)), Linea(await ArticuloDePrecio(20m)) };

        var grabado = await GrabarOk(Datos(lineas: lineas));

        Assert.Equal([30m, 10m, 20m],
            grabado.GetProperty("lineas").EnumerateArray().Select(l => l.GetProperty("precioUnitario").GetDecimal()));
    }

    [Fact]
    public async Task AC86_las_lineas_conservan_su_precio_si_el_catalogo_se_recalcula()
    {
        var grabado = await GrabarOk(Datos(lineas: [Linea()]));
        (await _cliente.PutAsJsonAsync("/api/configuracion", Configuracion(multiploRedondeo: 50m))).EnsureSuccessStatusCode();

        var articulo = await _cliente.GetFromJsonAsync<JsonElement>($"/api/articulos/{_articulo}");
        Assert.Equal(1850.00m, articulo.GetProperty("precioVenta").GetDecimal());
        var leido = await _cliente.GetFromJsonAsync<JsonElement>($"/api/presupuestos/{grabado.GetProperty("numero").GetInt32()}");
        Assert.Equal(1815.00m, leido.GetProperty("lineas")[0].GetProperty("precioUnitario").GetDecimal());
        Assert.Equal(1815.00m, leido.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Sin_lineas_no_graba()
    {
        var errores = await Errores(await Grabar(Datos(lineas: [])));

        Assert.Equal("Agregá al menos un artículo al presupuesto", errores.GetProperty("lineas")[0].GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2.5)] // AC-43
    public async Task AC12_AC43_rechaza_cantidades_no_enteras_o_menores_a_1(decimal cantidad)
    {
        var errores = await Errores(await Grabar(Datos(lineas: [Linea(cantidad: cantidad)])));

        Assert.Equal("La cantidad debe ser un número entero mayor a 0", errores.GetProperty("lineas[0].cantidad")[0].GetString());
    }

    [Fact]
    public async Task AC12_rechaza_un_articulo_con_precio_de_venta_negativo() // RF-17; solo puede venir de la planilla (RF-80)
    {
        var negativo = _app.ConBase(db =>
        {
            var articulo = new Optica.Api.Catalogo.Articulo();
            articulo.Actualizar("NEG", "Precio negativo", -100m, 0m, db.Parametros.Single());
            db.Articulos.Add(articulo);
            db.SaveChanges();
            return articulo.Codigo;
        });

        var errores = await Errores(await Grabar(Datos(lineas: [Linea(), Linea(negativo)])));

        Assert.StartsWith("El precio unitario no puede ser negativo", errores.GetProperty("lineas[1].precioUnitario")[0].GetString());
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(100.01)]
    public async Task AC42_rechaza_un_descuento_fuera_de_0_a_100(decimal descuento)
    {
        var errores = await Errores(await Grabar(Datos(lineas: [Linea(porcentajeDescuento: descuento)])));

        Assert.Equal("El descuento debe estar entre 0 y 100", errores.GetProperty("lineas[0].porcentajeDescuento")[0].GetString());
    }

    [Fact]
    public async Task Rechaza_un_articulo_que_no_esta_en_el_catalogo()
    {
        var errores = await Errores(await Grabar(Datos(lineas: [Linea(codigoArticulo: 999)])));

        Assert.Contains("no existe en el catálogo", errores.GetProperty("lineas[0].codigoArticulo")[0].GetString());
    }

    // ---- Modificación y estados (RF-04, RF-07, RF-08, RF-67) ----

    private static object Configuracion(decimal multiploRedondeo = 0.01m) => new
    {
        alicuotaIva = 21m, condicionFiscal = "ResponsableInscripto", topeIdentificacion = 10_000_000m, multiploRedondeo,
    };

    private object Modificacion(string estado = "Borrador", string domicilio = "Calle 7 nº 1234, La Plata",
        object[]? lineas = null) => new
    {
        cliente = new { apellido = "González", nombre = "María", dni = "23456789", domicilio, email = (string?)null, telefono = (string?)null },
        lineas = lineas ?? [Linea()],
        estado,
    };

    private Task<HttpResponseMessage> Modificar(int numero, object datos) =>
        _cliente.PutAsJsonAsync($"/api/presupuestos/{numero}", datos);

    private async Task<int> NuevoBorrador() => (await GrabarOk(Datos())).GetProperty("numero").GetInt32();

    private Task<JsonElement> Leer(int numero) => _cliente.GetFromJsonAsync<JsonElement>($"/api/presupuestos/{numero}");

    [Fact]
    public async Task AC07_un_borrador_se_modifica_y_el_cambio_se_ve_al_reabrirlo()
    {
        var numero = await NuevoBorrador();

        var respuesta = await Modificar(numero, Modificacion(domicilio: "Calle 7 nº 1234, La Plata"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var leido = await Leer(numero);
        Assert.Equal("Calle 7 nº 1234, La Plata", leido.GetProperty("cliente").GetProperty("domicilio").GetString());
        Assert.Equal("Borrador", leido.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Modificar_un_borrador_permite_agregar_y_quitar_lineas()
    {
        var numero = await NuevoBorrador();
        var otro = await ArticuloDePrecio(500m);

        await Modificar(numero, Modificacion(lineas: [Linea(otro, 2m), Linea(cantidad: 1m)]));

        var leido = await Leer(numero);
        Assert.Equal(2, leido.GetProperty("lineas").GetArrayLength());
        Assert.Equal(2815.00m, leido.GetProperty("total").GetDecimal()); // 500 × 2 + 1815
    }

    [Fact]
    public async Task Al_modificar_un_borrador_las_lineas_existentes_conservan_su_precio() // RF-87
    {
        var numero = await NuevoBorrador();
        (await _cliente.PutAsJsonAsync("/api/configuracion", Configuracion(multiploRedondeo: 50m))).EnsureSuccessStatusCode();

        await Modificar(numero, Modificacion(lineas: [Linea(cantidad: 2m)]));

        var leido = await Leer(numero);
        Assert.Equal(1815.00m, leido.GetProperty("lineas")[0].GetProperty("precioUnitario").GetDecimal());
        Assert.Equal(3630.00m, leido.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task AC05_un_borrador_pasa_a_Final()
    {
        var numero = await NuevoBorrador();

        var respuesta = await Modificar(numero, Modificacion(estado: "Final"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Final", (await Leer(numero)).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Al_pasar_a_Final_graba_tambien_los_cambios_del_contenido()
    {
        var numero = await NuevoBorrador();

        await Modificar(numero, Modificacion(estado: "Final", domicilio: "Domicilio final", lineas: [Linea(cantidad: 3m)]));

        var leido = await Leer(numero);
        Assert.Equal("Domicilio final", leido.GetProperty("cliente").GetProperty("domicilio").GetString());
        Assert.Equal(5445.00m, leido.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task AC66_un_presupuesto_Final_no_se_modifica()
    {
        var numero = await NuevoBorrador();
        await Modificar(numero, Modificacion(estado: "Final", domicilio: "Original"));
        var antes = (await Leer(numero)).GetRawText();

        var respuesta = await Modificar(numero, Modificacion(estado: "Final", domicilio: "Cambiado", lineas: [Linea(cantidad: 9m)]));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(antes, (await Leer(numero)).GetRawText());
    }

    [Fact]
    public async Task AC67_un_presupuesto_Final_no_vuelve_a_Borrador()
    {
        var numero = await NuevoBorrador();
        await Modificar(numero, Modificacion(estado: "Final"));

        var respuesta = await Modificar(numero, Modificacion(estado: "Borrador"));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("Final", (await Leer(numero)).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task La_base_rechaza_cambios_directos_sobre_un_presupuesto_Final()
    {
        var numero = await NuevoBorrador();
        await Modificar(numero, Modificacion(estado: "Final"));

        foreach (var sql in new[]
        {
            $"UPDATE Presupuestos SET Estado = 'Borrador' WHERE Numero = {numero}",
            $"UPDATE Presupuestos SET Total = '1' WHERE Numero = {numero}",
            $"DELETE FROM Presupuestos WHERE Numero = {numero}",
            $"UPDATE LineasPresupuesto SET PrecioFinal = '1' WHERE PresupuestoId = (SELECT Id FROM Presupuestos WHERE Numero = {numero})",
            $"DELETE FROM LineasPresupuesto WHERE PresupuestoId = (SELECT Id FROM Presupuestos WHERE Numero = {numero})",
            $"INSERT INTO LineasPresupuesto (PresupuestoId, Orden, CodigoArticulo, Descripcion, PrecioUnitario, Cantidad, PorcentajeDescuento, PrecioConDescuento, PrecioFinal) SELECT Id, 99, 1, 'x', '1', 1, '0', '1', '1' FROM Presupuestos WHERE Numero = {numero}",
        })
        {
            var error = Assert.ThrowsAny<Exception>(() => _app.ConBase(db => db.Database.ExecuteSqlRaw(sql)));
            Assert.Contains("estado Final", error.Message);
        }
        Assert.Equal("Final", (await Leer(numero)).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Dos_sesiones_modifican_el_mismo_borrador_y_una_lo_finaliza()
    {
        var numero = await NuevoBorrador();

        var respuestas = await Task.WhenAll(
            Modificar(numero, Modificacion(estado: "Final", domicilio: "De la sesión que finaliza")),
            Modificar(numero, Modificacion(domicilio: "De la otra sesión")));

        // Se serializan: si la que finaliza llega primero, la otra recibe 409; si llega
        // segunda, finaliza sobre el cambio de la otra. Nunca se modifica un Final.
        var leido = await Leer(numero);
        Assert.Equal("Final", leido.GetProperty("estado").GetString());
        Assert.Contains(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(respuestas, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        if (respuestas[1].StatusCode == HttpStatusCode.Conflict)
            Assert.Equal("De la sesión que finaliza", leido.GetProperty("cliente").GetProperty("domicilio").GetString());
    }

    [Fact]
    public async Task Rechaza_un_estado_desconocido()
    {
        var numero = await NuevoBorrador();

        var errores = await Errores(await Modificar(numero, Modificacion(estado: "Anulado")));

        Assert.Equal("Elegí el estado: Borrador o Final", errores.GetProperty("estado")[0].GetString());
    }

    [Fact]
    public async Task Modificar_un_presupuesto_inexistente_responde_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Modificar(999, Modificacion())).StatusCode);
    }
}
