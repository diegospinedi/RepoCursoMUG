// Formatos argentinos (ver .claude/skills/frontend-design).

const importe = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' })
const decimal = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 2 })

/** $ 1.815,00 */
export function formatearImporte(valor: number): string {
  return importe.format(valor)
}

/** 10,5 o 10.000.000 — para mostrar números en campos editables; leerDecimal los vuelve a leer. */
export function formatearDecimal(valor: number): string {
  return decimal.format(valor)
}

/**
 * Lee un número escrito por la operadora. La coma es el separador decimal y el
 * punto el de miles ("1.815,50"). Sin coma, un punto seguido de 1 o 2 dígitos
 * se toma como decimal ("10.5"), para no convertir 10.5 en 105.
 * Devuelve null si el texto no es un número.
 */
export function leerDecimal(texto: string): number | null {
  let limpio = texto.trim().replace(/^\$\s*/, '').replaceAll(' ', '')
  if (limpio === '') return null

  if (limpio.includes(',')) {
    limpio = limpio.replaceAll('.', '').replace(',', '.')
  } else if (!/^-?\d+\.\d{1,2}$/.test(limpio)) {
    limpio = limpio.replaceAll('.', '')
  }

  if (!/^-?\d+(\.\d+)?$/.test(limpio)) return null
  return Number(limpio)
}

/** Lee una cantidad entera positiva ("3"). Devuelve null si no lo es: "2,5", "0", "-1", "abc". */
export function leerEnteroPositivo(texto: string): number | null {
  const limpio = texto.trim()
  if (!/^\d+$/.test(limpio)) return null
  const valor = Number(limpio)
  return valor > 0 ? valor : null
}

/** 23.456.789 — el DNI se guarda sin puntos y se muestra con puntos de miles. */
export function formatearDni(dni: string): string {
  return /^\d+$/.test(dni) ? dni.replace(/\B(?=(\d{3})+(?!\d))/g, '.') : dni
}

/** dd/mm/aaaa, a partir de la fecha ISO que devuelve la API ("2026-10-05"). */
export function formatearFecha(fechaIso: string): string {
  const [anio, mes, dia] = fechaIso.split('-')
  return `${dia}/${mes}/${anio}`
}
