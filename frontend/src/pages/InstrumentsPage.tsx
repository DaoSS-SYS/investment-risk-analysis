import { useState } from 'react'
import {
  App,
  Button,
  Card,
  Input,
  Modal,
  Popconfirm,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import {
  CloudDownloadOutlined,
  DeleteOutlined,
  LineChartOutlined,
  PlusOutlined,
  SearchOutlined,
} from '@ant-design/icons'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import type { ColumnsType } from 'antd/es/table'

import { api, describeError } from '../api/client'
import type { Instrument, SecuritySearchResult } from '../api/types'
import { describeSecurityType, formatCount, formatDate, formatDuration } from '../lib/format'

/**
 * Справочник финансовых инструментов: просмотр, добавление из справочника
 * Московской Биржи, загрузка истории котировок.
 */
export default function InstrumentsPage() {
  const { message } = App.useApp()
  const queryClient = useQueryClient()

  const [searchOpen, setSearchOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [submittedQuery, setSubmittedQuery] = useState('')

  const instruments = useQuery({
    queryKey: ['instruments'],
    queryFn: api.instruments.list,
  })

  const searchResults = useQuery({
    queryKey: ['instrument-search', submittedQuery],
    queryFn: () => api.instruments.search(submittedQuery),
    enabled: submittedQuery.length > 0,
  })

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['instruments'] })
    void queryClient.invalidateQueries({ queryKey: ['system-info'] })
  }

  const addInstrument = useMutation({
    mutationFn: (item: SecuritySearchResult) => api.instruments.add(item.ticker, item.board),
    onSuccess: (instrument) => {
      message.success(`Инструмент ${instrument.ticker} добавлен в справочник`)
      invalidate()
      void queryClient.invalidateQueries({ queryKey: ['instrument-search'] })
    },
    onError: (error) => message.error(describeError(error)),
  })

  const importQuotes = useMutation({
    mutationFn: (id: number) => api.instruments.importQuotes(id),
    onSuccess: (result) => {
      message.success(
        `${result.ticker}: добавлено ${formatCount(result.rowsInserted)} котировок, ` +
          `обновлено ${formatCount(result.rowsUpdated)}, ${formatDuration(result.durationMs)}`,
      )
      invalidate()
    },
    onError: (error) => message.error(describeError(error)),
  })

  const removeInstrument = useMutation({
    mutationFn: (id: number) => api.instruments.remove(id),
    onSuccess: () => {
      message.success('Инструмент удалён вместе с историей котировок')
      invalidate()
    },
    onError: (error) => message.error(describeError(error)),
  })

  const columns: ColumnsType<Instrument> = [
    {
      title: 'Тикер',
      dataIndex: 'ticker',
      width: 110,
      sorter: (a, b) => a.ticker.localeCompare(b.ticker),
      render: (ticker: string, record) => (
        <Link to={`/instruments/${record.id}`} style={{ fontWeight: 600 }}>
          {ticker}
        </Link>
      ),
    },
    { title: 'Наименование', dataIndex: 'shortName', ellipsis: true },
    {
      title: 'Тип',
      dataIndex: 'securityType',
      width: 130,
      filters: [
        { text: 'Акция', value: 'Share' },
        { text: 'Индекс', value: 'Index' },
        { text: 'Облигация', value: 'Bond' },
      ],
      onFilter: (value, record) => record.securityType === value,
      render: (type: string) => <Tag>{describeSecurityType(type)}</Tag>,
    },
    { title: 'Режим торгов', dataIndex: 'board', width: 120 },
    {
      title: 'Котировок',
      dataIndex: 'quoteCount',
      width: 110,
      align: 'right',
      sorter: (a, b) => a.quoteCount - b.quoteCount,
      render: (value: number) =>
        value > 0 ? formatCount(value) : <Typography.Text type="warning">нет данных</Typography.Text>,
    },
    {
      title: 'История',
      width: 210,
      render: (_, record) =>
        record.historyFrom ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {formatDate(record.historyFrom)} — {formatDate(record.historyTo)}
          </Typography.Text>
        ) : (
          '—'
        ),
    },
    {
      title: 'Действия',
      width: 150,
      align: 'right',
      render: (_, record) => (
        <Space size={4}>
          <Tooltip title="Загрузить историю котировок за десять лет">
            <Button
              size="small"
              icon={<CloudDownloadOutlined />}
              loading={importQuotes.isPending && importQuotes.variables === record.id}
              onClick={() => importQuotes.mutate(record.id)}
            />
          </Tooltip>

          <Tooltip title="Анализ инструмента">
            <Link to={`/instruments/${record.id}`}>
              <Button size="small" icon={<LineChartOutlined />} />
            </Link>
          </Tooltip>

          <Popconfirm
            title="Удалить инструмент?"
            description="История котировок будет удалена безвозвратно."
            okText="Удалить"
            cancelText="Отмена"
            okButtonProps={{ danger: true }}
            onConfirm={() => removeInstrument.mutate(record.id)}
          >
            <Button size="small" danger icon={<DeleteOutlined />} />
          </Popconfirm>
        </Space>
      ),
    },
  ]

  const searchColumns: ColumnsType<SecuritySearchResult> = [
    { title: 'Тикер', dataIndex: 'ticker', width: 130 },
    { title: 'Наименование', dataIndex: 'shortName', ellipsis: true },
    {
      title: 'Тип',
      dataIndex: 'securityType',
      width: 120,
      render: (type: string) => <Tag>{describeSecurityType(type)}</Tag>,
    },
    { title: 'Режим', dataIndex: 'board', width: 90 },
    {
      title: '',
      width: 130,
      align: 'right',
      render: (_, record) =>
        record.alreadyAdded ? (
          <Tag color="success">добавлен</Tag>
        ) : (
          <Button
            size="small"
            type="primary"
            icon={<PlusOutlined />}
            loading={addInstrument.isPending && addInstrument.variables?.ticker === record.ticker}
            onClick={() => addInstrument.mutate(record)}
          >
            Добавить
          </Button>
        ),
    },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space style={{ width: '100%', justifyContent: 'space-between' }}>
        <Typography.Title level={4} style={{ margin: 0 }}>
          Справочник инструментов
        </Typography.Title>

        <Button type="primary" icon={<SearchOutlined />} onClick={() => setSearchOpen(true)}>
          Найти на Московской Бирже
        </Button>
      </Space>

      <Card size="small">
        <Table
          rowKey="id"
          size="small"
          columns={columns}
          dataSource={instruments.data ?? []}
          loading={instruments.isLoading}
          pagination={false}
          locale={{
            emptyText:
              'Справочник пуст. Найдите инструменты на Московской Бирже и добавьте их в систему.',
          }}
        />
      </Card>

      <Modal
        title="Поиск инструмента в справочнике Московской Биржи"
        open={searchOpen}
        onCancel={() => setSearchOpen(false)}
        footer={null}
        width={860}
      >
        <Space direction="vertical" size={12} style={{ width: '100%' }}>
          <Input.Search
            placeholder="Биржевой код или часть наименования, например SBER или Сбербанк"
            enterButton="Искать"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            onSearch={(value) => setSubmittedQuery(value.trim())}
            loading={searchResults.isFetching}
            allowClear
          />

          {searchResults.isError && (
            <Typography.Text type="danger">{describeError(searchResults.error)}</Typography.Text>
          )}

          <Table
            rowKey={(record) => `${record.ticker}|${record.board}`}
            size="small"
            columns={searchColumns}
            dataSource={searchResults.data ?? []}
            loading={searchResults.isFetching}
            pagination={{ pageSize: 8, hideOnSinglePage: true }}
            locale={{
              emptyText: submittedQuery
                ? 'Ничего не найдено. Уточните запрос.'
                : 'Введите биржевой код или наименование инструмента.',
            }}
          />
        </Space>
      </Modal>
    </Space>
  )
}
