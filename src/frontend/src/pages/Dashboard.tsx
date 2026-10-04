import { Card, CardContent, Grid, Typography, Box } from '@mui/material'
import { useEffect, useState } from 'react'
import api from '../api/client'

interface Stats {
  total: number
  active: number
  revoked: number
  expiringSoon: number
  latestIssued: Array<{
    serialNumber: string
    commonName: string
    createdAt: string
    status: string
  }>
}

export default function Dashboard() {
  const [stats, setStats] = useState<Stats | null>(null)

  useEffect(() => {
    api.get('/dashboard/stats').then(res => setStats(res.data))
  }, [])

  if (!stats) return <Typography>Loading...</Typography>

  const cards = [
    { label: 'Total Certificates', value: stats.total, color: '#1976d2' },
    { label: 'Active', value: stats.active, color: '#2e7d32' },
    { label: 'Revoked', value: stats.revoked, color: '#d32f2f' },
    { label: 'Expiring Soon', value: stats.expiringSoon, color: '#ed6c02' },
  ]

  return (
    <Box>
      <Typography variant="h4" gutterBottom>Dashboard</Typography>
      <Grid container spacing={3} sx={{ mb: 4 }}>
        {cards.map(card => (
          <Grid item xs={12} sm={6} md={3} key={card.label}>
            <Card>
              <CardContent>
                <Typography color="textSecondary" gutterBottom>{card.label}</Typography>
                <Typography variant="h3" sx={{ color: card.color }}>{card.value}</Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
      <Typography variant="h6" gutterBottom>Latest Issued Certificates</Typography>
      <Card>
        <CardContent>
          {stats.latestIssued.length === 0 ? (
            <Typography color="textSecondary">No certificates issued yet.</Typography>
          ) : (
            stats.latestIssued.map(cert => (
              <Box key={cert.serialNumber} sx={{ py: 1, display: 'flex', justifyContent: 'space-between' }}>
                <Typography>{cert.commonName}</Typography>
                <Typography color="textSecondary">{new Date(cert.createdAt).toLocaleDateString()}</Typography>
              </Box>
            ))
          )}
        </CardContent>
      </Card>
    </Box>
  )
}
