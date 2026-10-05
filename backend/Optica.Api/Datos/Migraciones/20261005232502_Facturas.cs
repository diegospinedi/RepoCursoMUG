using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optica.Api.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class Facturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmisionesPendientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PresupuestoId = table.Column<int>(type: "INTEGER", nullable: false),
                    PuntoVenta = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Numero = table.Column<long>(type: "INTEGER", nullable: false),
                    ImporteTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    AlicuotaIva = table.Column<decimal>(type: "TEXT", nullable: true),
                    CondicionFiscalEmisor = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SolicitudJson = table.Column<string>(type: "TEXT", nullable: false),
                    RegistradaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmisionesPendientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmisionesPendientes_Presupuestos_PresupuestoId",
                        column: x => x.PresupuestoId,
                        principalTable: "Presupuestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Facturas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PresupuestoId = table.Column<int>(type: "INTEGER", nullable: false),
                    PuntoVenta = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Numero = table.Column<long>(type: "INTEGER", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TipoDocumentoReceptor = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroDocumentoReceptor = table.Column<long>(type: "INTEGER", nullable: false),
                    ImporteTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    ImporteNeto = table.Column<decimal>(type: "TEXT", nullable: false),
                    ImporteIva = table.Column<decimal>(type: "TEXT", nullable: false),
                    AlicuotaIva = table.Column<decimal>(type: "TEXT", nullable: true),
                    CondicionFiscalEmisor = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Cae = table.Column<string>(type: "TEXT", maxLength: 14, nullable: false),
                    VencimientoCae = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facturas_Presupuestos_PresupuestoId",
                        column: x => x.PresupuestoId,
                        principalTable: "Presupuestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmisionesPendientes_PresupuestoId",
                table: "EmisionesPendientes",
                column: "PresupuestoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_Fecha",
                table: "Facturas",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_PresupuestoId",
                table: "Facturas",
                column: "PresupuestoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_PuntoVenta_Tipo_Numero",
                table: "Facturas",
                columns: new[] { "PuntoVenta", "Tipo", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmisionesPendientes");

            migrationBuilder.DropTable(
                name: "Facturas");
        }
    }
}
