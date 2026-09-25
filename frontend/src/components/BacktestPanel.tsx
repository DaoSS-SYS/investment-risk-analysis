import { useState } from 'react'
import {
  Alert,
  Button,
  Card,
  Descriptions,
  Segmented,
  Space,
  Spin,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import { ExperimentOutlined } from '@ant-design/icons'
import { useQuery } from '@tanstack/react-query'
import ReactECharts from 'echarts-for-react'
import type { ColumnsType } from 'antd/es/table'

import { api, describeError } from '../api/client'
import type { BacktestResult, BaselZone } from '../api/types'
import { formatCount, formatDate, formatDuration, formatNumber, formatPercent } from '../lib/format'

/** Наименование метода на русском языке. */
function methodName(method: string): string {
  const names: Record<string, string> = {
    Parametric: 'Параметрический',
    Historical: 'Историческое моделирование',
    MonteCarlo: 'Монте-Карло',
  }

  return names[method] ?? method
}

/** Оформление зоны надзорной оценки. */
function zoneTag(zone: BaselZone) {
  const settings: Record<BaselZone, { color: string; label: string }> = {
    Green: { color: 'success', label: 'зелёная' },
    Yellow: { color: 'warning', label: 'жёлтая' },
    Red: { color: 'error', label: 'красная' },
  }

  const { color, label } = settings[zone]

  return <Tag color={color}>{label}</Tag>
}

/** Признак прохождения критерия. */
function verdictTag(isRejected: boolean) {
  return isRejected ? <Tag color="error">отвергнута</Tag> : <Tag color="success">принята</Tag>
}

interface Props {
  instrumentId: number
}

/**
 * Бэктестирование моделей оценки стоимостной меры риска.
 *
 * Расчёт выполняется по каждому дню периода проверки тремя методами и
 * занимает продолжительное время, поэтому запускается по явному
 * требованию пользователя.
 */
export default function BacktestPanel({ instrumentId }: Props) {
  const [confidence, setConfidence] = useState(0.99)
  const [window, setWindow] = useState(250)
  const [requested, setRequested] = useState(false)
  const [selectedMethod, setSelectedMethod] = useState('Historical')

  const backtest = useQuery({
    queryKey: ['backtest', instrumentId, confidence, window],
    queryFn: () => api.analysis.backtest(instrumentId, { confidence, window }),
    enabled: requested,
  })

  const columns: ColumnsType<BacktestResult> = [
    {
      title: 'Метод',
      dataIndex: 'method',
      render: (method: string) => methodName(method),
    },
    {
      title: 'Нарушений',
      dataIndex: 'violations',
      align: 'right',
      width: 100,
    },
    {
      title: 'Ожидалось',
      dataIndex: 'expectedViolations',
      align: 'right',
      width: 100,
      render: (value: number) => formatNumber(value, 1),
    },
    {
      title: 'Доля нарушений',
      dataIndex: 'violationRate',
      align: 'right',
      width: 130,
      render: (value: number, record) => (
        <Typography.Text
          strong
          style={{ color: value > record.expectedViolationRate ? '#b91c1c' : '#15803d' }}
        >
          {formatPercent(value, 2)}
        </Typography.Text>
      ),
    },
    {
      title: (
        <Tooltip title="Критерий Купца: соответствие частоты нарушений заявленному уровню доверия. Критическое значение 3,841">
          Купец
        </Tooltip>
      ),
      key: 'kupiec',
      align: 'right',
      width: 100,
      render: (_, record) => formatNumber(record.kupiec.statistic, 2),
    },
    {
      title: 'Гипотеза',
      key: 'kupiecVerdict',
      align: 'center',
      width: 110,
      render: (_, record) => verdictTag(record.kupiec.isRejected),
    },
    {
      title: (
        <Tooltip title="Критерий Кристоферсена: независимость нарушений во времени. Критическое значение 3,841">
          Независимость
        </Tooltip>
      ),
      key: 'independence',
      align: 'right',
      width: 130,
      render: (_, record) => formatNumber(record.christoffersen.independenceStatistic, 2),
    },
    {
      title: 'Гипотеза',
      key: 'independenceVerdict',
      align: 'center',
      width: 110,
      render: (_, record) => verdictTag(record.christoffersen.independenceRejected),
    },
    {
      title: (
        <Tooltip title="Зона надзорной оценки по подходу Базельского комитета">
          Зона
        </Tooltip>
      ),
      dataIndex: 'zone',
      align: 'center',
      width: 100,
      render: (zone: BaselZone) => zoneTag(zone),
    },
  ]

  const selected = backtest.data?.results.find((result) => result.method === selectedMethod)

  // График нарушений: фактические доходности, линия границы оценки риска
  // и выделенные точки нарушений.
  const violationChart = selected
    ? {
        tooltip: {
          trigger: 'axis',
          axisPointer: { type: 'cross' },
          valueFormatter: (value: number) => formatPercent(value, 2),
        },
        legend: {
          data: ['Фактическая доходность', 'Граница оценки риска', 'Нарушения'],
          top: 0,
        },
        grid: { left: 60, right: 24, top: 40, bottom: 56 },
        xAxis: {
          type: 'category',
          data: selected.series.map((point) => point.date),
          axisLabel: { formatter: (value: string) => formatDate(value) },
        },
        yAxis: {
          type: 'value',
          axisLabel: { formatter: (value: number) => `${(value * 100).toFixed(0)} %` },
        },
        dataZoom: [{ type: 'inside' }, { type: 'slider', height: 22, bottom: 12 }],
        series: [
          {
            name: 'Фактическая доходность',
            type: 'line',
            showSymbol: false,
            lineStyle: { width: 0.8, color: '#94a3b8' },
            data: selected.series.map((point) => point.actualReturn),
          },
          {
            name: 'Граница оценки риска',
            type: 'line',
            showSymbol: false,
            lineStyle: { width: 1.6, color: '#1d4ed8' },
            data: selected.series.map((point) => -point.varEstimate),
          },
          {
            name: 'Нарушения',
            type: 'scatter',
            symbolSize: 7,
            itemStyle: { color: '#dc2626' },
            data: selected.series.map((point, index) =>
              point.isViolation ? [index, point.actualReturn] : null,
            ),
          },
        ],
      }
    : null

  return (
    <Card
      title="Бэктестирование моделей оценки риска"
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
            value={window}
            onChange={(value) => setWindow(Number(value))}
            options={[
              { label: 'окно 250', value: 250 },
              { label: 'окно 500', value: 500 },
            ]}
          />
          <Button
            type="primary"
            size="small"
            icon={<ExperimentOutlined />}
            loading={backtest.isFetching}
            onClick={() => {
              setRequested(true)
              void backtest.refetch()
            }}
          >
            Проверить модели
          </Button>
        </Space>
      }
    >
      {!requested && (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Бэктестирование проверяет, согласуются ли оценки риска с фактически
          наблюдавшимися потерями. Для каждого дня периода проверки оценка строится
          только по предшествующим наблюдениям и сопоставляется с фактической доходностью.
          Расчёт тремя методами занимает около десяти секунд.
        </Typography.Paragraph>
      )}

      {backtest.isError && <Alert type="error" message={describeError(backtest.error)} />}

      {backtest.isFetching && (
        <Space direction="vertical" align="center" style={{ width: '100%', padding: 24 }}>
          <Spin />
          <Typography.Text type="secondary">
            Выполняется проверка моделей на всей истории котировок
          </Typography.Text>
        </Space>
      )}

      {backtest.data && !backtest.isFetching && (
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <Descriptions size="small" column={{ xs: 1, sm: 2, md: 2, lg: 4 }} bordered>
            <Descriptions.Item label="Период">
              {formatDate(backtest.data.from)} — {formatDate(backtest.data.to)}
            </Descriptions.Item>
            <Descriptions.Item label="Проверено наблюдений">
              {formatCount(backtest.data.results[0]?.observations)}
            </Descriptions.Item>
            <Descriptions.Item label="Ожидаемая доля нарушений">
              {formatPercent(backtest.data.results[0]?.expectedViolationRate, 2)}
            </Descriptions.Item>
            <Descriptions.Item label="Длительность расчёта">
              {formatDuration(backtest.data.durationMs)}
            </Descriptions.Item>
          </Descriptions>

          <Table
            rowKey="method"
            size="small"
            pagination={false}
            columns={columns}
            dataSource={backtest.data.results}
            onRow={(record) => ({
              onClick: () => setSelectedMethod(record.method),
              style: {
                cursor: 'pointer',
                background: record.method === selectedMethod ? '#eff6ff' : undefined,
              },
            })}
          />

          <Alert
            type={
              backtest.data.results.every((r) => !r.kupiec.isRejected) ? 'success' : 'warning'
            }
            showIcon
            message="Вывод по результатам проверки"
            description={
              <Typography.Text style={{ fontSize: 12 }}>
                {backtest.data.conclusion}
              </Typography.Text>
            }
          />

          {violationChart && selected && (
            <Card
              size="small"
              title={`Нарушения: ${methodName(selected.method)}`}
              extra={
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  выберите строку таблицы для смены метода
                </Typography.Text>
              }
              styles={{ body: { paddingTop: 8 } }}
            >
              <ReactECharts option={violationChart} style={{ height: 320 }} notMerge />

              <Typography.Paragraph
                type="secondary"
                style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}
              >
                Серая линия — фактическая доходность, синяя — граница оценки риска,
                красные точки — дни, в которые убыток превысил оценку. Скопление красных
                точек указывает на группирование нарушений во времени: модель не успевает
                реагировать на рост волатильности.
              </Typography.Paragraph>
            </Card>
          )}

          {selected && (
            <Descriptions
              size="small"
              column={{ xs: 1, sm: 2 }}
              bordered
              title={`Подробно: ${methodName(selected.method)}`}
            >
              <Descriptions.Item label="Критерий Купца">
                {formatNumber(selected.kupiec.statistic, 3)} при критическом{' '}
                {formatNumber(selected.kupiec.criticalValue, 3)}
              </Descriptions.Item>
              <Descriptions.Item label="Условное покрытие">
                {formatNumber(selected.christoffersen.conditionalCoverageStatistic, 3)}
              </Descriptions.Item>
              <Descriptions.Item label="Матрица переходов">
                n00={selected.christoffersen.n00}, n01={selected.christoffersen.n01},
                n10={selected.christoffersen.n10}, n11={selected.christoffersen.n11}
              </Descriptions.Item>
              <Descriptions.Item label="Надбавка к множителю капитала">
                {formatNumber(selected.capitalMultiplierAddOn, 2)}
              </Descriptions.Item>
              <Descriptions.Item label="Средняя оценка риска">
                {formatPercent(selected.averageVar, 2)}
              </Descriptions.Item>
              <Descriptions.Item label="Среднее превышение при нарушении">
                {formatPercent(selected.averageViolationSize, 2)}
              </Descriptions.Item>
              <Descriptions.Item label="Вывод" span={2}>
                <Typography.Text style={{ fontSize: 12 }}>{selected.conclusion}</Typography.Text>
              </Descriptions.Item>
            </Descriptions>
          )}
        </Space>
      )}
    </Card>
  )
}
