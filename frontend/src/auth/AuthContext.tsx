import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'

import { api, setAuthTokenProvider, setUnauthorizedHandler } from '../api/client'
import type { LoginResult, UserView } from '../api/types'

/**
 * Роли системы. Состав отражает распределение обязанностей в организации.
 */
export const Roles = {
  Administrator: 'Administrator',
  Analyst: 'Analyst',
  Viewer: 'Viewer',
} as const

interface AuthState {
  user: UserView | null
  token: string | null
  isAuthenticated: boolean
  isRestoring: boolean
  login: (userName: string, password: string) => Promise<void>
  logout: () => void
  isInRole: (...roles: string[]) => boolean
  canManagePortfolios: boolean
  canManageReferenceData: boolean
  canManageUsers: boolean
}

const AuthContext = createContext<AuthState | null>(null)

const StorageKey = 'riskanalysis.session'

interface StoredSession {
  token: string
  expiresAt: string
  user: UserView
}

/**
 * Читает сохранённый сеанс. Сеанс с истёкшим сроком действия маркера
 * не восстанавливается: обращение с таким маркером было бы отклонено
 * сервером, и пользователь увидел бы ошибку вместо страницы входа.
 */
function readStoredSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(StorageKey)

    if (!raw) {
      return null
    }

    const session = JSON.parse(raw) as StoredSession

    if (new Date(session.expiresAt).getTime() <= Date.now()) {
      localStorage.removeItem(StorageKey)
      return null
    }

    return session
  } catch {
    // Хранилище может быть недоступно либо содержать запись
    // несовместимого вида: в обоих случаях сеанс считается отсутствующим.
    return null
  }
}

/**
 * Состояние проверки подлинности.
 *
 * Маркер доступа сохраняется в хранилище браузера, чтобы обновление
 * страницы не требовало повторного входа. Срок действия маркера
 * проверяется при восстановлении сеанса.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<StoredSession | null>(null)
  const [isRestoring, setIsRestoring] = useState(true)

  useEffect(() => {
    setSession(readStoredSession())
    setIsRestoring(false)
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem(StorageKey)
    setSession(null)
  }, [])

  // Клиент программного интерфейса получает маркер через функцию, а не
  // через замыкание: это позволяет ему обращаться к текущему значению,
  // не завися от состояния компонентов.
  useEffect(() => {
    setAuthTokenProvider(() => session?.token ?? null)
    setUnauthorizedHandler(logout)
  }, [session, logout])

  const login = useCallback(async (userName: string, password: string) => {
    const result: LoginResult = await api.auth.login(userName, password)

    const stored: StoredSession = {
      token: result.token,
      expiresAt: result.expiresAt,
      user: result.user,
    }

    try {
      localStorage.setItem(StorageKey, JSON.stringify(stored))
    } catch {
      // Невозможность сохранить сеанс не препятствует работе:
      // он будет утрачен при обновлении страницы.
    }

    setSession(stored)
  }, [])

  const value = useMemo<AuthState>(() => {
    const roles = session?.user.roles ?? []

    const isInRole = (...required: string[]) =>
      required.some((role) => roles.includes(role))

    return {
      user: session?.user ?? null,
      token: session?.token ?? null,
      isAuthenticated: session !== null,
      isRestoring,
      login,
      logout,
      isInRole,
      canManagePortfolios: isInRole(Roles.Analyst, Roles.Administrator),
      canManageReferenceData: isInRole(Roles.Administrator),
      canManageUsers: isInRole(Roles.Administrator),
    }
  }, [session, isRestoring, login, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

/** Возвращает состояние проверки подлинности. */
export function useAuth(): AuthState {
  const context = useContext(AuthContext)

  if (context === null) {
    throw new Error('useAuth должен вызываться внутри AuthProvider')
  }

  return context
}

/** Наименование роли на русском языке. */
export function describeRole(role: string): string {
  const names: Record<string, string> = {
    Administrator: 'Администратор',
    Analyst: 'Аналитик',
    Viewer: 'Наблюдатель',
  }

  return names[role] ?? role
}
