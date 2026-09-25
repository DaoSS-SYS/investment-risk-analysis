import { useState } from 'react'
import {
  Alert,
  Card,
  Col,
  Descriptions,
  InputNumber,
  Row,
  Segmented,
  Space,
  Spin,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd'
import { useQuery } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import ReactECharts from 'echarts-for-react'

import { api, describeError } from '../api/client'
import type { VarEstimate } from '../api/types'
import {
  formatCount,
  formatDate,
  formatDuration,
  formatMoney,
  formatNumber,
  formatPercent,
} from '../lib/format'

/**
 * Анализ отдельного финансового инструмента: динамика цены, описательные
 * статистики, проверка гипотезы о нормальности распределения доходностей,
 * гистограмма распределения и сопоставление оценок риска.
 */
export default function InstrumentAnalysisPage() {
  const { id } = useParams<{ id: string }>()
  const instrumentId = Number(id)

  const [confidence, setConfidence] = useState(0.99)
  const [horizon, setHorizon] = useState(1)
  const [positionValue, setPositionValue] = useState(1_000_000)

  const instruments = useQuery({ queryKey: ['instruments'], queryFn: api.instruments.list })
  const instrument = instruments.data?.find((item) => item.id === instrumentId)

  // Эталонным портфелем для оценки параметров модели CAPM служит биржевой
  // индекс из справочника. Для самого индекса оценка не выполняется:
  // коэффициент бета относительно самого себя тождественно равен единице.
  const benchmark = instruments.data?.find(
    (item) => item.securityType === 'Index' && item.id !== instrumentId,
  )

  const series = useQuery({
    queryKey: ['series', instrumentId],
    queryFn: () => api.instruments.series(instrumentId),
    enabled: Number.isFinite(instrumentId),
  })

  const returns = useQuery({
    queryKey: ['returns', instrumentId],
    queryFn: () => api.analysis.returns(instrumentId),
    enabled: Number.isFinite(instrumentId),
  })

  const varAnalysis = useQuery({
    queryKey: ['var', instrumentId, confidence, horizon, positionValue],
    queryFn: () =>
      api.analysis.valueAtRisk(instrumentId, {
        confidence,
        horizon,
        value: positionValue,
      }),
    enabled: Number.isFinite(instrumentId),
  })

  const performance = useQuery({
    queryKey: ['performance', instrumentId, benchmark?.id],
    queryFn: () => api.analysis.performance(instrumentId, benchmark?.id),
    enabled: Number.isFinite(instrumentId) && instruments.isSuccess,
  })

  if (!Number.isFinite(instrumentId)) {
    return <Alert type="error" message="Инструмент не указан" />
  }

  // График динамики скорректированной цены.
  const priceChart = {
    tooltip: { trigger: 'axis' },
    grid: { left: 60, right: 24, top: 24, bottom: 56 },
    xAxis: {
      type: 'category',
      data: series.data?.points.map((point) => point.date) ?? [],
      axisLabel: { formatter: (value: string) => formatDate(value) },
    },
    yAxis: { type: 'value', scale: true, name: '₽' },
    dataZoom: [{ type: 'inside' }, { type: 'slider', height: 24, bottom: 12 }],
    series: [
      {
        name: 'Цена закрытия (скорректированная)',
        type: 'line',
        showSymbol: false,
        lineStyle: { width: 1.5, color: '#1d4ed8' },
        areaStyle: { color: 'rgba(29,78,216,0.08)' },
        data: series.data?.points.map((point) => point.adjustedClose) ?? [],
      },
    ],
  }

  // Гистограмма распределения доходностей с наложением теоретических
  // частот нормального распределения.
  const histogram = returns.data?.histogram ?? []

  const histogramChart = {
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    legend: { data: ['Наблюдаемая частота', 'Нормальное распределение'], top: 0 },
    grid: { left: 60, right: 24, top: 40, bottom: 48 },
    xAxis: {
      type: 'category',
      data: histogram.map((bin) => (bin.lowerBound * 100).toFixed(1)),
      name: 'доходность, %',
      nameLocation: 'middle',
      nameGap: 30,
      axisLabel: { interval: Math.max(0, Math.floor(histogram.length / 12)) },
    },
    yAxis: { type: 'value', name: 'доля' },
    series: [
      {
        name: 'Наблюдаемая частота',
        type: 'bar',
        itemStyle: { color: '#1d4ed8' },
        data: histogram.map((bin) => bin.observedFrequency),
      },
      {
        name: 'Нормальное распределение',
        type: 'line',
        smooth: true,
        showSymbol: false,
        lineStyle: { width: 2, color: '#dc2626' },
        data: histogram.map((bin) => bin.normalFrequency),
      },
    ],
  }

  const varColumns = [
    { title: 'Метод', dataIndex: 'name', key: 'name' },
    {
      title: 'VaR, %',
      key: 'varRelative',
      align: 'right' as const,
      render: (_: unknown, record: VarEstimate) =>
        formatPercent(record.result.valueAtRiskRelative, 3),
    },
    {
      title: 'VaR, ₽',
      key: 'varAbsolute',
      align: 'right' as const,
      render: (_: unknown, record: VarEstimate) => formatMoney(record.result.valueAtRiskAbsolute),
    },
    {
      title: 'CVaR, %',
      key: 'cvarRelative',
      align: 'right' as const,
      render: (_: unknown, record: VarEstimate) =>
        formatPercent(record.result.expectedShortfallRelative, 3),
    },
    {
      title: 'CVaR, ₽',
      key: 'cvarAbsolute',
      align: 'right' as const,
      render: (_: unknown, record: VarEstimate) => (
        <Typography.Text strong>
          {formatMoney(record.result.expectedShortfallAbsolute)}
        </Typography.Text>
      ),
    },
    {
      title: 'Время',
      dataIndex: 'durationMs',
      key: 'durationMs',
      align: 'right' as const,
      width: 90,
      render: (value: number) => formatDuration(value),
    },
  ]

  const statistics = returns.data?.statistics
  const normality = returns.data?.normality
  const metrics = performance.data?.performance
  const capm = performance.data?.capm

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space align="baseline" wrap>
        <Typography.Title level={4} style={{ margin: 0 }}>
          {instrument?.ticker ?? '—'}
        </Typography.Title>
        <Typography.Text type="secondary">{instrument?.shortName}</Typography.Text>
        {returns.data && (
          <Tag>
            {formatDate(returns.data.from)} — {formatDate(returns.data.to)},{' '}
            {formatCount(returns.data.returnCount)} наблюдений
          </Tag>
        )}
      </Space>

      {returns.isError && <Alert type="error" message={describeError(returns.error)} />}

      <Card title="Динамика цены" size="small" loading={series.isLoading}>
        <ReactECharts option={priceChart} style={{ height: 320 }} notMerge />
      </Card>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card size="small">
            <Statistic
              title="Доходность, годовых"
              value={formatPercent(metrics?.annualizedReturn, 2, true)}
              loading={performance.isLoading}
              valueStyle={{
                fontSize: 20,
                color: (metrics?.annualizedReturn ?? 0) >= 0 ? '#15803d' : '#b91c1c',
              }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card size="small">
            <Statistic
              title="Волатильность, годовых"
              value={formatPercent(statistics?.annualizedVolatility)}
              loading={returns.isLoading}
              valueStyle={{ fontSize: 20 }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card size="small">
            <Statistic
              title="Коэффициент Шарпа"
              value={formatNumber(metrics?.sharpeRatio, 2)}
              loading={performance.isLoading}
              valueStyle={{
                fontSize: 20,
                color: (metrics?.sharpeRatio ?? 0) >= 0 ? '#15803d' : '#b91c1c',
              }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card size="small">
            <Statistic
              title="Максимальная просадка"
              value={formatPercent(performance.data?.drawdown.maxDrawdown)}
              loading={performance.isLoading}
              valueStyle={{ fontSize: 20, color: '#b91c1c' }}
            />
          </Card>
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={14}>
          <Card title="Распределение доходностей" size="small" loading={returns.isLoading}>
            <ReactECharts option={histogramChart} style={{ height: 300 }} notMerge />
          </Card>
        </Col>

        <Col xs={24} xl={10}>
          <Card title="Проверка гипотезы о нормальности" size="small" loading={returns.isLoading}>
            {normality && (
              <Space direction="vertical" size={12} style={{ width: '100%' }}>
                <Alert
                  type={normality.isNormalityRejected ? 'warning' : 'success'}
                  showIcon
                  message={
                    normality.isNormalityRejected
                      ? 'Гипотеза о нормальности отвергается'
                      : 'Гипотеза о нормальности не отвергается'
                  }
                  description={
                    <Typography.Text style={{ fontSize: 12 }}>
                      {normality.conclusion}
                    </Typography.Text>
                  }
                />

                <Descriptions size="small" column={1} bordered>
                  <Descriptions.Item label="Критерий Жарка — Бера">
                    {formatNumber(normality.statistic, 1)}
                  </Descriptions.Item>
                  <Descriptions.Item label="Критическое значение">
                    {formatNumber(normality.criticalValue, 2)}
                  </Descriptions.Item>
                  <Descriptions.Item label="Коэффициент асимметрии">
                    {formatNumber(statistics?.skewness, 3)}
                  </Descriptions.Item>
                  <Descriptions.Item label="Коэффициент эксцесса">
                    {formatNumber(statistics?.excessKurtosis, 2)}
                  </Descriptions.Item>
                </Descriptions>
              </Space>
            )}
          </Card>
        </Col>
      </Row>

      <Card
        title="Стоимостная мера риска"
        size="small"
        extra={
          <Space size={12} wrap>
            <Segmented
              size="small"
              value={confidence}
              onChange={(value) => setConfidence(Number(value))}
              options={[
                { label: '95 %', value: 0.95 },
                { label: '99 %', value: 0.99 },
              ]}
            />
            <Segmented
              size="small"
              value={horizon}
              onChange={(value) => setHorizon(Number(value))}
              options={[
                { label: '1 день', value: 1 },
                { label: '10 дней', value: 10 },
              ]}
            />
            <InputNumber
              size="small"
              style={{ width: 150 }}
              value={positionValue}
              min={1000}
              step={100_000}
              formatter={(value) => `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ' ')}
              parser={(value) => Number((value ?? '').replace(/\s/g, ''))}
              onChange={(value) => setPositionValue(value ?? 1_000_000)}
              addonAfter="₽"
            />
          </Space>
        }
      >
        {varAnalysis.isLoading ? (
          <Spin />
        ) : (
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            <Table
              rowKey="name"
              size="small"
              pagination={false}
              columns={varColumns}
              dataSource={varAnalysis.data?.estimates ?? []}
            />

            {varAnalysis.data && (
              <Alert
                type="info"
                showIcon
                message="Сопоставление методов"
                description={
                  <Typography.Text style={{ fontSize: 12 }}>
                    {varAnalysis.data.conclusion}
                  </Typography.Text>
                }
              />
            )}
          </Space>
        )}
      </Card>

      {capm && (
        <Card
          title="Модель оценки капитальных активов (CAPM)"
          size="small"
          extra={
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              эталонный портфель: {performance.data?.benchmarkTicker}
            </Typography.Text>
          }
        >
          <Row gutter={[16, 16]}>
            <Col xs={12} md={6}>
              <Statistic title="Коэффициент бета" value={formatNumber(capm.beta, 3)} />
            </Col>
            <Col xs={12} md={6}>
              <Statistic
                title="Альфа Йенсена, годовых"
                value={formatPercent(capm.alphaAnnualized, 2, true)}
                suffix={
                  <Tag color={capm.alphaIsSignificant ? 'success' : 'default'}>
                    {capm.alphaIsSignificant ? 'значима' : 'не значима'}
                  </Tag>
                }
              />
            </Col>
            <Col xs={12} md={6}>
              <Statistic
                title="Систематический риск"
                value={formatPercent(capm.systematicRiskShare, 1)}
              />
            </Col>
            <Col xs={12} md={6}>
              <Statistic title="Коэффициент детерминации" value={formatNumber(capm.rSquared, 3)} />
            </Col>
          </Row>

          <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0, fontSize: 12 }}>
            {capm.conclusion}
          </Typography.Paragraph>
        </Card>
      )}
    </Space>
  )
}
