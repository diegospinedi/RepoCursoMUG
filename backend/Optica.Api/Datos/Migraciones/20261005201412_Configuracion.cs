using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optica.Api.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class Configuracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configuracion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    AlicuotaIva = table.Column<decimal>(type: "TEXT", nullable: false),
                    CondicionFiscal = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TopeIdentificacion = table.Column<decimal>(type: "TEXT", nullable: false),
                    MultiploRedondeo = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuracion", x => x.Id);
                    table.CheckConstraint("CK_Configuracion_Unica", "Id = 1");
                });

            migrationBuilder.InsertData(
                table: "Configuracion",
                columns: new[] { "Id", "AlicuotaIva", "CondicionFiscal", "MultiploRedondeo", "TopeIdentificacion" },
                values: new object[] { 1, 21m, "ResponsableInscripto", 0.01m, 10000000m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configuracion");
        }
    }
}
