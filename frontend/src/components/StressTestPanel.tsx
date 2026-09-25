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
  Tag,
  Typography,
} from 'antd'
import { WarningOutlined } from '@ant-design/icons'
import { useQuery } from '@tanstack/react-query'
import ReactECharts from 'echarts-for-react'
import type { ColumnsType } from 'antd/es/table'

import { api, describeError } from '../api/client'
import type { ScenarioView } from '../api/types'
import { formatDate, formatDuration, formatMoney, formatNumber, formatPercent } from '../lib/format'

interface Props {
  portfolioId: number
  disabled?: boolean
}

/**
 * Стресс-тестирование портфеля: применение исторических и гипотетических
 * сценариев и сопоставление полученных потерь со стоимостной мерой риска.
 */
export default function StressTestPanel({ portfolioId, disabled }: Props) {
  const [confidence, setConfidence] = useState(0.99)
  const [horizon, setHorizon] = useState(10)
  const [requested, setRequested] = useState(false)

  const stress = useQuery({
    queryKey: ['stress-test', portfolioId, confidence, horizon],
    queryFn: () => api.portfolios.stressTest(portfolioId, { confidence, horizon }),
    enabled: requested,
  })

  const columns: ColumnsType<ScenarioView> = [
    {
      title: 'Сценарий',
      dataIndex: 'name',
      render: (name: string, record) => (
        <Space direction="vertical" size={0}>
          <Typography.Text strong>{name}</Typography.Text>
          <Tag color={record.kind === 'Historical' ? 'blue' : 'purple'}>
            {record.kind === 'Historical' ? 'исторический' : 'гипотетический'}
          </Tag>
        </Space>
      ),
    },
    {
      title: 'Период',
      key: 'period',
      width: 190,
      render: (_, record) =>
        record.from ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {formatDate(record.from)} — {formatDate(record.to)}
            <br />
            {record.tradingDays} торг. дн.
          </Typography.Text>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
    {
      title: 'Потери',
      dataIndex: 'lossAmount',
      align: 'right',
      width: 150,
      render: (value: number) => (
        <Typography.Text strong style={{ color: '#b91c1c' }}>
          {formatMoney(value)}
        </Typography.Text>
      ),
    },
    {
      title: '% стоимости',
      dataIndex: 'portfolioReturn',
      align: 'right',
      width: 120,
      render: (value: number) => formatPercent(value, 1, true),
    },
    {
      title: 'Стоимость после',
      dataIndex: 'valueAfter',
      align: 'right',
      width: 150,
      render: (value: number) => formatMoney(value),
    },
    {
      title: 'Превышение VaR',
      dataIndex: 'lossToVarRatio',
      align: 'right',
      width: 140,
      render: (value: number) => (
        <Tag color={value >= 3 ? 'error' : value >= 2 ? 'warning' : 'default'}>
          {formatNumber(value, 2)} ×
        </Tag>
      ),
    },
  ]

  const scenarios = stress.data?.scenarios ?? []

  // Сопоставление потерь по сценариям со стоимостной мерой риска
  // и ожидаемыми потерями.
  const chart = stress.data
    ? {
        tooltip: {
          trigger: 'axis',
          axisPointer: { type: 'shadow' },
          valueFormatter: (value: number) => formatMoney(value),
        },
        grid: { left: 170, right: 40, top: 16, bottom: 32 },
        xAxis: {
          type: 'value',
          axisLabel: {
            formatter: (value: number) => `${(value / 1000).toFixed(0)} тыс.`,
          },
        },
        yAxis: {
          type: 'category',
          data: [...scenarios].reverse().map((scenario) =>
            scenario.name.length > 26 ? `${scenario.name.slice(0, 24)}…` : scenario.name,
          ),
          axisLabel: { fontSize: 11 },
        },
        series: [
          {
            name: 'Потери в сценарии',
            type: 'bar',
            data: [...scenarios].reverse().map((scenario) => ({
              value: -scenario.lossAmount,
              itemStyle: {
                color: scenario.kind === 'Historical' ? '#1d4ed8' : '#a855f7',
              },
            })),
            markLine: {
              symbol: 'none',
              lineStyle: { color: '#dc2626', width: 2, type: 'dashed' },
              label: { formatter: 'VaR', position: 'insideEndTop', color: '#dc2626' },
              data: [{ xAxis: stress.data.valueAtRisk }],
            },
          },
        ],
      }
    : null

  return (
    <Card
      title="Стресс-тестирование портфеля"
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
              { label: '10 дней', value: 10 },
              { label: '20 дней', value: 20 },
            ]}
          />
          <Button
            type="primary"
            danger
            size="small"
            icon={<WarningOutlined />}
            loading={stress.isFetching}
            disabled={disabled}
            onClick={() => {
              setRequested(true)
              void stress.refetch()
            }}
          >
            Выполнить стресс-тест
          </Button>
        </Space>
      }
    >
      {!requested && (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Стоимостная мера риска по построению не охватывает события за пределами
          уровня доверия. Стресс-тестирование показывает, какими окажутся потери при
          реализации неблагоприятного события. Периоды исторических сценариев
          определяются алгоритмом по данным: отбираются непересекающиеся периоды
          наибольших потерь именно этого портфеля.
        </Typography.Paragraph>
      )}

      {stress.isError && <Alert type="error" message={describeError(stress.error)} />}

      {stress.isFetching && (
        <Space direction="vertical" align="center" style={{ width: '100%', padding: 24 }}>
          <Spin />
        </Space>
      )}

      {stress.data && !stress.isFetching && (
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={8}>
              <Card size="small">
                <Statistic
                  title={`VaR (${formatPercent(stress.data.confidenceLevel, 0)}, ${stress.data.horizonDays} дн.)`}
                  value={formatMoney(stress.data.valueAtRisk)}
                  valueStyle={{ fontSize: 18 }}
                />
              </Card>
            </Col>
            <Col xs={24} sm={8}>
              <Card size="small">
                <Statistic
                  title="Ожидаемые потери CVaR"
                  value={formatMoney(stress.data.expectedShortfall)}
                  valueStyle={{ fontSize: 18 }}
                />
              </Card>
            </Col>
            <Col xs={24} sm={8}>
              <Card size="small">
                <Statistic
                  title="Наибольшие потери в сценарии"
                  value={formatMoney(-(scenarios[0]?.lossAmount ?? 0))}
                  valueStyle={{ fontSize: 18, color: '#b91c1c' }}
                  suffix={
                    <Typography.Text type="secondary" style={{ fontSize: 12, marginLeft: 8 }}>
                      ×{formatNumber(scenarios[0]?.lossToVarRatio, 2)} к VaR
                    </Typography.Text>
                  }
                />
              </Card>
            </Col>
          </Row>

          {chart && (
            <Card size="small" title="Потери по сценариям" styles={{ body: { paddingTop: 8 } }}>
              <ReactECharts
                option={chart}
                style={{ height: Math.max(260, scenarios.length * 40) }}
                notMerge
              />
              <Typography.Paragraph
                type="secondary"
                style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}
              >
                Синим показаны исторические сценарии, фиолетовым — гипотетические.
                Пунктирная линия — стоимостная мера риска: всё, что правее неё,
                не охватывается обычной оценкой риска.
              </Typography.Paragraph>
            </Card>
          )}

          <Table
            rowKey="name"
            size="small"
            pagination={false}
            columns={columns}
            dataSource={scenarios}
            expandable={{
              expandedRowRender: (record) => (
                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {record.description}
                  </Typography.Text>

                  <Table
                    rowKey="instrumentId"
                    size="small"
                    pagination={false}
                    dataSource={record.impacts}
                    columns={[
                      { title: 'Тикер', dataIndex: 'ticker', width: 90 },
                      {
                        title: 'Доля',
                        dataIndex: 'weight',
                        align: 'right',
                        width: 90,
                        render: (value: number) => formatPercent(value, 1),
                      },
                      {
                        title: 'Изменение цены',
                        dataIndex: 'instrumentReturn',
                        align: 'right',
                        width: 140,
                        render: (value: number) => (
                          <Typography.Text style={{ color: value < 0 ? '#b91c1c' : '#15803d' }}>
                            {formatPercent(value, 1, true)}
                          </Typography.Text>
                        ),
                      },
                      {
                        title: 'Потери по позиции',
                        dataIndex: 'lossAmount',
                        align: 'right',
                        width: 160,
                        render: (value: number) => formatMoney(value),
                      },
                    ]}
                  />
                </Space>
              ),
            }}
          />

          <Alert
            type="warning"
            showIcon
            message="Вывод по результатам стресс-тестирования"
            description={
              <Typography.Text style={{ fontSize: 12 }}>{stress.data.conclusion}</Typography.Text>
            }
          />

          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Период выборки {formatDate(stress.data.from)} — {formatDate(stress.data.to)},
            расчёт {formatDuration(stress.data.durationMs)}
          </Typography.Text>
        </Space>
      )}
    </Card>
  )
}
