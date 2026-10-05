# PRD-001: Presupuestos y Facturación Electrónica Online — Sistema web para Óptica Sistema: presupuestos con catálogo de precios calculados y factura electrónica a consumidor final vía ARCA.

## Contexto y Problema
Actualmente la empresa Óptica Sistema factura online ingresando al sitio de ARCA con el usuario y la contraseña de la empresa. Por otro lado, los presupuestos se realizan a mano: se escriben los productos y se colocan los precios que se buscan en un libro/folleto que envían los proveedores. Esto genera dos problemas: pueden equivocarse de producto y/o de precio, y además los precios del folleto son los precios de costo (lo que la empresa paga al proveedor), sin incluir el margen de ganancia, por lo que el cálculo del precio de venta también se hace a mano.

Este sistema lo usarán Eugenia y Verónica, que son las dueñas del negocio y a su vez quienes trabajan todos los días en la empresa. Ellas necesitan poder hacer presupuestos online desde una página web, seleccionando los artículos desde un catálogo con precios de venta ya calculados, y que cada presupuesto quede guardado en una base de datos local. Además, esta versión incluye la facturación electrónica a consumidores finales mediante los web services de ARCA (ex AFIP), reemplazando la carga manual en el sitio web del organismo.

## Objetivos
- Tener un registro centralizado de los presupuestos: un solo lugar donde saben que están todos, con posibilidad de buscarlos si el cliente los olvida o los pide de nuevo.
- Evitar errores de precio y de escritura/transcripción de los productos, seleccionando los artículos desde un catálogo con precio de venta calculado automáticamente a partir del costo y el margen de utilidad.
- Poder generar el presupuesto en formato PDF para entregarlo o enviarlo manualmente por email o WhatsApp.
- Facturar electrónicamente a consumidores finales desde el sistema, integrándose con ARCA (ex AFIP), sin tener que cargar los datos manualmente en el sitio del organismo.

## Requerimientos Funcionales

### Presupuestos
- RF-01: El sistema debe permitir grabar un presupuesto nuevo.
- RF-02: El sistema debe permitir generar el presupuesto en formato PDF.
- RF-03: El sistema debe permitir buscar presupuestos por fecha y por datos del cliente (apellido, nombre, DNI).
- RF-04: El sistema debe manejar dos estados del presupuesto: Borrador y Final.
- RF-05: El sistema debe permitir cargar en el presupuesto los siguientes datos del cliente: Apellido, Nombre, DNI, Domicilio, Email y Nro. de Teléfono.
- RF-06: El sistema debe asignar a cada presupuesto un número autoincremental generado automáticamente.
- RF-07: El sistema debe permitir modificar un presupuesto únicamente mientras se encuentre en estado Borrador.
- RF-08: El sistema no debe permitir modificar un presupuesto en estado Final ni devolverlo al estado Borrador.
- RF-09: El sistema no debe permitir imprimir ni descargar el PDF de un presupuesto cuyo estado sea Borrador.

### Líneas del presupuesto y cálculos
- RF-10: El sistema debe permitir cargar productos por línea en el presupuesto.
- RF-11: El sistema debe permitir seleccionar un artículo del catálogo al cargar una línea, buscándolo por código o por descripción.
- RF-12: El sistema debe registrar en cada línea del presupuesto los siguientes campos: Código de Artículo, Descripción, Precio Unitario, Cantidad, Porcentaje de Descuento, Precio con Descuento y Precio Final.
- RF-13: El sistema debe calcular el precio con descuento de cada línea como: Precio Unitario × (1 − Porcentaje de Descuento / 100).
- RF-14: El sistema debe calcular el precio final de cada línea como el producto del precio con descuento por la cantidad.
- RF-15: El sistema debe calcular el total del presupuesto como la sumatoria de los precios finales, ya redondeados, de todas las líneas.
- RF-16: El sistema no debe admitir cantidades negativas ni iguales a cero.
- RF-17: El sistema no debe admitir precios unitarios negativos.
- RF-18: El sistema debe expresar con IVA incluido (precio final al consumidor) todos los precios que muestra en pantalla y en el presupuesto.
- RF-19: El sistema debe redondear los importes a 2 decimales usando redondeo estándar (mitad hacia arriba).

### Catálogo de artículos y precios
- RF-20: El sistema debe permitir cargar artículos con los siguientes campos: Código (autonumérico), Código en el proveedor, Descripción, Precio de Costo (precio final del folleto del proveedor, con IVA incluido) y Margen de Utilidad (%). El Precio de Venta (precio final al consumidor, con IVA incluido) no se carga: es un campo derivado que el sistema calcula según RF-21 y de solo lectura para la operadora.
- RF-21: El sistema debe calcular el precio de venta de cada artículo en tres pasos: (a) Costo sin IVA = Precio de Costo / (1 + alícuota de IVA / 100); (b) Precio de Venta sin IVA = Costo sin IVA × (1 + Margen de Utilidad / 100); (c) Precio de Venta = Precio de Venta sin IVA × (1 + alícuota de IVA / 100). El margen y la alícuota se expresan en porcentaje (0 a 100), con el mismo criterio que RF-13. Cuando la alícuota de compra y la de venta coinciden, el resultado equivale a Precio de Costo × (1 + Margen de Utilidad / 100).
- RF-22: El sistema debe permitir actualizar los precios de costo de los artículos a partir de una planilla Excel con un formato predeterminado.
- RF-23: El sistema debe aceptar una planilla Excel con dos columnas: Código de artículo en el proveedor y Precio de Costo Nuevo (mismo criterio que RF-20: precio final del proveedor, con IVA incluido).
- RF-24: El sistema debe mostrar en pantalla la lista de los artículos que no pudo actualizar y la razón de cada caso (por ejemplo: código inexistente, precio inválido).

### Facturación electrónica a consumidor final (ARCA)
- RF-25: El sistema debe permitir generar una factura electrónica a consumidor final a partir de un presupuesto en estado Final, tomando de él los datos del cliente y las líneas de artículos.
- RF-26: El sistema debe emitir Factura B como comprobante a consumidor final, dado que la óptica es Responsable Inscripto frente a ARCA.
- RF-27: El sistema debe identificar al receptor como "Consumidor Final" sin identificar, o con DNI cuando el monto de la operación supere el tope vigente establecido por la normativa de ARCA para identificar al receptor.
- RF-28: El sistema debe solicitar la autorización del comprobante (CAE) mediante el web service de facturación electrónica de ARCA (WSFEv1), utilizando el certificado digital de la empresa y el punto de venta habilitado para web services.
- RF-29: El sistema debe guardar el comprobante autorizado con su número (por punto de venta), CAE y fecha de vencimiento del CAE, asociado al presupuesto que le dio origen.
- RF-30: El sistema debe generar el PDF de la factura con todos los datos obligatorios según la normativa vigente, incluyendo el código QR de ARCA.
- RF-31: El sistema no debe emitir el comprobante si ARCA rechaza la solicitud o si el servicio no está disponible.
- RF-32: El sistema no debe permitir modificar ni eliminar una factura emitida con CAE.
- RF-33: El sistema debe permitir listar las facturas emitidas.

### Interfaz
- RF-34: El sistema debe mostrar en su interfaz el logo y la paleta de colores de la óptica.
- RF-35: El sistema debe indicar, en todo error de validación, el campo afectado y la acción correctiva que debe tomar la operadora.

### Requisitos derivados
Los siguientes requisitos surgen de desagregar RF anteriores que reunían más de una acción, y de resolver las contradicciones y los vacíos detectados al auditar el documento. Se numeran a continuación de RF-35 para no alterar la numeración ya existente; cada uno indica su origen.

- RF-36: El sistema debe permitir descargar en la PC el PDF del presupuesto generado, para que la operadora lo adjunte manualmente en un email o en un mensaje de WhatsApp. (Deriva de RF-02.)
- RF-37: El sistema debe incluir en el PDF del presupuesto la leyenda "Precios finales, IVA incluido". (Deriva de RF-02; ver RF-18.)
- RF-38: El sistema debe guardar los datos del cliente dentro del propio presupuesto. (Deriva de RF-05.)
- RF-39: El sistema debe completar automáticamente el código, la descripción y el precio unitario de la línea al seleccionarse un artículo, tomando como precio unitario el precio de venta del catálogo. (Deriva de RF-11.)
- RF-40: El sistema debe permitir ajustar la cantidad y el porcentaje de descuento de la línea después de seleccionar el artículo. (Deriva de RF-11.)
- RF-41: El sistema debe mostrar el precio con descuento en todas las líneas, incluidas aquellas cuyo porcentaje de descuento es cero, en cuyo caso es igual al precio unitario. (Deriva de RF-13.)
- RF-42: El sistema no debe discriminar IVA en el presupuesto. (Deriva de RF-18.)
- RF-43: El sistema debe calcular el desglose de neto e IVA únicamente al emitir la factura electrónica, porque lo exige el web service de ARCA (RF-28). (Deriva de RF-18.)
- RF-44: El sistema debe aplicar el redondeo a nivel de línea, primero sobre el precio con descuento y luego sobre el precio final de la línea. (Deriva de RF-19.)
- RF-45: El sistema debe recalcular automáticamente el precio de venta de un artículo cuando se modifique su precio de costo o su margen de utilidad. (Deriva de RF-21.)
- RF-46: El sistema debe tomar la alícuota de IVA, expresada en porcentaje (por ejemplo, 21), de un único parámetro de configuración, aplicable a todo el catálogo y al desglose de neto e IVA de la factura (RF-43). (Deriva de RF-21.)
- RF-47: El sistema debe recorrer la planilla Excel cargada y actualizar el precio de costo de cada artículo coincidente, recalculando su precio de venta según RF-21. (Deriva de RF-23.)
- RF-48: El sistema debe tomar la condición fiscal de la óptica de un parámetro de configuración que determina el tipo de comprobante (Responsable Inscripto → B; Monotributo → C). (Deriva de RF-26.)
- RF-49: El sistema debe tomar el tope de identificación del receptor de un parámetro configurable. (Deriva de RF-27.)
- RF-50: El sistema debe permitir descargar e imprimir el PDF de la factura. (Deriva de RF-30.)
- RF-51: El sistema debe mostrar a la operadora, en un mensaje claro, el motivo del rechazo informado por ARCA. (Deriva de RF-31.)
- RF-52: El sistema debe permitir reintentar más tarde la emisión del comprobante. (Deriva de RF-31.)
- RF-53: El sistema no debe dejar ningún comprobante en estado intermedio. (Deriva de RF-31.)
- RF-54: El sistema debe permitir buscar las facturas emitidas por fecha, número de comprobante y datos del cliente. (Deriva de RF-33.)
- RF-55: El sistema debe incluir el logo y la paleta de colores de la óptica en los PDF generados de presupuesto y de factura. (Deriva de RF-34.)
- RF-56: El sistema debe redondear el precio de venta resultante de RF-21 hacia arriba, hasta el múltiplo de redondeo comercial definido en la configuración, de modo que el precio de venta nunca quede por debajo del valor calculado y el margen cargado opere como piso. (Resuelve la contradicción entre RF-20 y RF-21.)
- RF-57: El sistema debe tomar el múltiplo de redondeo comercial de un parámetro de configuración, cuyo valor mínimo admitido es 0,01 (equivalente a redondear únicamente a 2 decimales según RF-19). (Deriva de RF-56.)
- RF-58: El sistema debe guardar el precio de venta ya redondeado y usar ese valor, sin volver a calcularlo, como precio unitario de la línea del presupuesto (RF-39). (Resuelve el alcance del redondeo entre RF-19 y RF-44.)
- RF-59: El sistema no debe admitir porcentajes de descuento menores a 0 ni mayores a 100. (Completa las validaciones de RF-16 y RF-17; evita el precio de línea negativo que resultaría de RF-13.)
- RF-60: El sistema debe admitir únicamente cantidades enteras en las líneas del presupuesto. (Completa RF-16.)
- RF-61: El sistema debe exigir Apellido, Nombre y DNI del cliente para grabar un presupuesto, y debe tratar Domicilio, Email y Nro. de Teléfono como datos opcionales. (Completa RF-05 y da sustento a AC-34.)
- RF-62: El sistema no debe permitir emitir una factura a partir de un presupuesto en estado Borrador. (Hace explícita la prohibición que AC-17 deducía de RF-25; simétrica con RF-09.)
- RF-63: El sistema debe permitir administrar, desde una pantalla de configuración protegida por el acceso de RNF-04, los siguientes parámetros de negocio: alícuota de IVA (RF-46), condición fiscal (RF-48), tope de identificación del receptor (RF-49) y múltiplo de redondeo comercial (RF-57).
- RF-64: El sistema no debe exponer en su interfaz el certificado digital de la empresa ni el punto de venta habilitado para web services (RF-28): ambos se instalan y se resguardan fuera de la aplicación. (Sostiene la mitigación del riesgo de acceso no autorizado a datos fiscales.)
- RF-65: El sistema debe consultar a ARCA el último comprobante autorizado del punto de venta antes de reintentar una emisión que haya quedado sin respuesta (RF-52). (Un timeout no distingue si ARCA autorizó o no; sin esta consulta, RF-53 no puede cumplirse.)
- RF-66: El sistema no debe emitir un comprobante nuevo cuando la consulta de RF-65 indique que la operación ya fue autorizada: debe recuperar el CAE ya otorgado y guardarlo según RF-29. (Evita el comprobante duplicado, que no puede anularse desde el sistema porque las notas de crédito están fuera de alcance.)

## Requerimientos No Funcionales
- RNF-01: La búsqueda de presupuestos debe responder en menos de 2 segundos en el percentil 95, con hasta 10.000 presupuestos almacenados. El tiempo se mide desde que la operadora presiona Buscar hasta que la grilla muestra los resultados en pantalla, no en la respuesta del API.
- RNF-02: El sistema debe realizar un backup automático diario de la base de datos en una ubicación distinta al disco principal (pendrive, carpeta sincronizada en la nube u otra), ejecutado al cierre de la jornada comercial, con la PC del local encendida.
- RNF-03: La interfaz debe funcionar correctamente en Chrome y en Edge, tanto en su versión estable vigente como en la inmediata anterior.
- RNF-04: El acceso a la aplicación debe estar protegido por una contraseña de 8 caracteres como mínimo, dado que la base de datos almacena datos personales de clientes (DNI, domicilio, teléfono, email) alcanzados por la Ley 25.326 de Protección de Datos Personales.
- RNF-05: El sistema debe conservar las 7 copias diarias de backup más recientes y 4 copias mensuales. (Deriva de RNF-02; la retención es lo que cubre la corrupción detectada tarde, que la sola copia diaria sobreescrita no cubre.)
- RNF-06: La restauración de la base a partir de una copia de backup debe completarse en menos de 4 horas, siguiendo un procedimiento documentado. (Deriva de RNF-02.)
- RNF-07: El sistema debe avisar a la operadora, al ingresar, cuando el backup del día anterior no se haya ejecutado, indicando la fecha del último backup disponible. (Deriva de RNF-02; sin este aviso, un backup que dejó de correr solo se descubre cuando ya hace falta.)
- RNF-08: El sistema debe bloquear el acceso durante 5 minutos tras 5 intentos de ingreso fallidos consecutivos. El bloqueo debe ser temporal y liberarse solo, dado que no existe un módulo de usuarios con un administrador que pueda desbloquearlo. (Deriva de RNF-04.)
- RNF-09: El sistema debe cerrar la sesión tras 60 minutos de inactividad y volver a exigir la contraseña. (Deriva de RNF-04.)
- RNF-10: El sistema debe dar por no disponible el web service de ARCA tras 30 segundos sin respuesta, y no debe reintentar la emisión por su cuenta: el reintento lo decide la operadora (RF-52).
- RNF-11: La emisión de una factura debe completarse en menos de 10 segundos en el percentil 95 cuando ARCA responde con normalidad, medido desde que la operadora presiona Facturar hasta el mensaje de confirmación. De ese total, el tiempo agregado por el sistema, excluida la respuesta de ARCA, debe ser menor a 2 segundos.
- RNF-12: El sistema debe soportar un catálogo de hasta 10.000 artículos y procesar una planilla Excel de hasta 10.000 filas en menos de 120 segundos.
- RNF-13: El sistema debe soportar 2 sesiones simultáneas grabando presupuestos sin que ninguna reciba errores de bloqueo de la base de datos.

## Criterios de Aceptación
- AC-01 (RF-01): Dado un presupuesto cargado con los campos número, apellido, nombre, DNI, dirección, teléfono y email, y al menos 1 artículo con código, descripción y precio, cuando presiono Grabar, entonces debe grabarse el presupuesto y mostrarse un mensaje que confirme que se grabó.
- AC-02 (RF-02, RF-09, RF-36, RF-55): Dado un presupuesto en estado Final, cuando presiono Descargar PDF, entonces debe generarse un PDF con el logo y colores de la óptica y descargarse en la PC.
- AC-03 (RF-03): Posicionada en la opción de búsqueda de presupuestos, al ingresar total o parcialmente los datos de la grilla (apellido, nombre, DNI, fecha), entonces el sistema debe filtrar todos los registros que coincidan con parte o con todos los datos ingresados.
- AC-04 (RF-04): Dado un presupuesto cargado con los campos número, apellido, nombre, DNI, dirección, teléfono y email, y al menos 1 artículo, cuando presiono Grabar por primera vez, entonces debe grabarse el presupuesto en estado Borrador y mostrarse un mensaje que confirme que se grabó en ese estado.
- AC-05 (RF-04, RF-08): Dado un presupuesto en estado Borrador, cuando cambio el estado a Final y grabo, entonces el sistema lo graba en estado Final y ya no es modificable ni puede volver a Borrador.
- AC-06 (RF-06): Dado que presiono Nuevo, entonces el sistema debe generar un nuevo número de presupuesto incremental.
- AC-07 (RF-07): Dado un presupuesto en estado Borrador, entonces el sistema debe permitir editarlo, cambiar un dato y grabar, mostrando un mensaje de confirmación a la operadora.
- AC-08 (RF-09): Dado un presupuesto en estado Borrador, entonces el botón Descargar PDF está deshabilitado.
- AC-09 (RF-09, RF-36): Dado un presupuesto en estado Final, entonces el sistema habilita el botón Descargar PDF, que descarga el presupuesto en la PC.
- AC-10 (RF-11, RF-39): Dado que estoy cargando una línea del presupuesto, cuando busco un artículo por código o descripción y lo selecciono, entonces el sistema completa automáticamente el código, la descripción y el precio unitario con el precio de venta del catálogo.
- AC-11 (RF-15): Dado un presupuesto con 2 o más líneas cargadas, entonces el sistema muestra el total como la sumatoria de los precios finales de cada línea.
- AC-12 (RF-16, RF-17): Dado que intento cargar una cantidad negativa o cero, o un precio unitario negativo, entonces el sistema no lo permite y muestra un mensaje indicando el campo con error.
- AC-13 (RF-21, RF-57): Dado el múltiplo de redondeo comercial configurado en $0,01 y un artículo con precio de costo $1.000 y margen de utilidad 50%, entonces el precio de venta calculado es $1.500,00.
- AC-14 (RF-22, RF-23, RF-45, RF-47, RF-56): Dada una planilla Excel con el formato predeterminado, cuando la cargo en el módulo de actualización, entonces el sistema actualiza los precios de costo de los artículos coincidentes y recalcula sus precios de venta.
- AC-15 (RF-24): Dada una planilla Excel que contiene un código de proveedor inexistente, cuando la proceso, entonces el sistema actualiza los artículos válidos y muestra en pantalla una lista con los artículos no actualizados y la razón.
- AC-16 (RF-25): Dado un presupuesto en estado Final, cuando presiono Facturar, entonces el sistema genera la solicitud de factura a consumidor final con los datos y líneas del presupuesto, sin necesidad de recargarlos.
- AC-17 (RF-25, RF-62): Dado un presupuesto en estado Borrador, entonces el botón Facturar está deshabilitado.
- AC-18 (RF-28, RF-29): Dado un presupuesto en estado Final, cuando presiono Facturar y ARCA autoriza el comprobante, entonces el sistema guarda la factura con número, CAE y vencimiento del CAE, y muestra un mensaje de confirmación con el número de comprobante.
- AC-19 (RF-30, RF-50): Dada una factura autorizada, cuando presiono Descargar PDF, entonces se genera y descarga un PDF con los datos obligatorios del comprobante y el código QR de ARCA.
- AC-20 (RF-31, RF-51, RF-52, RF-53): Dado que ARCA rechaza la solicitud (por ejemplo, por datos inválidos) o el servicio no responde, entonces el sistema no registra ninguna factura, muestra el motivo en pantalla y permite reintentar.
- AC-21 (RF-27, RF-49): Dado un presupuesto cuyo total supera el tope configurado para identificar al receptor, cuando presiono Facturar, entonces el sistema envía a ARCA el DNI del cliente como identificación del receptor.
- AC-22 (RF-33, RF-54): Posicionada en la búsqueda de facturas, al ingresar fecha, número o datos del cliente, entonces el sistema filtra las facturas que coinciden con los datos ingresados.
- AC-23 (RF-05, RF-38): Dado un presupuesto nuevo, cuando cargo Apellido, Nombre, DNI, Domicilio, Email y Nro. de Teléfono del cliente y grabo, entonces el sistema guarda esos seis datos dentro del presupuesto y los muestra al volver a abrirlo.
- AC-24 (RF-10): Dado un presupuesto en estado Borrador, cuando agrego una línea de producto y grabo, entonces la línea queda guardada en el presupuesto y se muestra en la grilla de líneas.
- AC-25 (RF-12): Dada una línea cargada, entonces la grilla muestra para esa línea los siete campos: Código de Artículo, Descripción, Precio Unitario, Cantidad, Porcentaje de Descuento, Precio con Descuento y Precio Final.
- AC-26 (RF-13, RF-41): Dada una línea con precio unitario $1.000 y porcentaje de descuento igual a cero, entonces el precio con descuento se muestra completo y es $1.000.
- AC-27 (RF-14): Dada una línea con precio con descuento $900 y cantidad 3, entonces el precio final de la línea es $2.700.
- AC-28 (RF-18, RF-42): Dado un presupuesto con líneas cargadas, entonces ni las líneas ni el total discriminan IVA, y los importes mostrados corresponden al precio final al consumidor con IVA incluido.
- AC-29 (RF-19, RF-44): Dada una línea cuyo precio con descuento sin redondear es $3,335 y cuya cantidad es 3, entonces el sistema redondea primero el precio con descuento a $3,34 (2 decimales, mitad hacia arriba) y muestra un precio final de línea de $10,02, de modo que la multiplicación exhibida cierra.
- AC-30 (RF-20, RF-58): Dado que cargo un artículo nuevo indicando Código en el proveedor, Descripción, Precio de Costo y Margen de Utilidad, cuando grabo, entonces el sistema asigna automáticamente el Código autonumérico y guarda el artículo con su Precio de Venta.
- AC-31 (RF-26, RF-48): Dada la condición fiscal de la óptica configurada como Responsable Inscripto, cuando emito una factura a consumidor final, entonces el comprobante solicitado a ARCA es Factura B.
- AC-32 (RF-32): Dada una factura ya autorizada con CAE, entonces el sistema no ofrece acciones de edición ni de eliminación del comprobante, y cualquier intento de modificarla o eliminarla es rechazado.
- AC-33 (RF-34, RF-55): Dado que abro una pantalla del sistema o genero el PDF de un presupuesto o de una factura, entonces se muestra el logo de la óptica y se aplica su paleta de colores.
- AC-34 (RF-35): Dado que intento grabar con un campo obligatorio vacío o con un valor inválido, entonces el sistema muestra un mensaje que identifica el campo afectado e indica la acción correctiva que debe tomar la operadora.
- AC-35 (RF-13): Dada una línea con precio unitario $1.000 y porcentaje de descuento 10%, entonces el precio con descuento calculado es $900.
- AC-36 (RF-21, RF-57): Dado el múltiplo de redondeo comercial configurado en $0,01, y un artículo con precio de costo $1.210 (final, con IVA al 21%), cuyo costo sin IVA es $1.000, y un margen de utilidad del 50%, entonces el precio de venta sin IVA es $1.500,00 y el precio de venta final es $1.815,00.
- AC-37 (RF-02, RF-18, RF-37): Dado un presupuesto en estado Final, cuando descargo su PDF, entonces el documento muestra la leyenda "Precios finales, IVA incluido".
- AC-38 (RF-56, RF-57): Dado el múltiplo de redondeo comercial configurado en $50 y un artículo cuyo precio de venta calculado según RF-21 es $1.815,37, cuando grabo el artículo, entonces el sistema guarda un precio de venta de $1.850,00.
- AC-39 (RF-56, RF-57): Dado el múltiplo de redondeo comercial configurado en $0,01 y un artículo cuyo precio de venta calculado según RF-21 es $1.666,656, cuando grabo el artículo, entonces el sistema guarda un precio de venta de $1.666,66.
- AC-40 (RF-58): Dado un artículo cuyo precio de venta guardado es $1.850,00, cuando lo selecciono en una línea del presupuesto, entonces el precio unitario de la línea es $1.850,00, tomado tal cual del catálogo y sin volver a calcularlo.
- AC-41 (RF-27, RF-49): Dado un presupuesto cuyo total no supera el tope configurado, cuando presiono Facturar, entonces el sistema identifica al receptor ante ARCA como "Consumidor Final" sin identificar, aun cuando el presupuesto tenga DNI cargado.
- AC-42 (RF-59): Dado que intento cargar un porcentaje de descuento negativo o mayor a 100, entonces el sistema no lo permite y muestra un mensaje indicando el campo con error.
- AC-43 (RF-60): Dado que intento cargar una cantidad con decimales, por ejemplo 2,5, entonces el sistema no lo permite y muestra un mensaje indicando el campo con error.
- AC-44 (RF-61): Dado un presupuesto sin Apellido, sin Nombre o sin DNI, cuando presiono Grabar, entonces el sistema no lo graba y muestra un mensaje que identifica el campo faltante.
- AC-45 (RF-61): Dado un presupuesto con Apellido, Nombre y DNI cargados y sin Domicilio, Email ni Nro. de Teléfono, cuando presiono Grabar, entonces el sistema lo graba sin reclamar esos tres campos.
- AC-46 (RF-40): Dada una línea con un artículo ya seleccionado, cuando modifico la cantidad y el porcentaje de descuento, entonces el sistema toma los nuevos valores y recalcula el precio con descuento y el precio final de la línea.
- AC-47 (RF-43): Dado un presupuesto en estado Final cuyas líneas no discriminan IVA, cuando emito la factura, entonces el sistema calcula el neto y el IVA a partir de los importes finales y los envía discriminados a ARCA.
- AC-48 (RF-46): Dada la alícuota de IVA configurada en 21, cuando cargo un artículo y cuando emito una factura, entonces el sistema aplica esa misma alícuota tanto en el cálculo del precio de venta (RF-21) como en el desglose de neto e IVA (RF-43).
- AC-49 (RF-63): Dada la pantalla de configuración abierta con el acceso de RNF-04, cuando modifico el tope de identificación del receptor y grabo, entonces el sistema aplica el nuevo tope en la siguiente facturación, sin intervención técnica.
- AC-50 (RF-64): Dado que recorro las pantallas del sistema, entonces ninguna muestra ni permite editar el certificado digital de la empresa ni el punto de venta habilitado para web services.
- AC-51 (RNF-01): Dada una base con 10.000 presupuestos, cuando ejecuto 20 búsquedas por apellido, entonces al menos 19 muestran la grilla de resultados en menos de 2 segundos contados desde la pulsación de Buscar.
- AC-52 (RNF-02): Dado el sistema en operación, cuando llega el cierre de la jornada comercial con la PC encendida, entonces existe en la ubicación de backup configurada una copia de la base con la fecha del día.
- AC-53 (RNF-05): Dado el sistema operando desde hace más de cuatro meses, entonces la ubicación de backup contiene las 7 copias diarias más recientes y 4 copias mensuales.
- AC-54 (RNF-06): Dada una copia de backup y el procedimiento documentado, cuando restauro la base en una PC preparada, entonces el sistema vuelve a estar operativo en menos de 4 horas.
- AC-55 (RNF-07): Dado que el backup del día anterior no se ejecutó, cuando ingreso al sistema, entonces se muestra un aviso que indica esa situación y la fecha del último backup disponible.
- AC-56 (RNF-03): Dado Chrome o Edge en su versión estable vigente o en la inmediata anterior, cuando abro el sistema, entonces todas las pantallas se muestran y operan correctamente.
- AC-57 (RNF-04): Dado que intento definir una contraseña de menos de 8 caracteres, entonces el sistema no la acepta e indica la longitud mínima exigida.
- AC-58 (RNF-08): Dados 5 intentos de ingreso fallidos consecutivos, entonces el sistema bloquea el acceso, y transcurridos 5 minutos vuelve a admitir el ingreso sin intervención de un administrador.
- AC-59 (RNF-09): Dada una sesión iniciada y 60 minutos sin actividad, entonces el sistema cierra la sesión y exige ingresar nuevamente la contraseña.
- AC-60 (RNF-10): Dado que ARCA no responde, cuando presiono Facturar, entonces el sistema abandona la espera a los 30 segundos, no reintenta por su cuenta y muestra el motivo en pantalla.
- AC-61 (RNF-11): Dado ARCA respondiendo con normalidad, cuando emito 20 facturas, entonces al menos 19 se completan en menos de 10 segundos contados desde la pulsación de Facturar hasta el mensaje de confirmación.
- AC-62 (RNF-12): Dado un catálogo de 10.000 artículos y una planilla Excel de 10.000 filas, cuando la proceso, entonces el sistema actualiza los precios de costo y recalcula los de venta en menos de 120 segundos.
- AC-63 (RNF-13): Dadas dos sesiones abiertas al mismo tiempo, cuando ambas graban un presupuesto simultáneamente, entonces los dos presupuestos quedan grabados y ninguna sesión recibe un error de bloqueo de la base.
- AC-64 (RF-65, RF-66): Dada una emisión que quedó sin respuesta de ARCA pero que en realidad fue autorizada, cuando la operadora reintenta, entonces el sistema detecta el comprobante ya autorizado, guarda su CAE y no emite uno nuevo.
- AC-65 (RF-65): Dada una emisión que quedó sin respuesta de ARCA y que no llegó a autorizarse, cuando la operadora reintenta, entonces el sistema verifica que no existe comprobante autorizado para esa operación y recién entonces envía el pedido.

## Fuera de Alcance
- Facturas A (a responsables inscriptos con CUIT) y comprobantes distintos de la factura a consumidor final: notas de crédito, notas de débito y anulaciones. Si una factura emitida debe corregirse, en esta versión se resuelve por fuera del sistema.
- Envío automático de emails o mensajes de WhatsApp desde el sistema. En esta versión, la operadora descarga el PDF y lo adjunta manualmente.
- Módulo de usuarios (ABM de usuarios y roles). Solo se contempla el acceso con contraseña de RNF-04.
- Módulo/ABM de clientes, incluido el historial de presupuestos y facturas por cliente. Los datos del cliente se cargan directamente en cada presupuesto y no se persisten como entidad independiente.

Los cuatro puntos anteriores quedan como pendientes para una versión futura.

## Riesgos y Dependencias
- Riesgo: La IA que genere el código puede alucinar o interpretar mal los requerimientos. Mitigación: criterios de aceptación explícitos y verificables.
- Riesgo: Fallas en la conexión a internet. Mitigación: la base de datos es local (SQLite), por lo que presupuestos y catálogo operan sin conectividad; la facturación sí requiere conexión y, en caso de corte, se reintenta cuando se restablece (RF-52).
- Riesgo: Indisponibilidad temporal de los web services de ARCA. Mitigación: el sistema informa el error, no deja comprobantes en estado intermedio y permite reintentar (RF-31, RF-51, RF-52, RF-53).
- Riesgo: Cambios normativos de ARCA (topes de identificación del receptor, formato del comprobante). Mitigación: los topes son parámetros configurables (RF-49) y la integración se aísla en un módulo propio para facilitar su actualización.
- Riesgo: Pérdida de datos por falla del disco de la PC del local (presupuestos, facturas, catálogo y datos personales de clientes). Mitigación: backup automático diario en ubicación externa (RNF-02).
- Riesgo: Acceso no autorizado a datos personales de clientes y a datos fiscales de la empresa (certificado digital, claves) almacenados localmente. Mitigación: acceso con contraseña (RNF-04), resguardo del certificado en ubicación protegida y fuera del alcance de la interfaz (RF-64) y, a futuro, módulo de usuarios con roles.
- Dependencia: Servidor web local o hosting donde correrá la aplicación.
- Dependencia: SQLite como motor de base de datos.
- Dependencia: Certificado digital de la empresa emitido por ARCA y asociado al servicio de facturación electrónica (WSFEv1).
- Dependencia: Punto de venta habilitado en ARCA para facturación por web services (distinto del punto de venta del facturador online manual).
- Dependencia: Entorno de homologación (testing) de ARCA para probar la integración antes de emitir comprobantes reales.
