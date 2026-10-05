using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Optica.Api.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class CierrePresupuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RF-08, RF-67, AGENTS.md: un presupuesto Final es un registro cerrado. Además de
            // la validación de la API, la base rechaza cualquier cambio sobre él o sus líneas.
            const string mensaje = "'El presupuesto está en estado Final y no se puede modificar'";

            migrationBuilder.Sql($"""
                CREATE TRIGGER TR_Presupuestos_FinalSinCambios
                BEFORE UPDATE ON Presupuestos WHEN OLD.Estado = 'Final'
                BEGIN SELECT RAISE(ABORT, {mensaje}); END;
                """);
            migrationBuilder.Sql($"""
                CREATE TRIGGER TR_Presupuestos_FinalSinBaja
                BEFORE DELETE ON Presupuestos WHEN OLD.Estado = 'Final'
                BEGIN SELECT RAISE(ABORT, {mensaje}); END;
                """);
            migrationBuilder.Sql($"""
                CREATE TRIGGER TR_LineasPresupuesto_FinalSinAltas
                BEFORE INSERT ON LineasPresupuesto
                WHEN (SELECT Estado FROM Presupuestos WHERE Id = NEW.PresupuestoId) = 'Final'
                BEGIN SELECT RAISE(ABORT, {mensaje}); END;
                """);
            migrationBuilder.Sql($"""
                CREATE TRIGGER TR_LineasPresupuesto_FinalSinCambios
                BEFORE UPDATE ON LineasPresupuesto
                WHEN (SELECT Estado FROM Presupuestos WHERE Id = OLD.PresupuestoId) = 'Final'
                BEGIN SELECT RAISE(ABORT, {mensaje}); END;
                """);
            migrationBuilder.Sql($"""
                CREATE TRIGGER TR_LineasPresupuesto_FinalSinBajas
                BEFORE DELETE ON LineasPresupuesto
                WHEN (SELECT Estado FROM Presupuestos WHERE Id = OLD.PresupuestoId) = 'Final'
                BEGIN SELECT RAISE(ABORT, {mensaje}); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var trigger in new[]
            {
                "TR_Presupuestos_FinalSinCambios", "TR_Presupuestos_FinalSinBaja", "TR_LineasPresupuesto_FinalSinAltas",
                "TR_LineasPresupuesto_FinalSinCambios", "TR_LineasPresupuesto_FinalSinBajas",
            })
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {trigger};");
        }
    }
}
