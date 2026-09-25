import { Layout, Menu, Typography } from 'antd'
import {
  BarChartOutlined,
  DatabaseOutlined,
  DashboardOutlined,
  FundOutlined,
} from '@ant-design/icons'
import { Link, Navigate, Route, Routes, useLocation } from 'react-router-dom'

import DashboardPage from './pages/DashboardPage'
import InstrumentsPage from './pages/InstrumentsPage'
import InstrumentAnalysisPage from './pages/InstrumentAnalysisPage'
import PortfoliosPage from './pages/PortfoliosPage'
import PortfolioDetailPage from './pages/PortfolioDetailPage'

const { Header, Sider, Content, Footer } = Layout

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
]

/**
 * Корневой компонент клиентского приложения: общая разметка,
 * навигация и таблица маршрутов.
 */
export default function App() {
  const location = useLocation()

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
        <Typography.Title level={5} style={{ margin: 0, color: '#f8fafc' }}>
          Количественный анализ рисков инвестиционной деятельности
        </Typography.Title>
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
