using Microsoft.EntityFrameworkCore;
using Optica.Api.Datos;

namespace Optica.Api.Catalogo;

public record DatosArticulo(string? CodigoProveedor, string? Descripcion, decimal? PrecioCosto, decimal? MargenUtilidad);

public record ArticuloVista(int Codigo, string CodigoProveedor, string Descripcion, decimal PrecioCosto,
    decimal MargenUtilidad, decimal PrecioVenta);

public record PaginaArticulos(IReadOnlyList<ArticuloVista> Articulos, int Total, int Pagina, int TamanoPagina);

public static class ArticulosEndpoints
{
    public const int TamanoPagina = 50;
    public const int LargoMaximoCodigoProveedor = 50;
    public const int LargoMaximoDescripcion = 200;

    public static void MapArticulos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/articulos");

        // RF-11: busca por código, código en el proveedor o descripción, sin distinguir mayúsculas ni acentos.
        grupo.MapGet("/", async (string? texto, int? pagina, OpticaDbContext db) =>
        {
            var consulta = db.Articulos.AsNoTracking();
            var normalizado = TextoBusqueda.Normalizar(texto);
            if (normalizado != "")
            {
                var codigo = int.TryParse(normalizado, out var numero) ? numero : -1;
                consulta = consulta.Where(a => a.Codigo == codigo || a.TextoBusqueda.Contains(normalizado));
            }

            var numeroPagina = Math.Max(pagina ?? 1, 1);
            var total = await consulta.CountAsync();
            var articulos = await consulta
                .OrderBy(a => a.Descripcion).ThenBy(a => a.Codigo)
                .Skip((numeroPagina - 1) * TamanoPagina).Take(TamanoPagina)
                .Select(a => AVista(a))
                .ToListAsync();

            return Results.Ok(new PaginaArticulos(articulos, total, numeroPagina, TamanoPagina));
        });

        grupo.MapGet("/{codigo:int}", async (int codigo, OpticaDbContext db) =>
            await db.Articulos.FindAsync(codigo) is { } articulo ? Results.Ok(AVista(articulo)) : NoEncontrado(codigo));

        grupo.MapPost("/", async (DatosArticulo datos, OpticaDbContext db) =>
        {
            if (Validar(datos) is { Count: > 0 } errores)
                return Results.ValidationProblem(errores);

            var articulo = new Articulo();
            articulo.Actualizar(datos.CodigoProveedor!, datos.Descripcion!, datos.PrecioCosto!.Value,
                datos.MargenUtilidad!.Value, await db.Parametros.SingleAsync());
            db.Articulos.Add(articulo);
            await db.SaveChangesAsync();

            return Results.Created($"/api/articulos/{articulo.Codigo}", AVista(articulo));
        });

        // El precio de venta no forma parte de DatosArticulo: no se puede editar (RF-70, AC-81).
        grupo.MapPut("/{codigo:int}", async (int codigo, DatosArticulo datos, OpticaDbContext db) =>
        {
            if (await db.Articulos.FindAsync(codigo) is not { } articulo)
                return NoEncontrado(codigo);
            if (Validar(datos) is { Count: > 0 } errores)
                return Results.ValidationProblem(errores);

            articulo.Actualizar(datos.CodigoProveedor!, datos.Descripcion!, datos.PrecioCosto!.Value,
                datos.MargenUtilidad!.Value, await db.Parametros.SingleAsync());
            await db.SaveChangesAsync();

            return Results.Ok(AVista(articulo));
        });
    }

    private static ArticuloVista AVista(Articulo a) =>
        new(a.Codigo, a.CodigoProveedor, a.Descripcion, a.PrecioCosto, a.MargenUtilidad, a.PrecioVenta);

    private static IResult NoEncontrado(int codigo) =>
        Results.Problem($"No existe el artículo {codigo}.", statusCode: StatusCodes.Status404NotFound);

    /// <summary>Valida cada campo con un mensaje que indica cómo corregirlo (RF-35).</summary>
    private static Dictionary<string, string[]> Validar(DatosArticulo datos)
    {
        var errores = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(datos.CodigoProveedor))
            errores["codigoProveedor"] = ["Ingresá el código del artículo en el proveedor"];
        else if (datos.CodigoProveedor.Trim().Length > LargoMaximoCodigoProveedor)
            errores["codigoProveedor"] = [$"El código en el proveedor puede tener hasta {LargoMaximoCodigoProveedor} caracteres"];

        if (string.IsNullOrWhiteSpace(datos.Descripcion))
            errores["descripcion"] = ["Ingresá la descripción del artículo"];
        else if (datos.Descripcion.Trim().Length > LargoMaximoDescripcion)
            errores["descripcion"] = [$"La descripción puede tener hasta {LargoMaximoDescripcion} caracteres"];

        if (datos.PrecioCosto is not { } costo || costo <= 0)
            errores["precioCosto"] = ["Ingresá un precio de costo mayor a 0"];
        else if (decimal.Round(costo, 2) != costo)
            errores["precioCosto"] = ["El precio de costo puede tener como máximo 2 decimales"];

        // RF-21: el margen se expresa en porcentaje, de 0 a 100.
        if (datos.MargenUtilidad is not { } margen || margen < 0 || margen > 100)
            errores["margenUtilidad"] = ["Ingresá un margen de utilidad entre 0 y 100"];
        else if (decimal.Round(margen, 2) != margen)
            errores["margenUtilidad"] = ["El margen puede tener como máximo 2 decimales"];

        return errores;
    }
}
