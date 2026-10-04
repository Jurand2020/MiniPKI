import { Routes, Route, Navigate } from 'react-router-dom'
import { useEffect, useState } from 'react'
import api from './api/client'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Certificates from './pages/Certificates'
import Configuration from './pages/Configuration'
import Audit from './pages/Audit'
import Layout from './components/Layout'

interface AuthStatus {
  authenticated: boolean
  firstRun: boolean
}

export default function App() {
  const [authStatus, setAuthStatus] = useState<AuthStatus | null>(null)

  const refreshStatus = () => {
    api.get('/auth/status')
      .then(res => setAuthStatus(res.data))
      .catch(() => setAuthStatus({ authenticated: false, firstRun: false }))
  }

  useEffect(() => { refreshStatus() }, [])

  if (!authStatus) return null

  if (authStatus.firstRun) {
    return <Login firstRun onSuccess={refreshStatus} />
  }

  if (!authStatus.authenticated) {
    return (
      <Routes>
        <Route path="/login" element={<Login onSuccess={refreshStatus} />} />
        <Route path="*" element={<Navigate to="/login" />} />
      </Routes>
    )
  }

  return (
    <Layout onLogout={async () => {
      await api.post('/auth/logout')
      refreshStatus()
    }}>
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/certificates" element={<Certificates />} />
        <Route path="/configuration" element={<Configuration />} />
        <Route path="/audit" element={<Audit />} />
        <Route path="*" element={<Navigate to="/" />} />
      </Routes>
    </Layout>
  )
}
