/**
 * Типы данных программного интерфейса.
 *
 * Соответствуют объектам передачи данных серверной части. Сервер
 * сериализует свойства в нижнем верблюжьем регистре, а перечисления —
 * строковыми значениями, поэтому типы описаны в том же виде.
 */

export type SecurityType = 'Share' | 'Bond' | 'Index' | 'Currency' | 'Etf'

export type CalculationStatus =
  | 'Queued'
  | 'Running'
  | 'Succeeded'
  | 'Failed'
  | 'Cancelled'

export type CorporateActionType = 'Split' | 'ReverseSplit' | 'MarketEvent'

export interface Instrument {
  id: number
  ticker: string
  shortName: string
  fullName: string | null
  securityType: SecurityType
  engine: string
  market: string
  board: string
  currency: string
  isin: string | null
  sector: string | null
  isActive: boolean
  historyFrom: string | null
  historyTo: string | null
  quoteCount: number
}

export interface SecuritySearchResult {
  ticker: string
  shortName: string
  fullName: string | null
  securityType: SecurityType
  engine: string
  market: string
  board: string
  isin: string | null
  alreadyAdded: boolean
}

export interface ImportResult {
  instrumentId: number
  ticker: string
  dateFrom: string
  dateTo: string
  rowsReceived: number
  rowsInserted: number
  rowsUpdated: number
  status: 'Success' | 'PartialSuccess' | 'Failed'
  message: string | null
  durationMs: number
}

export interface PricePoint {
  date: string
  close: number
  adjustedClose: number
}

export interface PriceSeries {
  instrumentId: number
  ticker: string
  points: PricePoint[]
  appliedAdjustments: number
}

export interface DescriptiveStatistics {
  count: number
  mean: number
  variance: number
  standardDeviation: number
  skewness: number
  excessKurtosis: number
  skewnessAdjusted: number
  excessKurtosisAdjusted: number
  minimum: number
  maximum: number
  median: number
  annualizedMean: number
  annualizedVolatility: number
}

export interface NormalityTest {
  statistic: number
  pValue: number
  criticalValue: number
  significanceLevel: number
  isNormalityRejected: boolean
  skewness: number
  excessKurtosis: number
  conclusion: string
}

export interface HistogramBin {
  lowerBound: number
  upperBound: number
  count: number
  observedFrequency: number
  normalFrequency: number
}

export interface ReturnAnalysis {
  instrumentId: number
  ticker: string
  shortName: string
  from: string
  to: string
  returnType: string
  frequency: string
  priceCount: number
  returnCount: number
  appliedAdjustments: number
  statistics: DescriptiveStatistics
  normality: NormalityTest
  histogram: HistogramBin[]
}

export interface VarResult {
  method: 'Parametric' | 'Historical' | 'MonteCarlo'
  confidenceLevel: number
  horizonDays: number
  valueAtRiskRelative: number
  expectedShortfallRelative: number
  valueAtRiskAbsolute: number
  expectedShortfallAbsolute: number
  portfolioValue: number
  observationCount: number
  mean: number
  standardDeviation: number
  description: string
}

export interface VarEstimate {
  name: string
  result: VarResult
  durationMs: number
}

export interface VarComparison {
  instrumentId: number
  ticker: string
  from: string
  to: string
  returnCount: number
  confidenceLevel: number
  horizonDays: number
  portfolioValue: number
  statistics: DescriptiveStatistics
  normality: NormalityTest
  estimates: VarEstimate[]
  conclusion: string
  durationMs: number
}

export interface DrawdownResult {
  maxDrawdown: number
  peakDate: string
  troughDate: string
  recoveryDate: string | null
  declineTradingDays: number
  recoveryTradingDays: number
  currentDrawdown: number
}

export interface PerformanceMetrics {
  count: number
  riskFreeRateAnnual: number
  annualizedLogReturn: number
  annualizedReturn: number
  annualizedVolatility: number
  annualizedExcessReturn: number
  sharpeRatio: number
  sortinoRatio: number
  downsideDeviationAnnualized: number
  calmarRatio: number
  maxDrawdown: number
  conclusion: string
}

export interface CapmResult {
  count: number
  beta: number
  betaStandardError: number
  betaTStatistic: number
  alphaPerPeriod: number
  alphaAnnualized: number
  alphaStandardError: number
  alphaTStatistic: number
  alphaIsSignificant: boolean
  rSquared: number
  correlation: number
  systematicRiskShare: number
  specificRiskShare: number
  specificVolatilityAnnualized: number
  treynorRatio: number
  informationRatio: number
  trackingErrorAnnualized: number
  conclusion: string
}

export interface PerformanceAnalysis {
  instrumentId: number
  ticker: string
  benchmarkTicker: string | null
  from: string
  to: string
  returnCount: number
  riskFreeRate: {
    annualRate: number
    source: string
    observationCount: number
    minimum: number
    maximum: number
  }
  statistics: DescriptiveStatistics
  performance: PerformanceMetrics
  drawdown: DrawdownResult
  capm: CapmResult | null
  durationMs: number
}

export interface CorrelationAnalysis {
  tickers: string[]
  from: string
  to: string
  observationCount: number
  correlation: number[][]
  annualizedVolatility: number[]
  equalWeightedPortfolioVolatility: number
  weightedAverageVolatility: number
  diversificationEffect: number
  averageCorrelation: number
  conclusion: string
}

export interface Position {
  id: number
  instrumentId: number
  ticker: string
  shortName: string
  quantity: number
  purchasePrice: number
  purchaseDate: string
  lastPrice: number | null
  lastPriceDate: string | null
  purchaseValue: number
  currentValue: number
  profitLoss: number
  profitLossPercent: number
  weight: number
}

export interface Portfolio {
  id: number
  name: string
  description: string | null
  baseCurrency: string
  benchmarkInstrumentId: number | null
  benchmarkTicker: string | null
  purchaseValue: number
  currentValue: number
  profitLoss: number
  profitLossPercent: number
  positions: Position[]
  createdAt: string
  updatedAt: string
}

export interface PositionRiskContribution {
  instrumentId: number
  ticker: string
  weight: number
  marginalVar: number
  componentVar: number
  contributionShare: number
  standaloneVar: number
  diversificationBenefit: number
}

export interface PortfolioRiskReport {
  portfolioId: number
  portfolioName: string
  from: string
  to: string
  observationCount: number
  portfolioValue: number
  confidenceLevel: number
  horizonDays: number
  portfolioVolatilityAnnualized: number
  weightedAverageVolatility: number
  diversificationEffect: number
  estimates: VarResult[]
  contributions: PositionRiskContribution[]
  sumOfStandaloneVar: number
  conclusion: string
  durationMs: number
}

export interface RiskCalculationParameters {
  confidenceLevel: number
  horizonDays: number
  from?: string | null
  to?: string | null
  scenarioCount: number
  portfolioValueOverride?: number | null
}

export interface Calculation {
  id: string
  portfolioId: number
  calculationType: string
  status: CalculationStatus
  parameters: RiskCalculationParameters | null
  createdAt: string
  startedAt: string | null
  finishedAt: string | null
  durationMs: number | null
  errorMessage: string | null
  result: PortfolioRiskReport | null
}

export interface CorporateAction {
  id: number
  instrumentId: number
  ticker: string
  actionDate: string
  actionType: CorporateActionType
  ratio: number
  adjustmentFactor: number
  observedRatio: number
  observedLogReturn: number
  source: string
  isConfirmed: boolean
  isApplied: boolean
  comment: string | null
}

export interface SystemInfo {
  application: string
  version: string | null
  machineName: string
  databaseAvailable: boolean
  appliedMigrations: string[]
  instrumentCount: number
  quoteCount: number
  portfolioCount: number
}

// ---------------------------------------------------------------------------
// Бэктестирование моделей оценки риска
// ---------------------------------------------------------------------------

export type BaselZone = 'Green' | 'Yellow' | 'Red'

export interface BacktestPoint {
  date: string
  actualReturn: number
  varEstimate: number
  isViolation: boolean
}

export interface KupiecTestResult {
  statistic: number
  pValue: number
  criticalValue: number
  isRejected: boolean
  conclusion: string
}

export interface ChristoffersenTestResult {
  independenceStatistic: number
  independencePValue: number
  independenceRejected: boolean
  conditionalCoverageStatistic: number
  conditionalCoveragePValue: number
  conditionalCoverageRejected: boolean
  n00: number
  n01: number
  n10: number
  n11: number
  conclusion: string
}

export interface BacktestResult {
  method: 'Parametric' | 'Historical' | 'MonteCarlo'
  confidenceLevel: number
  windowSize: number
  observations: number
  violations: number
  violationRate: number
  expectedViolationRate: number
  expectedViolations: number
  kupiec: KupiecTestResult
  christoffersen: ChristoffersenTestResult
  zone: BaselZone
  capitalMultiplierAddOn: number
  averageVar: number
  averageViolationSize: number
  series: BacktestPoint[]
  conclusion: string
}

export interface BacktestReport {
  instrumentId: number
  ticker: string
  from: string
  to: string
  confidenceLevel: number
  windowSize: number
  totalReturns: number
  results: BacktestResult[]
  conclusion: string
  durationMs: number
}

// ---------------------------------------------------------------------------
// Стресс-тестирование
// ---------------------------------------------------------------------------

export type StressScenarioKind = 'Historical' | 'Hypothetical'

export interface ScenarioPositionImpact {
  instrumentId: number
  ticker: string
  weight: number
  instrumentReturn: number
  contribution: number
  lossAmount: number
}

export interface ScenarioView {
  name: string
  kind: StressScenarioKind
  from: string | null
  to: string | null
  tradingDays: number
  portfolioReturn: number
  lossAmount: number
  valueAfter: number
  lossToVarRatio: number
  impacts: ScenarioPositionImpact[]
  description: string
}

export interface StressTestReport {
  portfolioId: number
  portfolioName: string
  portfolioValue: number
  from: string
  to: string
  confidenceLevel: number
  horizonDays: number
  valueAtRisk: number
  expectedShortfall: number
  scenarios: ScenarioView[]
  conclusion: string
  durationMs: number
}
