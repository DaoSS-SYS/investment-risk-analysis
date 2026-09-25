import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

/**
 * Конфигурация сборщика Vite.
 *
 * В среде разработки запросы к программному интерфейсу перенаправляются
 * на сервер приложения через прокси. Благодаря этому браузер обращается
 * к тому же адресу, с которого загружено клиентское приложение, и механизм
 * разделения ресурсов между источниками (CORS) не задействуется.
 *
 * Адрес сервера задаётся переменной окружения VITE_API_TARGET; при её
 * отсутствии используется адрес локального запуска.
 */
export default defineConfig(({ mode }) => {
  const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:5080'

  return {
    plugins: [react()],

    server: {
      // Приём соединений со всех сетевых интерфейсов: требуется для
      // демонстрации системы с другого компьютера локальной сети.
      host: '0.0.0.0',
      port: 5173,
      proxy: {
        '/api': { target: apiTarget, changeOrigin: true },
        '/health': { target: apiTarget, changeOrigin: true },
      },
    },

    build: {
      outDir: 'dist',
      sourcemap: mode !== 'production',

      // Разделение сборки на части. Библиотека построения диаграмм и
      // библиотека элементов интерфейса составляют основной объём и
      // изменяются редко, поэтому выносятся в отдельные файлы: при
      // обновлении приложения браузер повторно загружает только
      // изменившуюся часть, а первая загрузка распараллеливается.
      rolldownOptions: {
        output: {
          advancedChunks: {
            groups: [
              { name: 'echarts', test: /[\\/]node_modules[\\/](echarts|zrender)/ },
              { name: 'antd', test: /[\\/]node_modules[\\/](antd|@ant-design|rc-)/ },
              {
                name: 'vendor',
                test: /[\\/]node_modules[\\/](react|react-dom|react-router|@tanstack|axios|dayjs)/,
              },
            ],
          },
        },
      },
    },
  }
})
