# AGENTS.md

## Propósito
Aplicación web para Óptica Sistema: presupuestos con catálogo de artículos y precio de venta calculado.
Desde un presupuesto en estado Final emite factura electrónica a consumidor final vía web services de ARCA (WSFEv1).

## Stack
- .NET 10 (ASP.NET Core Web API) + EF Core 10
- SQLite como base de datos local (archivo, sin servidor)
- React 19 + TypeScript + Vite, sobre Node 22
- Integración ARCA WSFEv1 aislada en su propio módulo (RF-27, cambios normativos)

## Cómo correr

Backend (API en `backend/Optica.Api`, solución `Optica.slnx`):
```bash
dotnet tool restore                          # una sola vez: dotnet-ef 10 como herramienta local
dotnet restore Optica.slnx
dotnet ef database update --project backend/Optica.Api
dotnet run --project backend/Optica.Api      # http://localhost:5220
```

Frontend (`frontend/`):
```bash
cd frontend
npm install
npm run dev        # Vite en http://localhost:5173
```

Tests:
```bash
dotnet test Optica.slnx
```

## Qué NO hacer
- **No modificar registros cerrados.** Un presupuesto en estado Final no se edita ni vuelve a Borrador (RF-08), y una factura con CAE no se modifica ni se elimina (RF-32). No agregues endpoints, migraciones ni "fixes" que permitan eso.
- **No emitir contra ARCA producción.** Todo desarrollo y prueba va contra el entorno de homologación. No apuntes a producción ni uses el certificado productivo para probar; un comprobante autorizado por error no se puede anular desde el sistema (las notas de crédito están fuera de alcance).
- **No ampliar el alcance.** Quedan afuera de esta versión: facturas A, notas de crédito/débito y anulaciones, envío automático por email o WhatsApp, ABM de clientes y módulo de usuarios con roles. Si algo parece necesitarlos, preguntá antes de implementarlo.

## Decisiones tomadas (no volver a preguntar)
- **Cifrado en reposo de la base y de los backups: fuera de alcance en esta versión.** Riesgo identificado y asumido por el responsable del proyecto el 19/09/2026: la base SQLite guarda datos personales de clientes (DNI, domicilio, teléfono, email) sin cifrar, y el backup diario de RNF-02 tampoco exige cifrado. El único control es el acceso con contraseña (RNF-04). No proponer cifrado ni volver a plantear este punto.
- **Framework: .NET 10 + EF Core 10.** Decidido el 19/09/2026 al armar el esqueleto. El PRD no fija versión; .NET 9 es release STS y su ventana de soporte venció en mayo de 2026, así que un proyecto nuevo no arranca sobre una versión sin parches de seguridad.
- **Condición fiscal: Responsable Inscripto.** El comprobante a consumidor final es Factura B (RF-26).
- **IVA:** los precios del proveedor y del catálogo son finales, con IVA incluido; el margen se aplica sobre el costo sin IVA; la alícuota es única para todo el catálogo y es un parámetro de configuración (RF-18, RF-20, RF-21).
