import { useState } from 'react'
import { Alert, Button, Card, Form, Input, Layout, Space, Typography } from 'antd'
import { BarChartOutlined, LockOutlined, UserOutlined } from '@ant-design/icons'

import { useAuth } from '../auth/AuthContext'
import { describeError } from '../api/client'

interface FormValues {
  userName: string
  password: string
}

/**
 * Страница входа в систему.
 *
 * Отображается вместо основного интерфейса, пока пользователь не прошёл
 * проверку подлинности.
 */
export default function LoginPage() {
  const { login } = useAuth()
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = async (values: FormValues) => {
    setError(null)
    setIsSubmitting(true)

    try {
      await login(values.userName.trim(), values.password)
    } catch (exception) {
      setError(describeError(exception))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Layout style={{ minHeight: '100vh', background: '#0f172a' }}>
      <Layout.Content
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: 24,
        }}
      >
        <Space direction="vertical" size={20} style={{ width: '100%', maxWidth: 420 }}>
          <Space direction="vertical" size={4} style={{ width: '100%', textAlign: 'center' }}>
            <BarChartOutlined style={{ fontSize: 34, color: '#93c5fd' }} />
            <Typography.Title level={4} style={{ color: '#f8fafc', margin: 0 }}>
              Количественный анализ рисков
            </Typography.Title>
            <Typography.Text style={{ color: '#94a3b8', fontSize: 13 }}>
              инвестиционной деятельности
            </Typography.Text>
          </Space>

          <Card>
            <Typography.Title level={5} style={{ marginTop: 0 }}>
              Вход в систему
            </Typography.Title>

            <Form<FormValues> layout="vertical" onFinish={handleSubmit} requiredMark={false}>
              <Form.Item
                name="userName"
                label="Имя пользователя"
                rules={[{ required: true, message: 'Укажите имя пользователя' }]}
              >
                <Input
                  size="large"
                  prefix={<UserOutlined style={{ color: '#94a3b8' }} />}
                  autoFocus
                  autoComplete="username"
                />
              </Form.Item>

              <Form.Item
                name="password"
                label="Пароль"
                rules={[{ required: true, message: 'Укажите пароль' }]}
              >
                <Input.Password
                  size="large"
                  prefix={<LockOutlined style={{ color: '#94a3b8' }} />}
                  autoComplete="current-password"
                />
              </Form.Item>

              {error && (
                <Form.Item>
                  <Alert type="error" showIcon message={error} />
                </Form.Item>
              )}

              <Form.Item style={{ marginBottom: 0 }}>
                <Button
                  type="primary"
                  size="large"
                  htmlType="submit"
                  block
                  loading={isSubmitting}
                >
                  Войти
                </Button>
              </Form.Item>
            </Form>
          </Card>

          <Typography.Text style={{ color: '#64748b', fontSize: 11, textAlign: 'center' }}>
            Выпускная квалификационная работа · Финансовый университет при Правительстве
            Российской Федерации
          </Typography.Text>
        </Space>
      </Layout.Content>
    </Layout>
  )
}
