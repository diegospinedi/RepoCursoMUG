using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optica.Api.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class Presupuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Presupuestos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Cliente_Apellido = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Cliente_Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Cliente_Dni = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    Cliente_Domicilio = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Cliente_Email = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Cliente_Telefono = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Cliente_ApellidoBusqueda = table.Column<string>(type: "TEXT", nullable: false),
                    Cliente_NombreBusqueda = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presupuestos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_Cliente_ApellidoBusqueda",
                table: "Presupuestos",
                column: "Cliente_ApellidoBusqueda");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_Cliente_Dni",
                table: "Presupuestos",
                column: "Cliente_Dni");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_Fecha",
                table: "Presupuestos",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_Numero",
                table: "Presupuestos",
                column: "Numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Presupuestos");
        }
    }
}
