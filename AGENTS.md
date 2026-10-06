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

Trabajá todo desde WSL (backend y frontend del mismo lado). Mezclar Windows y WSL trae dos problemas:
el proxy de Vite no llega a una API que corre en Windows, y Smart App Control de Windows puede bloquear
los DLL recién compilados ("An Application Control policy has blocked this file"). El SDK de .NET 10
de WSL está en `~/.dotnet` (instalado con `dotnet-install.sh`, en el `PATH` desde `~/.bashrc`).

Backend (API en `backend/Optica.Api`, solución `Optica.slnx`):
```bash
dotnet run --project backend/Optica.Api      # http://localhost:5220; crea la base o aplica las migraciones al arrancar
```

Para crear migraciones nuevas hace falta `dotnet-ef` (herramienta local del repo):
```bash
dotnet tool restore                          # una sola vez
dotnet ef migrations add <Nombre> --project backend/Optica.Api -o Datos/Migraciones
```

Frontend (`frontend/`):
```bash
cd frontend
npm install
npm run dev        # Vite en http://localhost:5173, con proxy de /api a la API
```

La primera vez, la app pide definir la contraseña de acceso; solo se acepta desde la PC donde corre la API.

Tests y verificación (correr los tres del frontend: Vitest no revisa tipos, el build sí):
```bash
dotnet test Optica.slnx
cd frontend && npm test && npm run build && npm run lint
```
Si `dotnet test` falla con "Access to the path ... Optica.Api.dll is denied", hay una API corriendo
que bloquea `bin/Debug`: cerrala o usá `dotnet test Optica.slnx -c Release`.

## Configuración (`backend/Optica.Api/appsettings.json`)
Se edita en el archivo, no en la interfaz: el certificado de ARCA y el punto de venta no se exponen en pantalla (RF-64).
- **`Arca`**: `Entorno` solo acepta `Simulado` (la conexión real con WSAA + WSFEv1 falta hasta tener certificado
  de homologación; con otro valor la API no arranca). `PuntoVenta`, `TiempoEsperaSegundos` (30, RNF-10) y
  `Simulador:Modo` para probar los casos de error desde la pantalla: `Normal`, `Rechazar`, `SinRespuesta`
  y `AutorizarSinResponder`. El simulador guarda sus comprobantes en `arca-simulado.json` (fuera del repo).
- **`Emisor`**: razón social, domicilio, CUIT, ingresos brutos e inicio de actividades para el PDF de la
  factura (RF-30). Vienen con "COMPLETAR" hasta cargar los datos reales de la óptica.
- Los parámetros de negocio (alícuota de IVA, condición fiscal, tope de identificación y múltiplo de
  redondeo) no van acá: se editan en la pantalla Configuración y se guardan en la base (RF-63).

## Estructura
- `backend/Optica.Api/`: un módulo por carpeta: `Acceso`, `Configuracion`, `Catalogo`, `Presupuestos`,
  `Facturacion` (armado del comprobante, emisión con reintento, PDF y QR) y `Arca` (solo el adaptador de
  WSFEv1 y su simulador, detrás de `IServicioArca`). `Datos/` tiene el `DbContext`, las migraciones y helpers
  compartidos. `Recursos/` tiene las fuentes de la marca para los PDF; el logo y los colores se enlazan desde `Marca/`.
- `tests/Optica.Tests/`: tests de integración con `AppDePrueba` (base SQLite temporal, reloj falso, espía de ARCA).
- `frontend/src/`: una carpeta por pantalla. Antes de tocar estilos o pantallas, usá la skill
  `frontend-design` (`.claude/skills/frontend-design`): colores y fuentes salen de `Marca/branding.json`
  y de los tokens de `estilos/tokens.css`, nunca escritos a mano.

## Qué NO hacer
- **No modificar registros cerrados.** Un presupuesto en estado Final no se edita ni vuelve a Borrador (RF-08, RF-67), y una factura con CAE no se modifica ni se elimina (RF-32). No agregues endpoints, migraciones ni "fixes" que permitan eso. La base lo refuerza con triggers de SQLite (migraciones `CierrePresupuestos` y `FacturasInmutablesYBusqueda`): no los borres. Para cargar datos de prueba, el presupuesto se graba en Borrador y después pasa a Final, como en la app.
- **No quitar la leyenda de comprobante simulado.** Con `Arca:Entorno = Simulado`, el PDF de la factura dice "COMPROBANTE SIMULADO — SIN VALIDEZ FISCAL": su CAE no existe en ARCA.
- **No copiar la base con una copia de archivo común.** SQLite está en modo WAL: los cambios recientes pueden estar en `optica.db-wal`. Un backup (RNF-02) tiene que usar `VACUUM INTO` o la API de backup de SQLite.
- **No emitir contra ARCA producción.** Todo desarrollo y prueba va contra el entorno de homologación. No apuntes a producción ni uses el certificado productivo para probar; un comprobante autorizado por error no se puede anular desde el sistema (las notas de crédito están fuera de alcance).
- **No ampliar el alcance.** Quedan afuera de esta versión: facturas A, notas de crédito/débito y anulaciones, envío automático por email o WhatsApp, ABM de clientes y módulo de usuarios con roles. Si algo parece necesitarlos, preguntá antes de implementarlo.

## Decisiones tomadas (no volver a preguntar)
- **Cifrado en reposo de la base y de los backups: fuera de alcance en esta versión.** Riesgo identificado y asumido por el responsable del proyecto el 19/09/2026: la base SQLite guarda datos personales de clientes (DNI, domicilio, teléfono, email) sin cifrar, y el backup diario de RNF-02 tampoco exige cifrado. El único control es el acceso con contraseña (RNF-04). No proponer cifrado ni volver a plantear este punto.
- **Framework: .NET 10 + EF Core 10.** Decidido el 19/09/2026 al armar el esqueleto. El PRD no fija versión; .NET 9 es release STS y su ventana de soporte venció en mayo de 2026, así que un proyecto nuevo no arranca sobre una versión sin parches de seguridad.
- **Condición fiscal: configurable.** Responsable Inscripto → Factura B; Monotributo → Factura C, sin desglose de IVA y con precio de venta = costo × (1 + margen) (PRD.md: RF-26, RF-48, RF-76, RF-84, RF-85).
- **IVA:** los precios del proveedor y del catálogo son finales, con IVA incluido; con Responsable Inscripto el margen se aplica sobre el costo sin IVA (con Monotributo, sobre el costo final); la alícuota es única para todo el catálogo y es un parámetro de configuración (RF-18, RF-20, RF-21).
- **Precio unitario de la línea: fijo, no editable** (decidido el 05/10/2026). La pantalla envía solo artículo, cantidad y descuento; el servidor toma descripción y precio del catálogo en líneas nuevas y de la línea ya grabada en las existentes (RF-39, RF-73, RF-87). Para bajar un precio está el descuento.
- **ARCA simulado hasta tener certificado** (decidido el 05/10/2026). No hay certificado de homologación todavía; todo se desarrolla contra el simulador.
- **PDF con QuestPDF** (licencia Community, válida mientras la empresa facture menos de USD 1 millón por año).

## Convenciones del código (respetalas al cambiarlo)
- **Precio de venta:** se calcula como costo × (1 + margen / 100), la identidad que el propio RF-21 reconoce con alícuota única. Aplicar los tres pasos literales en `decimal` deja residuos que el redondeo hacia arriba convierte en un centavo de más (costo 2 → 3,01).
- **Redondeo de líneas en el frontend:** en enteros (centavos), nunca con `Math.round` sobre `number` (2,01 × 50 % daría 1,00 en lugar de 1,01). Tiene que dar lo mismo que el servidor.
- **Numeración y emisión:** los números de presupuesto salen de una transacción `BEGIN IMMEDIATE`; las facturas se emiten de a una, con el número registrado antes de enviar a ARCA (`EmisionPendiente`, RF-89) y consulta previa en cada reintento (RF-65).
- **Errores de validación:** `ValidationProblem` con la clave del campo (`cliente.dni`, `lineas[0].cantidad`) y un mensaje que dice cómo corregirlo (RF-35); el frontend lo muestra junto al campo.

## Pendientes de confirmar con el responsable del proyecto
- Margen de utilidad limitado a 0–100 % (RF-21); en óptica suele superarse el 100 %.
- Número inicial de los presupuestos (hoy arranca en 1).
- Tope de identificación inicial ($10.000.000, cargado sin confirmar) y datos de RF-30 e IVA contenido (Ley 27.743): validar con el contador.
- Limitar la alícuota de Configuración a los valores que acepta ARCA (0; 2,5; 5; 10,5; 21; 27).
- Fuera del plan actual: importación de precios desde Excel (RF-22 a RF-24, RF-78 a RF-80, RF-93) y backups (RNF-02, RNF-05, RNF-06, RF-77).
