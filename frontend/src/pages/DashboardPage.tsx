import { useState } from 'react'
import { App, Button, Card, Col, Descriptions, Row, Space, Statistic, Tag, Typography } from 'antd'
import { CloudDownloadOutlined, ReloadOutlined, SafetyOutlined } from '@ant-design/icons'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { api, describeError } from '../api/client'
import { formatCount, formatDuration } from '../lib/format'

/**
 * Обзорная страница: состояние системы и операции загрузки данных.
 */
export default function DashboardPage() {
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const [lastOperation, setLastOperation] = useState<string | null>(null)

  const info = useQuery({
    queryKey: ['system-info'],
    queryFn: api.system.info,
    refetchInterval: 30_000,
  })

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['system-info'] })
    void queryClient.invalidateQueries({ queryKey: ['instruments'] })
  }

  const importKeyRate = useMutation({
    mutationFn: () => api.importData.keyRate(),
    onSuccess: (result) => {
      setLastOperation(
        `Ключевая ставка Банка России: получено ${formatCount(result.rowsReceived)} значений, ` +
          `добавлено ${formatCount(result.rowsInserted)}, ${formatDuration(result.durationMs)}.`,
      )
      message.success('Ключевая ставка загружена')
      invalidate()
    },
    onError: (error) => message.error(describeError(error)),
  })

  const importQuotes = useMutation({
    mutationFn: () => api.importData.allQuotes(),
    onSuccess: (results) => {
      const inserted = results.reduce((sum, item) => sum + item.rowsInserted, 0)
      const updated = results.reduce((sum, item) => sum + item.rowsUpdated, 0)

      setLastOperation(
        `Котировки: обработано инструментов ${results.length}, ` +
          `добавлено ${formatCount(inserted)} записей, обновлено ${formatCount(updated)}.`,
      )
      message.success('Котировки догружены')
      invalidate()
    },
    onError: (error) => message.error(describeError(error)),
  })

  const detectActions = useMutation({
    mutationFn: () => api.corporateActions.detectAll(),
    onSuccess: () => {
      setLastOperation('Предобработка данных выполнена: ценовые ряды проверены на аномалии.')
      message.success('Предобработка выполнена')
      invalidate()
    },
    onError: (error) => message.error(describeError(error)),
  })

  const data = info.data

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Typography.Title level={4} style={{ margin: 0 }}>
        Обзор
      </Typography.Title>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Инструментов в справочнике"
              value={data?.instrumentCount ?? 0}
              loading={info.isLoading}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Котировок в базе данных"
              value={data?.quoteCount ?? 0}
              loading={info.isLoading}
              formatter={(value) => formatCount(Number(value))}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Портфелей"
              value={data?.portfolioCount ?? 0}
              loading={info.isLoading}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="База данных"
              value={data?.databaseAvailable ? 'Доступна' : 'Недоступна'}
              loading={info.isLoading}
              valueStyle={{ color: data?.databaseAvailable ? '#15803d' : '#b91c1c' }}
            />
          </Card>
        </Col>
      </Row>

      <Card title="Загрузка и подготовка данных" size="small">
        <Space wrap>
          <Button
            icon={<CloudDownloadOutlined />}
            loading={importQuotes.isPending}
            onClick={() => importQuotes.mutate()}
          >
            Догрузить котировки с Московской Биржи
          </Button>

          <Button
            icon={<ReloadOutlined />}
            loading={importKeyRate.isPending}
            onClick={() => importKeyRate.mutate()}
          >
            Загрузить ключевую ставку Банка России
          </Button>

          <Button
            icon={<SafetyOutlined />}
            loading={detectActions.isPending}
            onClick={() => detectActions.mutate()}
          >
            Выполнить предобработку данных
          </Button>
        </Space>

        {lastOperation && (
          <Typography.Paragraph type="secondary" style={{ marginTop: 16, marginBottom: 0 }}>
            {lastOperation}
          </Typography.Paragraph>
        )}
      </Card>

      <Card title="Сведения о системе" size="small">
        <Descriptions column={{ xs: 1, sm: 1, md: 2 }} size="small" bordered>
          <Descriptions.Item label="Наименование">
            {data?.application ?? '—'}
          </Descriptions.Item>
          <Descriptions.Item label="Версия">{data?.version ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Узел размещения">
            {data?.machineName ?? '—'}
          </Descriptions.Item>
          <Descriptions.Item label="Применённые миграции">
            <Space direction="vertical" size={2}>
              {(data?.appliedMigrations ?? []).map((migration) => (
                <Tag key={migration} style={{ fontFamily: 'monospace', fontSize: 11 }}>
                  {migration}
                </Tag>
              ))}
            </Space>
          </Descriptions.Item>
        </Descriptions>
      </Card>
    </Space>
  )
}
