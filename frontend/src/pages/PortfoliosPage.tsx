import { useState } from 'react'
import {
  App,
  Button,
  Card,
  Col,
  Empty,
  Form,
  Input,
  Modal,
  Row,
  Select,
  Space,
  Statistic,
  Typography,
} from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'

import { api, describeError } from '../api/client'
import { formatMoney, formatPercent } from '../lib/format'

interface FormValues {
  name: string
  description?: string
  benchmarkInstrumentId?: number
}

/**
 * Перечень инвестиционных портфелей с текущей оценкой.
 */
export default function PortfoliosPage() {
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [form] = Form.useForm<FormValues>()

  const portfolios = useQuery({
    queryKey: ['portfolios'],
    queryFn: api.portfolios.list,
  })

  const instruments = useQuery({
    queryKey: ['instruments'],
    queryFn: api.instruments.list,
  })

  const createPortfolio = useMutation({
    mutationFn: (values: FormValues) =>
      api.portfolios.create({
        name: values.name,
        description: values.description,
        baseCurrency: 'RUB',
        benchmarkInstrumentId: values.benchmarkInstrumentId ?? null,
      }),
    onSuccess: () => {
      message.success('Портфель создан')
      setOpen(false)
      form.resetFields()
      void queryClient.invalidateQueries({ queryKey: ['portfolios'] })
      void queryClient.invalidateQueries({ queryKey: ['system-info'] })
    },
    onError: (error) => message.error(describeError(error)),
  })

  // В качестве эталонного портфеля предлагаются только биржевые индексы.
  const benchmarkOptions = (instruments.data ?? [])
    .filter((instrument) => instrument.securityType === 'Index')
    .map((instrument) => ({
      value: instrument.id,
      label: `${instrument.ticker} — ${instrument.shortName}`,
    }))

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space style={{ width: '100%', justifyContent: 'space-between' }}>
        <Typography.Title level={4} style={{ margin: 0 }}>
          Инвестиционные портфели
        </Typography.Title>

        <Button type="primary" icon={<PlusOutlined />} onClick={() => setOpen(true)}>
          Создать портфель
        </Button>
      </Space>

      {portfolios.data && portfolios.data.length === 0 && !portfolios.isLoading && (
        <Card>
          <Empty description="Портфели не созданы. Создайте портфель и добавьте в него позиции." />
        </Card>
      )}

      <Row gutter={[16, 16]}>
        {(portfolios.data ?? []).map((portfolio) => (
          <Col key={portfolio.id} xs={24} md={12} xl={8}>
            <Card
              loading={portfolios.isLoading}
              title={<Link to={`/portfolios/${portfolio.id}`}>{portfolio.name}</Link>}
              extra={
                portfolio.benchmarkTicker ? (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    бенчмарк: {portfolio.benchmarkTicker}
                  </Typography.Text>
                ) : null
              }
            >
              <Space direction="vertical" size={8} style={{ width: '100%' }}>
                {portfolio.description && (
                  <Typography.Paragraph
                    type="secondary"
                    ellipsis={{ rows: 2 }}
                    style={{ marginBottom: 0, fontSize: 12 }}
                  >
                    {portfolio.description}
                  </Typography.Paragraph>
                )}

                <Row gutter={16}>
                  <Col span={12}>
                    <Statistic
                      title="Текущая стоимость"
                      value={formatMoney(portfolio.currentValue)}
                      valueStyle={{ fontSize: 18 }}
                    />
                  </Col>
                  <Col span={12}>
                    <Statistic
                      title="Результат"
                      value={formatPercent(portfolio.profitLossPercent, 1, true)}
                      valueStyle={{
                        fontSize: 18,
                        color: portfolio.profitLoss >= 0 ? '#15803d' : '#b91c1c',
                      }}
                    />
                  </Col>
                </Row>

                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Позиций: {portfolio.positions.length} · вложено{' '}
                  {formatMoney(portfolio.purchaseValue)}
                </Typography.Text>
              </Space>
            </Card>
          </Col>
        ))}
      </Row>

      <Modal
        title="Создание портфеля"
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={createPortfolio.isPending}
        okText="Создать"
        cancelText="Отмена"
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={(values) => createPortfolio.mutate(values)}
          style={{ marginTop: 16 }}
        >
          <Form.Item
            name="name"
            label="Наименование"
            rules={[{ required: true, message: 'Укажите наименование портфеля' }]}
          >
            <Input placeholder="Например: Портфель голубых фишек" />
          </Form.Item>

          <Form.Item name="description" label="Описание">
            <Input.TextArea
              rows={3}
              placeholder="Инвестиционная стратегия, назначение портфеля"
            />
          </Form.Item>

          <Form.Item
            name="benchmarkInstrumentId"
            label="Эталонный портфель"
            tooltip="Используется при расчёте коэффициента бета и альфы Йенсена"
          >
            <Select
              allowClear
              placeholder="Индекс для сопоставления, например IMOEX"
              options={benchmarkOptions}
              notFoundContent="Добавьте индекс в справочник инструментов"
            />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
