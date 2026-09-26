import axios, { AxiosError } from 'axios'
import type {
  AuditRecordView,
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
  LoginResult,
  OptimizationReport,
  RoleInfo,
  SecuritySearchResult,
  StressTestReport,
  SystemInfo,
  UserView,
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
 * Источник маркера доступа. Задаётся состоянием проверки подлинности;
 * клиент обращается к нему при каждом запросе, а не хранит значение,
 * чтобы не зависеть от состояния компонентов.
 */
let authTokenProvider: () => string | null = () => null

/** Обработчик отказа в доступе. Вызывается при истечении срока маркера. */
let unauthorizedHandler: () => void = () => {}

/** Задаёт источник маркера доступа. */
export function setAuthTokenProvider(provider: () => string | null): void {
  authTokenProvider = provider
}

/** Задаёт обработчик отказа в доступе. */
export function setUnauthorizedHandler(handler: () => void): void {
  unauthorizedHandler = handler
}

// Маркер доступа добавляется в заголовок каждого запроса.
http.interceptors.request.use((config) => {
  const token = authTokenProvider()

  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

// Отказ по причине недействительного маркера означает истечение срока
// его действия: сеанс завершается, и пользователю предлагается войти вновь.
// Отказ по причине недостатка полномочий сеанс не прерывает.
http.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401) {
      unauthorizedHandler()
    }

    return Promise.reject(error)
  },
)

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

    if (error.response?.status === 403) {
      return 'Недостаточно полномочий для выполнения операции.'
    }

    if (error.response?.status === 401) {
      return 'Срок действия сеанса истёк. Выполните вход в систему повторно.'
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
  auth: {
    login: async (userName: string, password: string): Promise<LoginResult> =>
      (await http.post('/api/auth/login', { userName, password })).data,

    me: async (): Promise<UserView> => (await http.get('/api/auth/me')).data,

    users: async (): Promise<UserView[]> => (await http.get('/api/auth/users')).data,

    roles: async (): Promise<RoleInfo[]> => (await http.get('/api/auth/roles')).data,

    createUser: async (payload: {
      userName: string
      password: string
      fullName: string
      position?: string
      email?: string
      role: string
    }): Promise<UserView> => (await http.post('/api/auth/users', payload)).data,

    setUserActive: async (id: string, isActive: boolean): Promise<void> => {
      await http.put(`/api/auth/users/${id}/active`, null, { params: { isActive } })
    },

    audit: async (limit = 100): Promise<AuditRecordView[]> =>
      (await http.get('/api/audit', { params: { limit } })).data,
  },

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

    /**
     * Возвращает адрес выгрузки отчёта. Загрузка выполняется переходом
     * по адресу, а не запросом через клиент: сервер передаёт файл
     * с заголовком Content-Disposition, и браузер сохраняет его
     * с предложенным именем.
     */
    reportUrl: (
      id: number,
      format: 'pdf' | 'xlsx',
      options: { confidence?: number; horizon?: number; includeStressTest?: boolean } = {},
    ): string => {
      const params = new URLSearchParams()

      if (options.confidence !== undefined) params.set('confidence', String(options.confidence))
      if (options.horizon !== undefined) params.set('horizon', String(options.horizon))
      if (options.includeStressTest !== undefined) {
        params.set('includeStressTest', String(options.includeStressTest))
      }

      const base = import.meta.env.VITE_API_BASE_URL ?? ''

      return `${base}/api/portfolios/${id}/report/${format}?${params.toString()}`
    },

    optimization: async (
      id: number,
      options: { maxWeight?: number; from?: string; to?: string },
    ): Promise<OptimizationReport> =>
      (
        await http.get(`/api/portfolios/${id}/optimization`, {
          params: {
            maxWeight: options.maxWeight,
            from: formatDate(options.from),
            to: formatDate(options.to),
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
