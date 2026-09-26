import { Dropdown, Layout, Menu, Space, Spin, Tag, Typography } from 'antd'
import {
  AuditOutlined,
  BarChartOutlined,
  DatabaseOutlined,
  DashboardOutlined,
  FundOutlined,
  LogoutOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { Link, Navigate, Route, Routes, useLocation } from 'react-router-dom'

import DashboardPage from './pages/DashboardPage'
import InstrumentsPage from './pages/InstrumentsPage'
import InstrumentAnalysisPage from './pages/InstrumentAnalysisPage'
import PortfoliosPage from './pages/PortfoliosPage'
import PortfolioDetailPage from './pages/PortfolioDetailPage'
import AdministrationPage from './pages/AdministrationPage'
import LoginPage from './pages/LoginPage'
import { describeRole, useAuth } from './auth/AuthContext'

const { Header, Sider, Content, Footer } = Layout

/**
 * Корневой компонент клиентского приложения: общая разметка, навигация
 * и таблица маршрутов.
 *
 * Состав пунктов меню зависит от ролей пользователя. Сокрытие пунктов
 * является удобством представления, а не средством защиты: разграничение
 * доступа обеспечивается на стороне сервера.
 */
export default function App() {
  const location = useLocation()
  const { isAuthenticated, isRestoring, user, logout, canManageUsers } = useAuth()

  if (isRestoring) {
    return (
      <Layout style={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}>
        <Spin size="large" />
      </Layout>
    )
  }

  if (!isAuthenticated) {
    return <LoginPage />
  }

  const menuItems = [
    {
      key: '/dashboard',
      icon: <DashboardOutlined />,
      label: <Link to="/dashboard">Обзор</Link>,
    },
    {
      key: '/instruments',
      icon: <DatabaseOutlined />,
      label: <Link to="/instruments">Инструменты</Link>,
    },
    {
      key: '/portfolios',
      icon: <FundOutlined />,
      label: <Link to="/portfolios">Портфели</Link>,
    },
    ...(canManageUsers
      ? [
          {
            key: '/administration',
            icon: <AuditOutlined />,
            label: <Link to="/administration">Администрирование</Link>,
          },
        ]
      : []),
  ]

  // Пункт меню выделяется по первому сегменту пути, чтобы при переходе
  // к подчинённым страницам выделение раздела сохранялось.
  const selectedKey = `/${location.pathname.split('/')[1] || 'dashboard'}`

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Header
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 12,
          paddingInline: 24,
          background: '#0f172a',
        }}
      >
        <BarChartOutlined style={{ fontSize: 22, color: '#93c5fd' }} />

        <Typography.Title level={5} style={{ margin: 0, color: '#f8fafc', flex: 1 }}>
          Количественный анализ рисков инвестиционной деятельности
        </Typography.Title>

        <Dropdown
          menu={{
            items: [
              {
                key: 'info',
                disabled: true,
                label: (
                  <Space direction="vertical" size={0}>
                    <Typography.Text strong>{user?.fullName}</Typography.Text>
                    {user?.position && (
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {user.position}
                      </Typography.Text>
                    )}
                  </Space>
                ),
              },
              { type: 'divider' },
              {
                key: 'logout',
                icon: <LogoutOutlined />,
                label: 'Выйти',
                onClick: logout,
              },
            ],
          }}
        >
          <Space style={{ cursor: 'pointer', color: '#e2e8f0' }}>
            <UserOutlined />
            <span>{user?.userName}</span>
            {(user?.roles ?? []).map((role) => (
              <Tag key={role} color="blue" style={{ marginInlineEnd: 0 }}>
                {describeRole(role)}
              </Tag>
            ))}
          </Space>
        </Dropdown>
      </Header>

      <Layout>
        <Sider width={220} theme="light" breakpoint="lg" collapsedWidth={64}>
          <Menu
            mode="inline"
            selectedKeys={[selectedKey]}
            items={menuItems}
            style={{ height: '100%', borderInlineEnd: 0, paddingTop: 12 }}
          />
        </Sider>

        <Layout>
          <Content style={{ padding: 24, background: '#f5f7fa' }}>
            <Routes>
              <Route path="/" element={<Navigate to="/dashboard" replace />} />
              <Route path="/dashboard" element={<DashboardPage />} />
              <Route path="/instruments" element={<InstrumentsPage />} />
              <Route path="/instruments/:id" element={<InstrumentAnalysisPage />} />
              <Route path="/portfolios" element={<PortfoliosPage />} />
              <Route path="/portfolios/:id" element={<PortfolioDetailPage />} />
              {canManageUsers && (
                <Route path="/administration" element={<AdministrationPage />} />
              )}
              <Route path="*" element={<Navigate to="/dashboard" replace />} />
            </Routes>
          </Content>

          <Footer style={{ textAlign: 'center', background: '#f5f7fa', paddingBlock: 12 }}>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Выпускная квалификационная работа · Финансовый университет при Правительстве
              Российской Федерации · Источники данных: Московская Биржа, Банк России
            </Typography.Text>
          </Footer>
        </Layout>
      </Layout>
    </Layout>
  )
}
