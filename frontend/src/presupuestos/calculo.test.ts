import { describe, expect, it } from 'vitest'
import { centavosAImporte, precioConDescuentoEnCentavos, precioFinalEnCentavos } from './calculo'

describe('cálculo de líneas en centavos', () => {
  it.each([
    [1000, 0, 100000], // AC-26
    [1000, 10, 90000], // AC-35
    [6.67, 50, 334], // AC-29: 3,335 → 3,34
    [2.01, 50, 101], // 1,005 → 1,01 (en float daría 1,00)
    [4.35, 10, 392], // 3,915 → 3,92 (en float daría 3,91)
    [1815, 12.5, 158813], // 1588,125 → 1588,13
    [1815, 100, 0],
  ])('precio %d con %d %% de descuento da %d centavos', (precio, descuento, esperado) => {
    expect(precioConDescuentoEnCentavos(precio, descuento)).toBe(esperado)
  })

  it('AC-27 y AC-29: el precio final multiplica el valor ya redondeado', () => {
    expect(precioFinalEnCentavos(90000, 3)).toBe(270000)
    expect(precioFinalEnCentavos(precioConDescuentoEnCentavos(6.67, 50), 3)).toBe(1002)
  })

  it('AC-11: el total suma precios finales redondeados', () => {
    const total =
      precioFinalEnCentavos(precioConDescuentoEnCentavos(6.67, 50), 3) +
      precioFinalEnCentavos(precioConDescuentoEnCentavos(1000, 10), 3)
    expect(centavosAImporte(total)).toBe(2710.02)
  })
})
