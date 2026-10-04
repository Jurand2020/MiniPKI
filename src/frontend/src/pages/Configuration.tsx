import {
  Card, CardContent, Grid, TextField, Button, Typography, Alert, Box,
  MenuItem, Dialog, DialogTitle, DialogContent, DialogActions,
  Backdrop, CircularProgress
} from '@mui/material'
import { useEffect, useState } from 'react'
import api from '../api/client'

interface Config {
  defaultDomain: string
  crlUrl: string
  defaultValidityDays: number
  algorithm: string
  rsaKeySize: number
  organization: string
  organizationalUnit: string
  country: string
  state: string
  locality: string
}

export default function Configuration() {
  const [config, setConfig] = useState<Config | null>(null)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState('')
  const [resetOpen, setResetOpen] = useState(false)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    api.get('/configuration').then(res => setConfig(res.data))
  }, [])

  if (!config) return <Typography>Loading...</Typography>

  const handleSave = async () => {
    setError('')
    try {
      await api.put('/configuration', config)
      setSaved(true)
      setTimeout(() => setSaved(false), 3000)
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to save configuration')
    }
  }

  const handleResetCa = async () => {
    setLoading(true)
    try {
      await api.post('/configuration/reset-ca')
      setResetOpen(false)
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to reset CA')
    } finally {
      setLoading(false)
    }
  }

  return (
    <Box>
      <Typography variant="h4" gutterBottom>Configuration</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {saved && <Alert severity="success" sx={{ mb: 2 }}>Configuration saved successfully!</Alert>}
      <Card>
        <CardContent>
          <Grid container spacing={2}>
            <Grid item xs={12} md={6}>
              <TextField fullWidth label="Default Domain" value={config.defaultDomain}
                onChange={e => setConfig({ ...config, defaultDomain: e.target.value })} />
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField fullWidth label="CRL URL" value={config.crlUrl}
                onChange={e => setConfig({ ...config, crlUrl: e.target.value })}
                helperText="Use {API_CRL} placeholder for the CRL endpoint, e.g. http://my.host.local:11111{API_CRL}" />
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField fullWidth select label="Default Algorithm" value={config.algorithm}
                onChange={e => setConfig({ ...config, algorithm: e.target.value })}>
                <MenuItem value="RSA">RSA</MenuItem>
                <MenuItem value="ECDSA">ECDSA (P-256)</MenuItem>
              </TextField>
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField fullWidth select label="RSA Key Size" value={config.rsaKeySize}
                onChange={e => setConfig({ ...config, rsaKeySize: Number(e.target.value) })}>
                <MenuItem value={2048}>2048</MenuItem>
                <MenuItem value={3072}>3072</MenuItem>
                <MenuItem value={4096}>4096</MenuItem>
              </TextField>
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField fullWidth type="number" label="Default Validity (days)" value={config.defaultValidityDays}
                onChange={e => setConfig({ ...config, defaultValidityDays: Number(e.target.value) })} />
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField fullWidth label="Organization" value={config.organization}
                onChange={e => setConfig({ ...config, organization: e.target.value })} />
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField fullWidth label="Organizational Unit" value={config.organizationalUnit}
                onChange={e => setConfig({ ...config, organizationalUnit: e.target.value })} />
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField fullWidth label="Country" value={config.country}
                onChange={e => setConfig({ ...config, country: e.target.value })} />
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField fullWidth label="State" value={config.state}
                onChange={e => setConfig({ ...config, state: e.target.value })} />
            </Grid>
            <Grid item xs={12} md={6}>
              <TextField fullWidth label="Locality" value={config.locality}
                onChange={e => setConfig({ ...config, locality: e.target.value })} />
            </Grid>
          </Grid>
          <Box sx={{ display: 'flex', gap: 2, mt: 3 }}>
            <Button variant="contained" onClick={handleSave}>Save Configuration</Button>
            <Button variant="outlined" color="error" onClick={() => setResetOpen(true)}>
              Reset CA
            </Button>
          </Box>
        </CardContent>
      </Card>

      <Dialog open={resetOpen} onClose={() => setResetOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Reset Certificate Authority</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mt: 1, mb: 2 }}>
            This action cannot be undone!
          </Alert>
          <Typography paragraph>
            Resetting the CA will:
          </Typography>
          <Typography component="div" paragraph>
            <ul>
              <li>Delete the Root CA certificate and key</li>
              <li>Delete the Intermediate CA certificate and key</li>
              <li>Delete all issued certificates and their private keys</li>
              <li>Delete all revoked certificates</li>
              <li>Delete the current CRL</li>
            </ul>
          </Typography>
          <Typography paragraph>
            New Root CA and Intermediate CA certificates will be generated using the current configuration settings.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetOpen(false)}>Cancel</Button>
          <Button color="error" variant="contained" onClick={handleResetCa}>
            Reset CA
          </Button>
        </DialogActions>
      </Dialog>

      <Backdrop open={loading} sx={{ color: '#fff', zIndex: theme => theme.zIndex.drawer + 1 }}>
        <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2 }}>
          <CircularProgress color="inherit" />
          <Typography variant="h6">Please wait...</Typography>
          <Typography variant="body2" color="textSecondary">Resetting CA and generating new certificates</Typography>
        </Box>
      </Backdrop>
    </Box>
  )
}
