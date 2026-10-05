import { describe, expect, it } from 'vitest'
import { formatearDecimal, formatearDni, formatearFecha, formatearImporte, leerDecimal, leerEnteroPositivo } from './formato'

describe('formatos argentinos', () => {
  it('formatea importes con $ y 2 decimales', () => {
    expect(formatearImporte(1815)).toMatch(/^\$\s1\.815,00$/)
  })

  it('formatea decimales con coma y punto de miles, y se pueden volver a leer', () => {
    expect(formatearDecimal(10.5)).toBe('10,5')
    expect(formatearDecimal(10000000)).toBe('10.000.000')
    for (const valor of [0.01, 10.5, 1500, 380000.5, 10000000]) {
      expect(leerDecimal(formatearDecimal(valor))).toBe(valor)
    }
  })

  it.each([
    ['21', 21],
    ['10,5', 10.5],
    ['10.5', 10.5],
    ['0,01', 0.01],
    ['1.815,50', 1815.5],
    ['$ 10.000.000', 10000000],
    ['1.500', 1500],
    ['-1', -1],
  ])('lee "%s" como %d', (texto, esperado) => {
    expect(leerDecimal(texto)).toBe(esperado)
  })

  it.each(['', 'abc', '1,2,3', '12a'])('no acepta "%s"', (texto) => {
    expect(leerDecimal(texto)).toBeNull()
  })

  it.each([
    ['3', 3],
    [' 12 ', 12],
    ['0', null],
    ['-1', null],
    ['2,5', null],
    ['2.5', null],
    ['', null],
  ])('lee la cantidad "%s" como %s', (texto, esperado) => {
    expect(leerEnteroPositivo(texto)).toBe(esperado)
  })

  it('formatea DNI y fechas', () => {
    expect(formatearDni('23456789')).toBe('23.456.789')
    expect(formatearDni('1234567')).toBe('1.234.567')
    expect(formatearFecha('2026-10-05')).toBe('05/10/2026')
  })
})
