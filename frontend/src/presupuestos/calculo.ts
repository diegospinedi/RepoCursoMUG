// Cálculos de línea para mostrar en vivo (RF-13, RF-14, RF-15, RF-19, RF-44).
// El servidor recalcula y guarda; esto tiene que dar exactamente lo mismo.
//
// Se trabaja en enteros (centavos y centésimos de porcentaje) porque en punto
// flotante 2,01 × 0,5 = 1,00499… y Math.round daría 1,00 en lugar de 1,01.

/** Convierte un importe con hasta 2 decimales a centavos enteros. */
function aCentavos(importe: number): number {
  return Math.round(importe * 100)
}

/** División entera redondeando la mitad hacia arriba (valores no negativos). */
function dividirRedondeando(numerador: number, denominador: number): number {
  return Math.floor((2 * numerador + denominador) / (2 * denominador))
}

/** RF-13 + RF-44: precio unitario × (1 − descuento / 100), a 2 decimales, mitad hacia arriba. En centavos. */
export function precioConDescuentoEnCentavos(precioUnitario: number, porcentajeDescuento: number): number {
  const descuento = Math.round(porcentajeDescuento * 100) // centésimos de punto: 12,5 % → 1250
  return dividirRedondeando(aCentavos(precioUnitario) * (10000 - descuento), 10000)
}

/** RF-14: se multiplica el precio con descuento ya redondeado. En centavos. */
export function precioFinalEnCentavos(precioConDescuento: number, cantidad: number): number {
  return precioConDescuento * cantidad
}

export function centavosAImporte(centavos: number): number {
  return centavos / 100
}
