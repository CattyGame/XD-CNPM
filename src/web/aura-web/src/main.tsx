
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import AppRouter from './routes/AppRouter'
import ForbiddenPage from './pages/ForbiddenPage'
import ServerErrorPage from './pages/ServerErrorPage'
import LoginPage from './pages/LoginPage'
import ProtectedRoute from './routes/ProtectedRoute'
import UploadPage from './pages/UploadPage'

async function startApp() {
  const enableMock =
    import.meta.env.DEV &&
    import.meta.env.VITE_ENABLE_MOCK === 'true'

  if (enableMock) {
    const { worker } = await import('./mocks/browser.ts')

    await worker.start({
  onUnhandledRequest: 'bypass',
  serviceWorker: {
    url: '/mockServiceWorker.js',
  },
})
  }

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <>
        {enableMock && (
          <div
            role="status"
            style={{
              background: '#fff3cd',
              padding: '8px',
              textAlign: 'center',
              color: '#664d03',
            }}
          >
            DEMO MODE — Dữ liệu giả lập
          </div>
        )}
        <BrowserRouter>
  <Routes>
  <Route path="/login" element={<LoginPage />} />
  <Route path="/403" element={<ForbiddenPage />} />
  <Route path="/500" element={<ServerErrorPage />} />
  <Route path="/router-demo/*" element={<AppRouter />} />

  <Route
    path="/upload"
    element={
      <ProtectedRoute>
        <UploadPage />
      </ProtectedRoute>
    }
  />

  <Route
    path="/*"
    element={
      <ProtectedRoute>
        <App />
      </ProtectedRoute>
    }
  />
</Routes>
</BrowserRouter>
      </>
    </StrictMode>,
  )
}

startApp()
