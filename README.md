# Óptica Sistema: presupuestos y facturación electrónica

Aplicación web para Óptica Sistema:

- **Catálogo:** precio de venta calculado a partir del costo y el margen de utilidad.
- **Presupuestos:** numerados, con búsqueda y PDF.
- **Facturación electrónica a consumidor final:** Factura B o C con CAE de ARCA.

Los requerimientos completos están en [`PRD.md`](PRD.md). Las decisiones y convenciones para quien desarrolle están en [`AGENTS.md`](AGENTS.md).

> **ARCA está simulado.** No hay certificado de homologación, así que la facturación funciona contra un
> simulador del web service WSFEv1 incluido en el proyecto. Las facturas que se emiten son de prueba: su PDF
> dice **"COMPROBANTE SIMULADO — SIN VALIDEZ FISCAL"** y su CAE no existe en ARCA. No hace falta ningún
> certificado ni conexión con ARCA para probar la aplicación.

## Requisitos

| Herramienta | Versión | Descarga |
|---|---|---|
| SDK de .NET | 10 | https://dotnet.microsoft.com/download/dotnet/10.0 |
| Node.js | 22.12 o superior (o 20.19 o superior) | https://nodejs.org |
| Navegador | Chrome o Edge actual | — |

Para comprobarlo:
```bash
dotnet --version   # 10.x
node --version     # v22.12 o superior
```

No hace falta instalar una base de datos: la aplicación usa SQLite, un archivo que se crea solo.

## Cómo levantarla

Se necesitan **dos terminales**: una para el backend (la API) y otra para el frontend. Los comandos son iguales en
Windows (PowerShell), macOS y Linux. Hay que correrlos desde la carpeta del proyecto, la que tiene este README.

**Terminal 1, backend:**
```bash
dotnet run --project backend/Optica.Api
```
Queda escuchando en `http://localhost:5220` cuando aparece `Now listening on: http://localhost:5220`. La primera
vez descarga los paquetes NuGet y crea la base `backend/Optica.Api/optica.db` con todas sus tablas.

**Terminal 2, frontend:**
```bash
cd frontend
npm ci
npm run dev
```
`npm ci` solo hace falta la primera vez. Instala las dependencias exactas de `package-lock.json`.

**Abrí http://localhost:5173 en el navegador.** El frontend reenvía las llamadas a `/api` al backend, así que no
hay que configurar nada más.

Para detener cada servidor, pulsá `Ctrl+C` en su terminal.

## Primer uso

1. La primera pantalla es **"Definir contraseña"**: elegí una contraseña de al menos 8 caracteres. Solo se puede
   definir desde la misma PC donde corre el backend.
2. Con esa contraseña se ingresa las veces siguientes.
   - **Bloqueo:** después de 5 intentos fallidos el acceso se bloquea 5 minutos.
   - **Inactividad:** la sesión se cierra tras 60 minutos sin uso.

La base arranca vacía: no hay artículos ni presupuestos de ejemplo. El recorrido siguiente los crea en unos minutos.

## Recorrido sugerido

1. **Configuración.** Viene con Responsable Inscripto (emite Factura B), IVA 21 %, tope de identificación de
   $10.000.000 y múltiplo de redondeo 0,01.
2. **Catálogo → Nuevo artículo.** Cargá, por ejemplo:
   - Código en el proveedor `ABC-1`, costo `1210` y margen `50`. El precio de venta da **$ 1.815,00** (AC-36).
   - Dos o tres artículos más.
3. **Configuración:** cambiá el múltiplo de redondeo a `50` y guardá. El aviso dice cuántos precios se recalcularon,
   y en el catálogo el artículo anterior pasa a **$ 1.850,00** (AC-85). Volvé el múltiplo a `0,01`.
4. **Presupuestos → Nuevo presupuesto.**
   - Grabá sin DNI: aparece "Ingresá el DNI del cliente" junto al campo (AC-34).
   - Completá apellido, nombre y DNI. Buscá artículos con **Agregar artículo** y cambiá cantidad y descuento: los
     importes se recalculan en vivo.
   - Probá una cantidad `2,5` o un descuento `120` para ver los mensajes de error.
   - **Grabar:** el presupuesto queda en **Borrador**. "Descargar PDF" y "Facturar" están deshabilitados.
5. **Pasalo a Final.** En **Estado**, elegí **Final** y grabá. Pide confirmación y desde ahí no se puede modificar.
6. **Descargar PDF** del presupuesto: tiene logo, colores de la marca y la leyenda "Precios finales, IVA incluido".
7. **Facturar.** Pide confirmación, emite la factura simulada y muestra comprobante, fecha y CAE. Desde ahí se
   descarga el PDF de la factura, con todos los datos de RF-30 y el código QR.
8. **Facturas:** listado con búsqueda por fecha, número de comprobante (por ejemplo `1` o `0001-00000001`) y cliente.
9. **Presupuestos:** buscá por apellido sin acentos (`gonzalez` encuentra "González") o por parte del DNI.

### Probar los errores de ARCA

En `backend/Optica.Api/appsettings.json`, cambiá `Arca` → `Simulador` → `Modo` y reiniciá el backend
(`Ctrl+C` y de nuevo `dotnet run ...`):

| Modo | Qué pasa al facturar |
|---|---|
| `Normal` | ARCA autoriza (valor por defecto) |
| `Rechazar` | ARCA rechaza: se muestran código y descripción del error, no se registra nada y se puede reintentar (AC-20) |
| `SinRespuesta` | ARCA no responde: a los `TiempoEsperaSegundos` (30 por defecto) se abandona la espera sin reintentar sola (AC-60) |
| `AutorizarSinResponder` | ARCA autoriza pero la respuesta no llega. Volvé a `Normal`, reiniciá y reintentá: el sistema recupera el CAE ya otorgado sin emitir otra factura (AC-64) |

Para no esperar 30 segundos, podés bajar `TiempoEsperaSegundos` a `3` mientras probás.

## Tests

```bash
dotnet test Optica.slnx                                   # backend: unitarios e integración
cd frontend && npm test && npm run build && npm run lint  # frontend: tests, tipos y lint
```

Los tests del backend no usan la base real: cada uno crea una base SQLite temporal y la borra al terminar.

## Qué no está en el repositorio y por qué

| Archivo o carpeta | Por qué no está | Qué hacer |
|---|---|---|
| `backend/Optica.Api/optica.db` (y `-wal`, `-shm`) | Guarda datos personales de clientes (DNI, domicilio, teléfono, email) | Nada: se crea sola al levantar el backend |
| `frontend/node_modules/` | Dependencias de npm, propias de cada sistema operativo | `npm ci` en `frontend/` |
| `bin/`, `obj/` | Resultados de compilación | Nada: los genera `dotnet run` o `dotnet test` |
| `backend/Optica.Api/arca-simulado.json` | Comprobantes "autorizados" por el simulador de ARCA | Nada: lo crea el simulador al facturar |
| Certificado digital de ARCA (`.crt`, `.key`, `.pfx`) | Es una credencial fiscal de la empresa (RF-64) | No hace falta para probar: ARCA está simulado |

Los datos del emisor que aparecen en el PDF de la factura (razón social, domicilio, CUIT, etc.) están en la sección
`Emisor` de `appsettings.json` con el texto "COMPLETAR". No son necesarios para probar.

## Estructura

```
backend/Optica.Api/    API ASP.NET Core (.NET 10 + EF Core 10 + SQLite)
  Acceso/              contraseña, bloqueo e inactividad
  Configuracion/       parámetros de negocio y recálculo del catálogo
  Catalogo/            artículos y cálculo del precio de venta
  Presupuestos/        presupuestos, líneas, búsqueda y PDF
  Facturacion/         armado del comprobante, emisión con reintento, PDF y QR
  Arca/                adaptador de WSFEv1 y su simulador (aislado por cambios normativos)
tests/Optica.Tests/    tests de backend (xUnit)
frontend/              React 19 + TypeScript + Vite
Marca/                 logo y colores de la óptica (fuente única para pantallas y PDF)
```

## Problemas frecuentes

- **"An Application Control policy has blocked this file" (Windows).** Smart App Control de Windows 11 a veces
  bloquea los DLL recién compilados. Alternativas: correr el proyecto desde WSL, o desactivar Smart App Control
  en Seguridad de Windows → Control de aplicaciones y navegador.
- **"Access to the path ... Optica.Api.dll is denied" al compilar o testear.** Hay un backend corriendo que
  bloquea el archivo: detenelo con `Ctrl+C`, o corré los tests con `dotnet test Optica.slnx -c Release`.
- **El puerto 5220 o 5173 está ocupado.** Cerrá el otro programa que lo usa. El backend usa el 5220 y el frontend
  le reenvía las llamadas ahí, así que ese puerto no se puede cambiar sin tocar `frontend/vite.config.ts`.
- **La pantalla dice "No se pudo conectar con el sistema".** El backend no está corriendo, o está corriendo en otro
  entorno. Backend y frontend tienen que estar en el mismo lado: los dos en Windows o los dos en WSL.
- **`npm ci` o `npm run dev` fallan con errores de binarios nativos** (`rolldown`, `esbuild`). La carpeta
  `node_modules` se instaló en otro sistema operativo: borrala y volvé a correr `npm ci`.
- **Quiero empezar de cero.** Con el backend detenido, borrá `backend/Optica.Api/optica.db*` y
  `backend/Optica.Api/arca-simulado.json`.
