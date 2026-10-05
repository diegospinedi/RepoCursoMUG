---
name: frontend-design
description: Guía visual de Óptica Sistema (colores, tipografías, componentes y formatos) derivada del logo y de Marca/branding.json. Se usa al crear o modificar cualquier pantalla, componente o estilo del frontend, y al generar los PDF de presupuesto y factura.
---

# Frontend Design — Óptica Sistema

La estética sale del logo (`Marca/logo.png`): un ojo y el nombre en azul sólido
sobre blanco, tipografía geométrica ancha en mayúsculas y una línea secundaria
condensada en itálica. El sitio debe verse **sobrio, limpio y confiable**: es una
herramienta de trabajo diario para dos operadoras, no una landing page.

## Reglas obligatorias

1. **Una sola fuente de verdad.** El color primario y el fondo se leen de
   `Marca/branding.json` (vía `src/marca.ts`). Nunca escribas `#0903A0` ni
   `#FFFFFF` a mano en componentes o CSS.
2. **Solo tokens.** Todo color, tamaño de fuente, espaciado, radio y sombra sale
   de las variables de `frontend/src/estilos/tokens.css`. Si falta un token,
   agregalo ahí; no inventes valores sueltos en un componente.
3. **Sin dependencias de internet.** Las fuentes se instalan con `@fontsource`
   (empaquetadas por Vite), nunca desde Google Fonts ni CDN: presupuestos y
   catálogo tienen que funcionar sin conexión.
4. **Sin frameworks de UI ni de CSS** (Tailwind, MUI, Bootstrap). CSS plano con
   las clases de `frontend/src/estilos/componentes.css`.
5. **Chrome y Edge** estable y anterior (RNF-03): se puede usar `color-mix()`,
   `:has()`, grid y nesting de CSS.

## Color

| Token | Valor | Uso |
|---|---|---|
| `--color-primario` | de `branding.json` (#0903A0) | botones principales, títulos, links, foco |
| `--color-primario-oscuro` | `color-mix` con 25 % de negro | hover/active del primario |
| `--color-primario-suave` | `color-mix` con 8 % sobre el fondo | fondo de encabezados de grilla, fila seleccionada |
| `--color-fondo` | de `branding.json` (#FFFFFF) | fondo de tarjetas y superficies |
| `--color-superficie` | gris azulado muy claro | fondo de la página, detrás de las tarjetas |
| `--color-borde` | gris claro | bordes de inputs, tarjetas y grillas |
| `--color-texto` / `--color-texto-suave` | casi negro / gris medio | texto general / ayudas y etiquetas |
| `--color-error`, `--color-exito`, `--color-aviso` | rojo, verde, ámbar | solo para estados, nunca decorativos |

- El azul es el **único** color de marca. No agregues acentos de otros colores.
- Un solo botón primario por pantalla o formulario; el resto, secundarios.

## Tipografía

- **Montserrat** (600/700): títulos, nombre de la app y encabezados de grilla. Es
  la geométrica ancha que más se parece al "OPTICA SISTEMA" del logo. Los
  encabezados de grilla y las etiquetas de estado van en mayúsculas con
  `letter-spacing` leve.
- **Barlow** (400/500/600): todo el texto, formularios y grillas. Es sobria y
  muy legible, y su variante condensada evoca la línea de la dirección del logo.
- Importes y números de comprobante: `font-variant-numeric: tabular-nums` y
  alineados a la derecha, para que los decimales queden en columna.

## Formatos (Argentina)

- Importes: `$ 1.815,00`, siempre con 2 decimales, con
  `Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' })`.
- Fechas: `dd/mm/aaaa`.
- DNI: `23.456.789`. Comprobante: `0003-00034561`.
- Textos de interfaz en español rioplatense neutro y formal-cercano ("Ingresá el
  DNI del cliente"). Sin lunfardo.

## Componentes (clases de `componentes.css`)

- **Botones:** `.boton` (secundario: borde primario, fondo blanco),
  `.boton.boton-primario` (fondo primario, texto blanco), `.boton-peligro`
  solo para acciones irreversibles. Deshabilitado: opacidad 0,5 y
  `cursor: not-allowed` (por ejemplo, Descargar PDF en Borrador, AC-08).
- **Campos:** `.campo` envuelve `label` + `input` + `.campo-error`. El error va
  **junto al campo** que lo causa, en rojo, con la acción correctiva (RF-35).
  El input con error lleva `aria-invalid="true"` y borde rojo.
- **Tarjeta:** `.tarjeta` para agrupar secciones de un formulario (Cliente,
  Líneas, Totales).
- **Grilla:** `.grilla` con encabezado en `--color-primario-suave`, filas con
  separador fino, hover suave y columnas numéricas con `.numero`.
- **Estados:** `.etiqueta.etiqueta-borrador` (ámbar) y `.etiqueta.etiqueta-final`
  (primario). Las facturas con CAE usan `.etiqueta-final`.
- **Avisos:** `.aviso.aviso-error | aviso-exito | aviso-info` para mensajes de
  confirmación ("Presupuesto 155 grabado en estado Borrador") y errores de ARCA.

## Layout

- Encabezado blanco con el logo (72 px de alto) a la izquierda, navegación a la
  derecha y una línea inferior de 4 px en el primario.
- Contenido centrado, máximo 1200 px, sobre `--color-superficie`, con las
  secciones dentro de tarjetas blancas.
- Pensado para escritorio (la PC del local), pero sin scroll horizontal en
  pantallas de 1280 px. Las grillas anchas scrollean dentro de su tarjeta.

## Accesibilidad

- Todo campo con `label` asociado. Foco visible: anillo de 3 px en
  `--color-primario-suave` + borde primario.
- Contraste mínimo AA. El primario sobre blanco lo supera con holgura; no uses
  `--color-texto-suave` para información crítica.

## PDF (presupuesto y factura)

Mismo criterio (RF-55): logo arriba a la izquierda, títulos en el primario,
Montserrat para títulos y Barlow para el cuerpo, importes alineados a la derecha
con formato argentino y la leyenda "Precios finales, IVA incluido" en el
presupuesto (RF-37).
