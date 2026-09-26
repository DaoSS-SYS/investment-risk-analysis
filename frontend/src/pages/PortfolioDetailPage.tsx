import { useState } from 'react'
import {
  Alert,
  App,
  Button,
  Card,
  Col,
  DatePicker,
  Descriptions,
  Form,
  InputNumber,
  Modal,
  Popconfirm,
  Row,
  Segmented,
  Select,
  Space,
  Spin,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd'
import {
  DeleteOutlined,
  FileExcelOutlined,
  FilePdfOutlined,
  PlusOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import ReactECharts from 'echarts-for-react'
import dayjs from 'dayjs'
import type { ColumnsType } from 'antd/es/table'

import { api, describeError } from '../api/client'
import OptimizationPanel from '../components/OptimizationPanel'
import StressTestPanel from '../components/StressTestPanel'
import { useAuth } from '../auth/AuthContext'
import type { Position, PositionRiskContribution, VarResult } from '../api/types'
import {
  describeVarMethod,
  formatCount,
  formatDate,
  formatDuration,
  formatMoney,
  formatPercent,
} from '../lib/format'

interface PositionFormValues {
  instrumentId: number
  quantity: number
  purchasePrice: number
  purchaseDate: dayjs.Dayjs
}

/**
 * Портфель: состав позиций, текущая оценка и результаты расчёта риска
 * с разложением по позициям.
 */
export default function PortfolioDetailPage() {
  const { id } = useParams<{ id: string }>()
  const portfolioId = Number(id)

  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const { canManagePortfolios } = useAuth()

  const [positionOpen, setPositionOpen] = useState(false)
  const [form] = Form.useForm<PositionFormValues>()

  const [confidence, setConfidence] = useState(0.99)
  const [horizon, setHorizon] = useState(1)
  const [scenarios, setScenarios] = useState(100_000)
  const [riskRequested, setRiskRequested] = useState(false)

  const portfolio = useQuery({
    queryKey: ['portfolio', portfolioId],
    queryFn: () => api.portfolios.get(portfolioId),
    enabled: Number.isFinite(portfolioId),
  })

  const instruments = useQuery({ queryKey: ['instruments'], queryFn: api.instruments.list })

  const risk = useQuery({
    queryKey: ['portfolio-risk', portfolioId, confidence, horizon, scenarios],
    queryFn: () => api.portfolios.risk(portfolioId, { confidence, horizon, scenarios }),
    enabled: riskRequested && Number.isFinite(portfolioId),
  })

  const addPosition = useMutation({
    mutationFn: (values: PositionFormValues) =>
      api.portfolios.addPosition(portfolioId, {
        instrumentId: values.instrumentId,
        quantity: values.quantity,
        purchasePrice: values.purchasePrice,
        purchaseDate: values.purchaseDate.format('YYYY-MM-DD'),
      }),
    onSuccess: () => {
      message.success('Позиция добавлена')
      setPositionOpen(false)
      form.resetFields()
      void queryClient.invalidateQueries({ queryKey: ['portfolio', portfolioId] })
      void queryClient.invalidateQueries({ queryKey: ['portfolio-risk', portfolioId] })
    },
    onError: (error) => message.error(describeError(error)),
  })

  const removePosition = useMutation({
    mutationFn: (positionId: number) => api.portfolios.removePosition(portfolioId, positionId),
    onSuccess: () => {
      message.success('Позиция удалена')
      void queryClient.invalidateQueries({ queryKey: ['portfolio', portfolioId] })
      void queryClient.invalidateQueries({ queryKey: ['portfolio-risk', portfolioId] })
    },
    onError: (error) => message.error(describeError(error)),
  })

  const positionColumns: ColumnsType<Position> = [
    { title: 'Тикер', dataIndex: 'ticker', width: 90, render: (v: string) => <b>{v}</b> },
    { title: 'Наименование', dataIndex: 'shortName', ellipsis: true },
    {
      title: 'Количество',
      dataIndex: 'quantity',
      align: 'right',
      width: 110,
      render: (value: number) => formatCount(value),
    },
    {
      title: 'Цена покупки',
      dataIndex: 'purchasePrice',
      align: 'right',
      width: 120,
      render: (value: number) => formatMoney(value),
    },
    {
      title: 'Текущая цена',
      dataIndex: 'lastPrice',
      align: 'right',
      width: 120,
      render: (value: number | null) => formatMoney(value),
    },
    {
      title: 'Стоимость',
      dataIndex: 'currentValue',
      align: 'right',
      width: 130,
      render: (value: number) => formatMoney(value),
    },
    {
      title: 'Результат',
      dataIndex: 'profitLossPercent',
      align: 'right',
      width: 110,
      render: (value: number) => (
        <Typography.Text style={{ color: value >= 0 ? '#15803d' : '#b91c1c' }}>
          {formatPercent(value, 1, true)}
        </Typography.Text>
      ),
    },
    {
      title: 'Доля',
      dataIndex: 'weight',
      align: 'right',
      width: 90,
      render: (value: number) => formatPercent(value, 1),
    },
    {
      title: '',
      width: 50,
      align: 'right',
      hidden: !canManagePortfolios,
      render: (_, record) => (
        <Popconfirm
          title="Удалить позицию?"
          okText="Удалить"
          cancelText="Отмена"
          okButtonProps={{ danger: true }}
          onConfirm={() => removePosition.mutate(record.id)}
        >
          <Button size="small" danger type="text" icon={<DeleteOutlined />} />
        </Popconfirm>
      ),
    },
  ]

  const varColumns: ColumnsType<VarResult> = [
    {
      title: 'Метод',
      dataIndex: 'description',
      render: (description: string) => describeVarMethod(description),
    },
    {
      title: 'VaR',
      dataIndex: 'valueAtRiskAbsolute',
      align: 'right',
      width: 140,
      render: (value: number) => formatMoney(value),
    },
    {
      title: 'VaR, % стоимости',
      dataIndex: 'valueAtRiskRelative',
      align: 'right',
      width: 140,
      render: (value: number) => formatPercent(value, 2),
    },
    {
      title: 'CVaR',
      dataIndex: 'expectedShortfallAbsolute',
      align: 'right',
      width: 140,
      render: (value: number) => <Typography.Text strong>{formatMoney(value)}</Typography.Text>,
    },
  ]

  const contributionColumns: ColumnsType<PositionRiskContribution> = [
    { title: 'Тикер', dataIndex: 'ticker', width: 90, render: (v: string) => <b>{v}</b> },
    {
      title: 'Доля в портфеле',
      dataIndex: 'weight',
      align: 'right',
      width: 130,
      render: (value: number) => formatPercent(value, 1),
    },
    {
      title: 'Вклад в риск',
      dataIndex: 'contributionShare',
      align: 'right',
      width: 130,
      render: (value: number, record) => (
        <Typography.Text
          strong
          style={{ color: value > record.weight ? '#b91c1c' : '#15803d' }}
        >
          {formatPercent(value, 1)}
        </Typography.Text>
      ),
    },
    {
      title: 'Компонентная VaR',
      dataIndex: 'componentVar',
      align: 'right',
      width: 150,
      render: (value: number) => formatMoney(value),
    },
    {
      title: 'Обособленная VaR',
      dataIndex: 'standaloneVar',
      align: 'right',
      width: 150,
      render: (value: number) => formatMoney(value),
    },
    {
      title: 'Выигрыш от диверсификации',
      dataIndex: 'diversificationBenefit',
      align: 'right',
      width: 180,
      render: (value: number) => (
        <Typography.Text style={{ color: '#15803d' }}>{formatMoney(value)}</Typography.Text>
      ),
    },
  ]

  // Сопоставление доли позиции в портфеле и её вклада в риск.
  // Расхождение столбцов показывает влияние корреляции инструмента
  // с остальным портфелем.
  const contributions = risk.data?.contributions ?? []

  const contributionChart = {
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      valueFormatter: (value: number) => formatPercent(value, 1),
    },
    legend: { data: ['Доля в портфеле', 'Вклад в риск'], top: 0 },
    grid: { left: 60, right: 24, top: 40, bottom: 40 },
    xAxis: { type: 'category', data: contributions.map((item) => item.ticker) },
    yAxis: {
      type: 'value',
      axisLabel: { formatter: (value: number) => `${(value * 100).toFixed(0)} %` },
    },
    series: [
      {
        name: 'Доля в портфеле',
        type: 'bar',
        itemStyle: { color: '#93c5fd' },
        data: contributions.map((item) => item.weight),
      },
      {
        name: 'Вклад в риск',
        type: 'bar',
        itemStyle: { color: '#1d4ed8' },
        data: contributions.map((item) => item.contributionShare),
      },
    ],
  }

  const instrumentOptions = (instruments.data ?? []).map((instrument) => ({
    value: instrument.id,
    label: `${instrument.ticker} — ${instrument.shortName}`,
  }))

  const data = portfolio.data

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space style={{ width: '100%', justifyContent: 'space-between' }} wrap>
        <Space align="baseline" wrap>
          <Typography.Title level={4} style={{ margin: 0 }}>
            {data?.name ?? 'Портфель'}
          </Typography.Title>
          {data?.benchmarkTicker && <Tag>бенчмарк: {data.benchmarkTicker}</Tag>}
        </Space>

        <Space wrap>
          <Button
            icon={<FilePdfOutlined />}
            disabled={!data || data.positions.length === 0}
            href={api.portfolios.reportUrl(portfolioId, 'pdf', { confidence, horizon })}
            target="_blank"
          >
            Отчёт PDF
          </Button>

          <Button
            icon={<FileExcelOutlined />}
            disabled={!data || data.positions.length === 0}
            href={api.portfolios.reportUrl(portfolioId, 'xlsx', { confidence, horizon })}
            target="_blank"
          >
            Отчёт Excel
          </Button>

          {canManagePortfolios && (
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setPositionOpen(true)}>
              Добавить позицию
            </Button>
          )}
        </Space>
      </Space>

      {portfolio.isError && <Alert type="error" message={describeError(portfolio.error)} />}

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title="Вложено"
              value={formatMoney(data?.purchaseValue)}
              loading={portfolio.isLoading}
              valueStyle={{ fontSize: 20 }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title="Текущая стоимость"
              value={formatMoney(data?.currentValue)}
              loading={portfolio.isLoading}
              valueStyle={{ fontSize: 20 }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card size="small">
            <Statistic
              title="Финансовый результат"
              value={formatPercent(data?.profitLossPercent, 1, true)}
              loading={portfolio.isLoading}
              valueStyle={{
                fontSize: 20,
                color: (data?.profitLoss ?? 0) >= 0 ? '#15803d' : '#b91c1c',
              }}
            />
          </Card>
        </Col>
      </Row>

      <Card title="Состав портфеля" size="small">
        <Table
          rowKey="id"
          size="small"
          pagination={false}
          columns={positionColumns}
          dataSource={data?.positions ?? []}
          loading={portfolio.isLoading}
          locale={{ emptyText: 'Позиции не добавлены' }}
        />
      </Card>

      <Card
        title="Оценка риска портфеля"
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
            <Select
              size="small"
              style={{ width: 150 }}
              value={scenarios}
              onChange={setScenarios}
              options={[
                { value: 10_000, label: '10 тыс. сценариев' },
                { value: 100_000, label: '100 тыс. сценариев' },
                { value: 500_000, label: '500 тыс. сценариев' },
              ]}
            />
            <Button
              type="primary"
              size="small"
              icon={<ThunderboltOutlined />}
              loading={risk.isFetching}
              disabled={!data || data.positions.length === 0}
              onClick={() => {
                setRiskRequested(true)
                void risk.refetch()
              }}
            >
              Рассчитать
            </Button>
          </Space>
        }
      >
        {!riskRequested && (
          <Typography.Text type="secondary">
            Задайте параметры и нажмите «Рассчитать». Расчёт выполняется по истории котировок
            за десять лет всеми реализованными методами.
          </Typography.Text>
        )}

        {risk.isError && <Alert type="error" message={describeError(risk.error)} />}

        {risk.isFetching && <Spin />}

        {risk.data && !risk.isFetching && (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Descriptions size="small" column={{ xs: 1, sm: 2, lg: 4 }} bordered>
              <Descriptions.Item label="Период">
                {formatDate(risk.data.from)} — {formatDate(risk.data.to)}
              </Descriptions.Item>
              <Descriptions.Item label="Наблюдений">
                {formatCount(risk.data.observationCount)}
              </Descriptions.Item>
              <Descriptions.Item label="Волатильность портфеля">
                {formatPercent(risk.data.portfolioVolatilityAnnualized)} годовых
              </Descriptions.Item>
              <Descriptions.Item label="Длительность расчёта">
                {formatDuration(risk.data.durationMs)}
              </Descriptions.Item>
            </Descriptions>

            <Table
              rowKey={(record) => record.description}
              size="small"
              pagination={false}
              columns={varColumns}
              dataSource={risk.data.estimates}
            />

            <Row gutter={[16, 16]}>
              <Col xs={24} xl={12}>
                <Card
                  size="small"
                  title="Доля в портфеле и вклад в риск"
                  styles={{ body: { paddingTop: 8 } }}
                >
                  <ReactECharts option={contributionChart} style={{ height: 280 }} notMerge />
                </Card>
              </Col>

              <Col xs={24} xl={12}>
                <Card size="small" title="Эффект диверсификации">
                  <Space direction="vertical" size={12} style={{ width: '100%' }}>
                    <Row gutter={16}>
                      <Col span={12}>
                        <Statistic
                          title="Сумма обособленных мер риска"
                          value={formatMoney(risk.data.sumOfStandaloneVar)}
                          valueStyle={{ fontSize: 18 }}
                        />
                      </Col>
                      <Col span={12}>
                        <Statistic
                          title="Риск портфеля"
                          value={formatMoney(risk.data.estimates[0]?.valueAtRiskAbsolute)}
                          valueStyle={{ fontSize: 18, color: '#15803d' }}
                        />
                      </Col>
                    </Row>

                    <Alert
                      type="info"
                      showIcon
                      message={`Диверсификация снижает риск на ${formatPercent(
                        1 -
                          (risk.data.estimates[0]?.valueAtRiskAbsolute ?? 0) /
                            risk.data.sumOfStandaloneVar,
                        1,
                      )}`}
                      description={
                        <Typography.Text style={{ fontSize: 12 }}>
                          {risk.data.conclusion}
                        </Typography.Text>
                      }
                    />
                  </Space>
                </Card>
              </Col>
            </Row>

            <Card size="small" title="Разложение риска по позициям">
              <Table
                rowKey="instrumentId"
                size="small"
                pagination={false}
                columns={contributionColumns}
                dataSource={contributions}
                summary={() => (
                  <Table.Summary.Row style={{ fontWeight: 600, background: '#fafafa' }}>
                    <Table.Summary.Cell index={0}>Итого</Table.Summary.Cell>
                    <Table.Summary.Cell index={1} align="right">
                      100,0 %
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={2} align="right">
                      100,0 %
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={3} align="right">
                      {formatMoney(
                        contributions.reduce((sum, item) => sum + item.componentVar, 0),
                      )}
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={4} align="right">
                      {formatMoney(risk.data!.sumOfStandaloneVar)}
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={5} align="right">
                      {formatMoney(
                        contributions.reduce(
                          (sum, item) => sum + item.diversificationBenefit,
                          0,
                        ),
                      )}
                    </Table.Summary.Cell>
                  </Table.Summary.Row>
                )}
              />

              <Typography.Paragraph
                type="secondary"
                style={{ marginTop: 12, marginBottom: 0, fontSize: 12 }}
              >
                Сумма компонентных мер риска в точности равна стоимостной мере риска портфеля
                (тождество Эйлера). Позиция, вклад которой в риск превышает её долю в портфеле,
                выделена красным: её сокращение снижает риск сильнее прочих.
              </Typography.Paragraph>
            </Card>
          </Space>
        )}
      </Card>

      <StressTestPanel
        portfolioId={portfolioId}
        disabled={!data || data.positions.length === 0}
      />

      <OptimizationPanel
        portfolioId={portfolioId}
        disabled={!data || data.positions.length < 2}
      />

      <Modal
        title="Добавление позиции"
        open={positionOpen}
        onCancel={() => setPositionOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={addPosition.isPending}
        okText="Добавить"
        cancelText="Отмена"
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={(values) => addPosition.mutate(values)}
          initialValues={{ purchaseDate: dayjs() }}
          style={{ marginTop: 16 }}
        >
          <Form.Item
            name="instrumentId"
            label="Инструмент"
            rules={[{ required: true, message: 'Выберите инструмент' }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder="Выберите инструмент из справочника"
              options={instrumentOptions}
            />
          </Form.Item>

          <Form.Item
            name="quantity"
            label="Количество"
            rules={[{ required: true, message: 'Укажите количество' }]}
          >
            <InputNumber style={{ width: '100%' }} min={0.000001} step={1} />
          </Form.Item>

          <Form.Item
            name="purchasePrice"
            label="Цена приобретения, ₽"
            rules={[{ required: true, message: 'Укажите цену' }]}
          >
            <InputNumber style={{ width: '100%' }} min={0.000001} step={1} />
          </Form.Item>

          <Form.Item
            name="purchaseDate"
            label="Дата приобретения"
            rules={[{ required: true, message: 'Укажите дату' }]}
          >
            <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
