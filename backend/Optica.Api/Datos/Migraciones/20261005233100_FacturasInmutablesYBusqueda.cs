using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optica.Api.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class FacturasInmutablesYBusqueda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComprobanteBusqueda",
                table: "Facturas",
                type: "TEXT",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            // Completa las facturas que ya existieran, antes de que los triggers impidan tocarlas.
            migrationBuilder.Sql("UPDATE Facturas SET ComprobanteBusqueda = printf('%04d%08d', PuntoVenta, Numero);");

            // RF-32, AGENTS.md: una factura con CAE es un registro cerrado. La API no tiene
            // endpoints para modificarla ni eliminarla, y la base rechaza cualquier intento.
            const string mensaje = "'Una factura emitida con CAE no se puede modificar ni eliminar'";
            migrationBuilder.Sql($"""
                CREATE TRIGGER TR_Facturas_SinCambios
                BEFORE UPDATE ON Facturas
                BEGIN SELECT RAISE(ABORT, {mensaje}); END;
                """);
            migrationBuilder.Sql($"""
                CREATE TRIGGER TR_Facturas_SinBajas
                BEFORE DELETE ON Facturas
                BEGIN SELECT RAISE(ABORT, {mensaje}); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_Facturas_SinCambios;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_Facturas_SinBajas;");

            migrationBuilder.DropColumn(
                name: "ComprobanteBusqueda",
                table: "Facturas");
        }
    }
}
