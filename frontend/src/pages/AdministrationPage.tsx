import { useState } from 'react'
import {
  App,
  Button,
  Card,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Switch,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { ColumnsType } from 'antd/es/table'

import { api, describeError } from '../api/client'
import type { AuditRecordView, UserView } from '../api/types'
import { describeRole, useAuth } from '../auth/AuthContext'
import { formatDateTime } from '../lib/format'

interface UserFormValues {
  userName: string
  password: string
  fullName: string
  position?: string
  email?: string
  role: string
}

/** Наименование вида действия на русском языке. */
function describeAction(action: string): string {
  const names: Record<string, string> = {
    Login: 'вход',
    Create: 'создание',
    Update: 'изменение',
    Delete: 'удаление',
    Calculate: 'расчёт',
    Import: 'загрузка',
    Report: 'отчёт',
  }

  return names[action] ?? action
}

/** Цвет метки вида действия. */
function actionColor(action: string): string {
  const colors: Record<string, string> = {
    Login: 'blue',
    Create: 'green',
    Update: 'gold',
    Delete: 'red',
    Import: 'cyan',
  }

  return colors[action] ?? 'default'
}

/**
 * Администрирование: учётные записи пользователей и журнал действий.
 * Раздел доступен только пользователям с ролью администратора.
 */
export default function AdministrationPage() {
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const { user: currentUser } = useAuth()

  const [open, setOpen] = useState(false)
  const [form] = Form.useForm<UserFormValues>()

  const users = useQuery({ queryKey: ['users'], queryFn: api.auth.users })
  const roles = useQuery({ queryKey: ['roles'], queryFn: api.auth.roles })

  const audit = useQuery({
    queryKey: ['audit'],
    queryFn: () => api.auth.audit(200),
    refetchInterval: 30_000,
  })

  const createUser = useMutation({
    mutationFn: (values: UserFormValues) => api.auth.createUser(values),
    onSuccess: (created) => {
      message.success(`Учётная запись ${created.userName} создана`)
      setOpen(false)
      form.resetFields()
      void queryClient.invalidateQueries({ queryKey: ['users'] })
      void queryClient.invalidateQueries({ queryKey: ['audit'] })
    },
    onError: (error) => message.error(describeError(error)),
  })

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      api.auth.setUserActive(id, isActive),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['users'] })
      void queryClient.invalidateQueries({ queryKey: ['audit'] })
    },
    onError: (error) => message.error(describeError(error)),
  })

  const userColumns: ColumnsType<UserView> = [
    { title: 'Имя пользователя', dataIndex: 'userName', width: 150, render: (v) => <b>{v}</b> },
    { title: 'ФИО', dataIndex: 'fullName', ellipsis: true },
    { title: 'Должность', dataIndex: 'position', ellipsis: true, render: (v) => v ?? '—' },
    {
      title: 'Роль',
      dataIndex: 'roles',
      width: 160,
      render: (values: string[]) => (
        <Space size={4} wrap>
          {values.map((role) => (
            <Tag key={role} color="blue">
              {describeRole(role)}
            </Tag>
          ))}
        </Space>
      ),
    },
    {
      title: 'Последний вход',
      dataIndex: 'lastLoginAt',
      width: 180,
      render: (value: string | null) => (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {formatDateTime(value)}
        </Typography.Text>
      ),
    },
    {
      title: 'Действующая',
      dataIndex: 'isActive',
      width: 130,
      align: 'center',
      render: (isActive: boolean, record) => (
        <Switch
          size="small"
          checked={isActive}
          disabled={record.id === currentUser?.id || setActive.isPending}
          onChange={(checked) => setActive.mutate({ id: record.id, isActive: checked })}
        />
      ),
    },
  ]

  const auditColumns: ColumnsType<AuditRecordView> = [
    {
      title: 'Время',
      dataIndex: 'timestamp',
      width: 170,
      render: (value: string) => (
        <Typography.Text style={{ fontSize: 12 }}>{formatDateTime(value)}</Typography.Text>
      ),
    },
    { title: 'Пользователь', dataIndex: 'userName', width: 140 },
    {
      title: 'Действие',
      dataIndex: 'action',
      width: 120,
      filters: ['Login', 'Create', 'Update', 'Delete', 'Import'].map((value) => ({
        text: describeAction(value),
        value,
      })),
      onFilter: (value, record) => record.action === value,
      render: (action: string) => <Tag color={actionColor(action)}>{describeAction(action)}</Tag>,
    },
    { title: 'Объект', dataIndex: 'entityType', width: 110 },
    { title: 'Описание', dataIndex: 'description', ellipsis: true },
    {
      title: 'Адрес',
      dataIndex: 'ipAddress',
      width: 130,
      render: (value: string | null) => (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {value ?? '—'}
        </Typography.Text>
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Typography.Title level={4} style={{ margin: 0 }}>
        Администрирование
      </Typography.Title>

      <Tabs
        items={[
          {
            key: 'users',
            label: 'Учётные записи',
            children: (
              <Space direction="vertical" size={12} style={{ width: '100%' }}>
                <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Учётные записи не удаляются, а переводятся в недействующие:
                    удаление нарушило бы связность журнала действий.
                  </Typography.Text>

                  <Button type="primary" icon={<PlusOutlined />} onClick={() => setOpen(true)}>
                    Создать
                  </Button>
                </Space>

                <Card size="small">
                  <Table
                    rowKey="id"
                    size="small"
                    pagination={false}
                    columns={userColumns}
                    dataSource={users.data ?? []}
                    loading={users.isLoading}
                  />
                </Card>

                <Card size="small" title="Роли и полномочия">
                  <Space direction="vertical" size={8} style={{ width: '100%' }}>
                    {(roles.data ?? []).map((role) => (
                      <Space key={role.name} align="start">
                        <Tag color="blue" style={{ minWidth: 110, textAlign: 'center' }}>
                          {describeRole(role.name)}
                        </Tag>
                        <Typography.Text style={{ fontSize: 12 }}>
                          {role.description}
                        </Typography.Text>
                      </Space>
                    ))}
                  </Space>
                </Card>
              </Space>
            ),
          },
          {
            key: 'audit',
            label: 'Журнал действий',
            children: (
              <Card size="small">
                <Table
                  rowKey="id"
                  size="small"
                  columns={auditColumns}
                  dataSource={audit.data ?? []}
                  loading={audit.isLoading}
                  pagination={{ pageSize: 20, showSizeChanger: false }}
                />
              </Card>
            ),
          },
        ]}
      />

      <Modal
        title="Создание учётной записи"
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={createUser.isPending}
        okText="Создать"
        cancelText="Отмена"
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={(values) => createUser.mutate(values)}
          style={{ marginTop: 16 }}
        >
          <Form.Item
            name="userName"
            label="Имя пользователя"
            rules={[{ required: true, message: 'Укажите имя пользователя' }]}
          >
            <Input autoComplete="off" />
          </Form.Item>

          <Form.Item
            name="password"
            label="Пароль"
            tooltip="Не менее восьми знаков, строчные и прописные буквы, цифры"
            rules={[{ required: true, min: 8, message: 'Пароль не короче восьми знаков' }]}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>

          <Form.Item
            name="fullName"
            label="Фамилия, имя, отчество"
            rules={[{ required: true, message: 'Укажите фамилию, имя и отчество' }]}
          >
            <Input />
          </Form.Item>

          <Form.Item name="position" label="Должность">
            <Input />
          </Form.Item>

          <Form.Item name="email" label="Адрес электронной почты">
            <Input type="email" />
          </Form.Item>

          <Form.Item
            name="role"
            label="Роль"
            rules={[{ required: true, message: 'Выберите роль' }]}
          >
            <Select
              options={(roles.data ?? []).map((role) => ({
                value: role.name,
                label: describeRole(role.name),
              }))}
            />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
