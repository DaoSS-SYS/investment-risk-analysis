import { useState } from 'react'
import {
  Alert,
  Button,
  Card,
  Col,
  Row,
  Segmented,
  Space,
  Spin,
  Statistic,
  Table,
  Typography,
} from 'antd'
import { PieChartOutlined } from '@ant-design/icons'
import { useQuery } from '@tanstack/react-query'
import ReactECharts from 'echarts-for-react'
import type { ColumnsType } from 'antd/es/table'

import { api, describeError } from '../api/client'
import type { InstrumentWeight, NamedPortfolio } from '../api/types'
import { formatCount, formatDuration, formatNumber, formatPercent } from '../lib/format'

interface Props {
  portfolioId: number
  disabled?: boolean
}

/**
 * Оптимизация структуры портфеля по модели Марковица.
 *
 * Основное содержание — диаграмма множества сочетаний доходности и риска:
 * облако портфелей со случайной структурой, эффективная граница и три
 * отмеченные точки — текущая структура, портфель наименьшей дисперсии
 * и касательный портфель.
 */
export default function OptimizationPanel({ portfolioId, disabled }: Props) {
  const [maxWeight, setMaxWeight] = useState(0.4)
  const [requested, setRequested] = useState(false)

  const optimization = useQuery({
    queryKey: ['optimization', portfolioId, maxWeight],
    queryFn: () => api.portfolios.optimization(portfolioId, { maxWeight }),
    enabled: requested,
  })

  const data = optimization.data

  const chart = data
    ? {
        tooltip: {
          trigger: 'item',
          formatter: (params: { seriesName: string; value: number[] }) =>
            `${params.seriesName}<br/>риск ${formatPercent(params.value[0], 2)}<br/>` +
            `доходность ${formatPercent(params.value[1], 2)}`,
        },
        legend: {
          data: [
            'Случайные структуры',
            'Эффективная граница',
            'Текущая структура',
            'Наименьшая дисперсия',
            'Касательный портфель',
          ],
          top: 0,
          type: 'scroll',
        },
        grid: { left: 70, right: 24, top: 44, bottom: 52 },
        xAxis: {
          type: 'value',
          name: 'риск (волатильность, годовых)',
          nameLocation: 'middle',
          nameGap: 32,
          scale: true,
          axisLabel: { formatter: (value: number) => `${(value * 100).toFixed(0)} %` },
        },
        yAxis: {
          type: 'value',
          name: 'доходность, годовых',
          scale: true,
          axisLabel: { formatter: (value: number) => `${(value * 100).toFixed(0)} %` },
        },
        series: [
          {
            name: 'Случайные структуры',
            type: 'scatter',
            symbolSize: 4,
            itemStyle: { color: 'rgba(148,163,184,0.45)' },
            data: data.randomPortfolios.map((point) => [point.volatility, point.expectedReturn]),
          },
          {
            name: 'Эффективная граница',
            type: 'line',
            showSymbol: false,
            smooth: true,
            lineStyle: { width: 3, color: '#1d4ed8' },
            z: 5,
            data: data.efficientFrontier.map((point) => [point.volatility, point.expectedReturn]),
          },
          {
            name: 'Текущая структура',
            type: 'scatter',
            symbol: 'diamond',
            symbolSize: 18,
            itemStyle: { color: '#dc2626' },
            z: 10,
            data: data.current
              ? [[data.current.volatility, data.current.expectedReturn]]
              : [],
          },
          {
            name: 'Наименьшая дисперсия',
            type: 'scatter',
            symbol: 'triangle',
            symbolSize: 16,
            itemStyle: { color: '#15803d' },
            z: 10,
            data: [[data.minimumVariance.volatility, data.minimumVariance.expectedReturn]],
          },
          {
            name: 'Касательный портфель',
            type: 'scatter',
            symbol: 'circle',
            symbolSize: 16,
            itemStyle: { color: '#a855f7' },
            z: 10,
            data: [[data.maximumSharpe.volatility, data.maximumSharpe.expectedReturn]],
          },
        ],
      }
    : null

  const weightColumns: ColumnsType<InstrumentWeight> = [
    { title: 'Тикер', dataIndex: 'ticker', width: 80, render: (v: string) => <b>{v}</b> },
    {
      title: 'Текущая доля',
      dataIndex: 'currentWeight',
      align: 'right',
      width: 120,
      render: (value: number) => formatPercent(value, 1),
    },
    {
      title: 'Оптимальная доля',
      dataIndex: 'weight',
      align: 'right',
      width: 140,
      render: (value: number) => <Typography.Text strong>{formatPercent(value, 1)}</Typography.Text>,
    },
    {
      title: 'Изменение',
      dataIndex: 'change',
      align: 'right',
      width: 120,
      render: (value: number) => (
        <Typography.Text style={{ color: value >= 0 ? '#15803d' : '#b91c1c' }}>
          {formatPercent(value, 1, true)}
        </Typography.Text>
      ),
    },
  ]

  const summary = (portfolio: NamedPortfolio, color: string) => (
    <Card size="small" styles={{ body: { paddingBottom: 8 } }}>
      <Space direction="vertical" size={4} style={{ width: '100%' }}>
        <Typography.Text strong style={{ color }}>
          {portfolio.name}
        </Typography.Text>
        <Row gutter={8}>
          <Col span={8}>
            <Statistic
              title="Доходность"
              value={formatPercent(portfolio.expectedReturn, 2)}
              valueStyle={{ fontSize: 15 }}
            />
          </Col>
          <Col span={8}>
            <Statistic
              title="Риск"
              value={formatPercent(portfolio.volatility, 2)}
              valueStyle={{ fontSize: 15 }}
            />
          </Col>
          <Col span={8}>
            <Statistic
              title="Шарп"
              value={formatNumber(portfolio.sharpeRatio, 3)}
              valueStyle={{
                fontSize: 15,
                color: portfolio.sharpeRatio >= 0 ? '#15803d' : '#b91c1c',
              }}
            />
          </Col>
        </Row>
      </Space>
    </Card>
  )

  return (
    <Card
      title="Оптимизация структуры портфеля по модели Марковица"
      size="small"
      extra={
        <Space size={12} wrap>
          <Segmented
            size="small"
            value={maxWeight}
            onChange={(value) => setMaxWeight(Number(value))}
            options={[
              { label: 'доля ≤ 30 %', value: 0.3 },
              { label: 'доля ≤ 40 %', value: 0.4 },
              { label: 'без ограничения', value: 1 },
            ]}
          />
          <Button
            type="primary"
            size="small"
            icon={<PieChartOutlined />}
            loading={optimization.isFetching}
            disabled={disabled}
            onClick={() => {
              setRequested(true)
              void optimization.refetch()
            }}
          >
            Оптимизировать
          </Button>
        </Space>
      }
    >
      {!requested && (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Модель Марковица отыскивает структуру вложений, при которой для заданного
          уровня доходности риск минимален. Совокупность таких портфелей образует
          эффективную границу. Ограничение предельной доли одного инструмента
          препятствует получению решений, сосредоточенных в одной бумаге.
        </Typography.Paragraph>
      )}

      {optimization.isError && <Alert type="error" message={describeError(optimization.error)} />}

      {optimization.isFetching && (
        <Space direction="vertical" align="center" style={{ width: '100%', padding: 24 }}>
          <Spin />
        </Space>
      )}

      {data && !optimization.isFetching && (
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <Row gutter={[12, 12]}>
            {data.current && <Col xs={24} md={8}>{summary(data.current, '#dc2626')}</Col>}
            <Col xs={24} md={8}>{summary(data.minimumVariance, '#15803d')}</Col>
            <Col xs={24} md={8}>{summary(data.maximumSharpe, '#a855f7')}</Col>
          </Row>

          {chart && (
            <Card size="small" title="Множество сочетаний доходности и риска">
              <ReactECharts option={chart} style={{ height: 420 }} notMerge />
              <Typography.Paragraph
                type="secondary"
                style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}
              >
                Серые точки — {formatCount(data.randomPortfolios.length)} портфелей со
                случайной структурой, синяя линия — эффективная граница. Ни один случайный
                портфель не расположен выше границы: это и есть её определяющее свойство.
                Красный ромб — текущая структура портфеля.
              </Typography.Paragraph>
            </Card>
          )}

          <Row gutter={[16, 16]}>
            <Col xs={24} xl={12}>
              <Card size="small" title="Портфель наименьшей дисперсии">
                <Table
                  rowKey="instrumentId"
                  size="small"
                  pagination={false}
                  columns={weightColumns}
                  dataSource={data.minimumVariance.weights}
                />
              </Card>
            </Col>

            <Col xs={24} xl={12}>
              <Card size="small" title="Касательный портфель">
                <Table
                  rowKey="instrumentId"
                  size="small"
                  pagination={false}
                  columns={weightColumns}
                  dataSource={data.maximumSharpe.weights}
                />
              </Card>
            </Col>
          </Row>

          <Alert
            type="info"
            showIcon
            message="Вывод по результатам оптимизации"
            description={
              <Typography.Text style={{ fontSize: 12 }}>{data.conclusion}</Typography.Text>
            }
          />

          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Период выборки {data.from} — {data.to}, {formatCount(data.observationCount)} наблюдений,
            безрисковая ставка {formatPercent(data.riskFreeRateAnnual, 2)} годовых,
            расчёт {formatDuration(data.durationMs)}
          </Typography.Text>
        </Space>
      )}
    </Card>
  )
}
