import axios, { AxiosError } from 'axios'
import type {
  BacktestReport,
  Calculation,
  CorporateAction,
  CorrelationAnalysis,
  ImportResult,
  Instrument,
  PerformanceAnalysis,
  Portfolio,
  PortfolioRiskReport,
  PriceSeries,
  ReturnAnalysis,
  RiskCalculationParameters,
  SecuritySearchResult,
  StressTestReport,
  SystemInfo,
  VarComparison,
} from './types'

/**
 * Клиент программного интерфейса системы.
 *
 * Базовый адрес задаётся переменной окружения VITE_API_BASE_URL. При её
 * отсутствии используется относительный путь: в среде разработки запросы
 * перенаправляет прокси сборщика Vite, при размещении клиентского
 * приложения и сервера на одном узле — сам сервер.
 */
const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '',
  timeout: 120_000,
  headers: { 'Content-Type': 'application/json' },
})

/**
 * Приводит ошибку обращения к интерфейсу к сообщению, пригодному
 * для показа пользователю. Сервер возвращает пояснение в поле error.
 */
export function describeError(error: unknown): string {
  if (error instanceof AxiosError) {
    const payload = error.response?.data as { error?: string; title?: string } | undefined

    if (payload?.error) {
      return payload.error
    }

    if (payload?.title) {
      return payload.title
    }

    if (error.code === 'ECONNABORTED') {
      return 'Превышено время ожидания ответа сервера.'
    }

    if (!error.response) {
      return 'Сервер недоступен. Проверьте, запущена ли серверная часть системы.'
    }

    return `Сервер вернул код ${error.response.status}.`
  }

  return error instanceof Error ? error.message : 'Неизвестная ошибка.'
}

/** Преобразует дату в формат, ожидаемый интерфейсом. */
function formatDate(value?: string | null): string | undefined {
  return value ?? undefined
}

export const api = {
  system: {
    info: async (): Promise<SystemInfo> => (await http.get('/api/system/info')).data,
  },

  instruments: {
    list: async (): Promise<Instrument[]> => (await http.get('/api/instruments')).data,

    search: async (query: string): Promise<SecuritySearchResult[]> =>
      (await http.get('/api/instruments/search', { params: { query } })).data,

    add: async (ticker: string, board?: string, sector?: string): Promise<Instrument> =>
      (await http.post('/api/instruments', { ticker, board, sector })).data,

    remove: async (id: number): Promise<void> => {
      await http.delete(`/api/instruments/${id}`)
    },

    importQuotes: async (id: number, from?: string, to?: string): Promise<ImportResult> =>
      (
        await http.post(`/api/instruments/${id}/quotes/import`, null, {
          params: { from: formatDate(from), to: formatDate(to) },
        })
      ).data,

    series: async (id: number, from?: string, to?: string): Promise<PriceSeries> =>
      (
        await http.get(`/api/instruments/${id}/series`, {
          params: { from: formatDate(from), to: formatDate(to) },
        })
      ).data,
  },

  analysis: {
    returns: async (id: number, from?: string, to?: string): Promise<ReturnAnalysis> =>
      (
        await http.get(`/api/analysis/instruments/${id}/returns`, {
          params: { from: formatDate(from), to: formatDate(to) },
        })
      ).data,

    valueAtRisk: async (
      id: number,
      options: { from?: string; to?: string; confidence?: number; horizon?: number; value?: number },
    ): Promise<VarComparison> =>
      (
        await http.get(`/api/analysis/instruments/${id}/var`, {
          params: {
            from: formatDate(options.from),
            to: formatDate(options.to),
            confidence: options.confidence,
            horizon: options.horizon,
            value: options.value,
          },
        })
      ).data,

    performance: async (
      id: number,
      benchmarkId?: number,
      from?: string,
      to?: string,
    ): Promise<PerformanceAnalysis> =>
      (
        await http.get(`/api/analysis/instruments/${id}/performance`, {
          params: { benchmarkId, from: formatDate(from), to: formatDate(to) },
        })
      ).data,

    backtest: async (
      id: number,
      options: { from?: string; to?: string; confidence?: number; window?: number },
    ): Promise<BacktestReport> =>
      (
        await http.get(`/api/analysis/instruments/${id}/backtest`, {
          params: {
            from: formatDate(options.from),
            to: formatDate(options.to),
            confidence: options.confidence,
            window: options.window,
          },
          // Бэктестирование выполняется по каждому дню периода проверки
          // тремя методами и занимает больше времени, чем разовый расчёт.
          timeout: 300_000,
        })
      ).data,

    correlation: async (ids: number[], from?: string, to?: string): Promise<CorrelationAnalysis> =>
      (
        await http.get('/api/analysis/correlation', {
          params: { ids: ids.join(','), from: formatDate(from), to: formatDate(to) },
        })
      ).data,
  },

  portfolios: {
    list: async (): Promise<Portfolio[]> => (await http.get('/api/portfolios')).data,

    get: async (id: number): Promise<Portfolio> => (await http.get(`/api/portfolios/${id}`)).data,

    create: async (payload: {
      name: string
      description?: string
      baseCurrency?: string
      benchmarkInstrumentId?: number | null
    }): Promise<Portfolio> => (await http.post('/api/portfolios', payload)).data,

    remove: async (id: number): Promise<void> => {
      await http.delete(`/api/portfolios/${id}`)
    },

    addPosition: async (
      id: number,
      payload: {
        instrumentId: number
        quantity: number
        purchasePrice: number
        purchaseDate: string
        note?: string
      },
    ): Promise<Portfolio> => (await http.post(`/api/portfolios/${id}/positions`, payload)).data,

    removePosition: async (id: number, positionId: number): Promise<Portfolio> =>
      (await http.delete(`/api/portfolios/${id}/positions/${positionId}`)).data,

    risk: async (
      id: number,
      options: {
        confidence?: number
        horizon?: number
        from?: string
        to?: string
        scenarios?: number
      },
    ): Promise<PortfolioRiskReport> =>
      (
        await http.get(`/api/portfolios/${id}/risk`, {
          params: {
            confidence: options.confidence,
            horizon: options.horizon,
            from: formatDate(options.from),
            to: formatDate(options.to),
            scenarios: options.scenarios,
          },
        })
      ).data,

    stressTest: async (
      id: number,
      options: { confidence?: number; horizon?: number; from?: string; to?: string },
    ): Promise<StressTestReport> =>
      (
        await http.get(`/api/portfolios/${id}/stress-test`, {
          params: {
            confidence: options.confidence,
            horizon: options.horizon,
            from: formatDate(options.from),
            to: formatDate(options.to),
          },
        })
      ).data,

    enqueueCalculation: async (
      id: number,
      parameters: RiskCalculationParameters,
    ): Promise<Calculation> =>
      (await http.post(`/api/portfolios/${id}/calculations`, parameters)).data,

    calculations: async (id: number): Promise<Calculation[]> =>
      (await http.get(`/api/portfolios/${id}/calculations`)).data,
  },

  calculations: {
    get: async (id: string): Promise<Calculation> => (await http.get(`/api/calculations/${id}`)).data,
  },

  corporateActions: {
    list: async (instrumentId?: number): Promise<CorporateAction[]> =>
      (await http.get('/api/corporate-actions', { params: { instrumentId } })).data,

    detectAll: async (): Promise<unknown> => (await http.post('/api/corporate-actions/detect-all')).data,
  },

  importData: {
    keyRate: async (from?: string, to?: string): Promise<ImportResult> =>
      (
        await http.post('/api/import/key-rate', null, {
          params: { from: formatDate(from), to: formatDate(to) },
        })
      ).data,

    allQuotes: async (): Promise<ImportResult[]> => (await http.post('/api/import/quotes/all')).data,
  },
}
