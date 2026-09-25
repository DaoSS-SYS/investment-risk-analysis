/**
 * Форматирование числовых величин для представления пользователю.
 *
 * Применяются правила русской локали: разделителем целой и дробной части
 * служит запятая, разделителем групп разрядов — неразрывный пробел.
 */

const money = new Intl.NumberFormat('ru-RU', {
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
})

const moneyPrecise = new Intl.NumberFormat('ru-RU', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

/** Денежная величина в рублях, без дробной части. */
export function formatMoney(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—'
  }

  return `${money.format(Math.round(value))} ₽`
}

/** Денежная величина с копейками. */
export function formatMoneyPrecise(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—'
  }

  return `${moneyPrecise.format(value)} ₽`
}

/** Доля, выраженная в процентах. */
export function formatPercent(
  value: number | null | undefined,
  fractionDigits = 2,
  withSign = false,
): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—'
  }

  const formatted = new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
    signDisplay: withSign ? 'exceptZero' : 'auto',
  }).format(value * 100)

  return `${formatted} %`
}

/** Число с заданным количеством знаков после запятой. */
export function formatNumber(
  value: number | null | undefined,
  fractionDigits = 4,
): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—'
  }

  return new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(value)
}

/** Целое число с разделителями разрядов. */
export function formatCount(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—'
  }

  return new Intl.NumberFormat('ru-RU').format(value)
}

/** Дата в виде ДД.ММ.ГГГГ. */
export function formatDate(value: string | null | undefined): string {
  if (!value) {
    return '—'
  }

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleDateString('ru-RU')
}

/** Дата и время. */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '—'
  }

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return value
  }

  return date.toLocaleString('ru-RU')
}

/** Длительность в миллисекундах, переведённая в удобные единицы. */
export function formatDuration(milliseconds: number | null | undefined): string {
  if (milliseconds === null || milliseconds === undefined) {
    return '—'
  }

  if (milliseconds < 1000) {
    return `${milliseconds} мс`
  }

  return `${(milliseconds / 1000).toLocaleString('ru-RU', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })} с`
}

/** Наименование типа инструмента на русском языке. */
export function describeSecurityType(type: string): string {
  const names: Record<string, string> = {
    Share: 'Акция',
    Bond: 'Облигация',
    Index: 'Индекс',
    Currency: 'Валюта',
    Etf: 'Биржевой фонд',
  }

  return names[type] ?? type
}

/** Наименование состояния расчёта на русском языке. */
export function describeCalculationStatus(status: string): string {
  const names: Record<string, string> = {
    Queued: 'В очереди',
    Running: 'Выполняется',
    Succeeded: 'Выполнен',
    Failed: 'Ошибка',
    Cancelled: 'Отменён',
  }

  return names[status] ?? status
}

/** Цвет индикатора состояния расчёта. */
export function calculationStatusColor(status: string): string {
  const colors: Record<string, string> = {
    Queued: 'default',
    Running: 'processing',
    Succeeded: 'success',
    Failed: 'error',
    Cancelled: 'warning',
  }

  return colors[status] ?? 'default'
}

/**
 * Формирует краткое наименование метода оценки риска для таблицы.
 *
 * Сервер возвращает развёрнутое описание расчёта. Разновидности метода
 * Монте-Карло различаются законом распределения, поэтому для них
 * наименование дополняется его названием.
 */
export function describeVarMethod(description: string): string {
  const head = description.split(',')[0].trim()

  const match = description.match(/закон распределения — ([^.]+)\./)

  if (!match) {
    return head
  }

  const distribution = match[1].trim()

  const shortNames: Record<string, string> = {
    'нормальное распределение': 'нормальное распределение',
    'историческая бутстрэп-выборка': 'бутстрэп-выборка',
  }

  const shortName =
    shortNames[distribution] ??
    (distribution.startsWith('распределение Стьюдента') ? 'распределение Стьюдента' : distribution)

  return `${head}: ${shortName}`
}
