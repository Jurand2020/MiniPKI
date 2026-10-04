import {
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
  Button, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Typography, Box, Alert, IconButton, Menu, MenuItem, Chip,
  Backdrop, CircularProgress
} from '@mui/material'
import MoreVertIcon from '@mui/icons-material/MoreVert'
import DownloadIcon from '@mui/icons-material/Download'
import UploadFileIcon from '@mui/icons-material/UploadFile'
import { useEffect, useState } from 'react'
import api from '../api/client'

interface Cert {
  serialNumber: string
  commonName: string
  sanEntries: string[]
  keyAlgorithm: string
  keySize: number
  createdAt: string
  expiresAt: string
  status: string
  revokedAt?: string
  revocationReason?: string
}

export default function Certificates() {
  const [certs, setCerts] = useState<Cert[]>([])
  const [issueOpen, setIssueOpen] = useState(false)
  const [csrOpen, setCsrOpen] = useState(false)
  const [revokeTarget, setRevokeTarget] = useState<Cert | null>(null)
  const [menuAnchor, setMenuAnchor] = useState<{ el: HTMLElement; cert: Cert } | null>(null)
  const [p12Dialog, setP12Dialog] = useState<Cert | null>(null)
  const [loading, setLoading] = useState(false)

  const load = () => api.get('/certificates').then(res => setCerts(res.data))

  useEffect(() => { load() }, [])

  const downloadFile = (serial: string, endpoint: string, filename: string) => {
    api.get(`/certificates/${serial}/${endpoint}`, { responseType: 'blob' })
      .then(res => {
        const url = window.URL.createObjectURL(res.data)
        const a = document.createElement('a')
        a.href = url
        a.download = filename
        a.click()
        window.URL.revokeObjectURL(url)
      })
  }

  const statusColor = (status: string): "success" | "error" | "warning" => {
    if (status === 'Revoked') return 'error'
    if (status === 'Expired') return 'warning'
    return 'success'
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h4">Certificates</Typography>
        <Box>
          <Button variant="outlined" startIcon={<UploadFileIcon />} sx={{ mr: 1 }} onClick={() => setCsrOpen(true)}>
            Issue from CSR
          </Button>
          <Button variant="contained" onClick={() => setIssueOpen(true)}>
            Issue Certificate
          </Button>
        </Box>
      </Box>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Common Name</TableCell>
              <TableCell>Serial</TableCell>
              <TableCell>Algorithm</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Expires</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {certs.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} align="center">
                  <Typography color="textSecondary" sx={{ py: 3 }}>
                    No certificates issued yet.
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              certs.map(cert => (
                <TableRow key={cert.serialNumber} hover>
                  <TableCell>{cert.commonName}</TableCell>
                  <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.85em' }}>
                    {cert.serialNumber.substring(0, 16)}...
                  </TableCell>
                  <TableCell>{cert.keyAlgorithm} {cert.keySize}</TableCell>
                  <TableCell>
                    <Chip label={cert.status} color={statusColor(cert.status)} size="small" />
                  </TableCell>
                  <TableCell>{new Date(cert.expiresAt).toLocaleDateString()}</TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={e => setMenuAnchor({ el: e.currentTarget, cert })}>
                      <MoreVertIcon />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Menu
        anchorEl={menuAnchor?.el}
        open={!!menuAnchor}
        onClose={() => setMenuAnchor(null)}
      >
        <MenuItem onClick={() => {
          const c = menuAnchor!.cert
          setMenuAnchor(null)
          downloadFile(c.serialNumber, 'download', `${c.commonName}.pem`)
        }}>
          <DownloadIcon sx={{ mr: 1 }} fontSize="small" /> Download PEM
        </MenuItem>
        <MenuItem onClick={() => {
          const c = menuAnchor!.cert
          setMenuAnchor(null)
          downloadFile(c.serialNumber, 'bundle', `${c.commonName}-bundle.pem`)
        }}>
          <DownloadIcon sx={{ mr: 1 }} fontSize="small" /> Download Bundle (Chain)
        </MenuItem>
        <MenuItem onClick={() => {
          const c = menuAnchor!.cert
          setMenuAnchor(null)
          downloadFile(c.serialNumber, 'key', `${c.commonName}.key`)
        }}>
          <DownloadIcon sx={{ mr: 1 }} fontSize="small" /> Download Key
        </MenuItem>
        <MenuItem onClick={() => {
          const c = menuAnchor!.cert
          setMenuAnchor(null)
          setP12Dialog(c)
        }}>
          <DownloadIcon sx={{ mr: 1 }} fontSize="small" /> Download P12
        </MenuItem>
        {menuAnchor?.cert.status === 'Valid' && (
          <MenuItem onClick={() => {
            const c = menuAnchor!.cert
            setMenuAnchor(null)
            setRevokeTarget(c)
          }} sx={{ color: 'error.main' }}>
            <MoreVertIcon sx={{ mr: 1 }} fontSize="small" /> Revoke
          </MenuItem>
        )}
      </Menu>

      <IssueDialog open={issueOpen} onClose={() => setIssueOpen(false)} onIssued={load} setLoading={setLoading} />
      <CsrDialog open={csrOpen} onClose={() => setCsrOpen(false)} onIssued={load} setLoading={setLoading} />
      <RevokeDialog cert={revokeTarget} onClose={() => setRevokeTarget(null)} onRevoked={load} />
      <P12PasswordDialog cert={p12Dialog} onClose={() => setP12Dialog(null)} />
      <Backdrop open={loading} sx={{ color: '#fff', zIndex: (theme) => theme.zIndex.modal + 1 }}>
        <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2 }}>
          <CircularProgress color="inherit" />
          <Typography variant="h6">Please wait...</Typography>
          <Typography variant="body2" color="textSecondary">Issuing certificate may take a moment</Typography>
        </Box>
      </Backdrop>
    </Box>
  )
}

function IssueDialog({ open, onClose, onIssued, setLoading }: {
  open: boolean
  onClose: () => void
  onIssued: () => void
  setLoading: (loading: boolean) => void
}) {
  const [commonName, setCommonName] = useState('')
  const [sans, setSans] = useState('')
  const [algorithm, setAlgorithm] = useState('RSA')
  const [keySize, setKeySize] = useState(4096)
  const [validityDays, setValidityDays] = useState(825)
  const [error, setError] = useState('')

  const handleIssue = async () => {
    setError('')
    setLoading(true)
    try {
      await api.post('/certificates', {
        commonName,
        sanEntries: sans.split(',').map(s => s.trim()).filter(Boolean),
        keyAlgorithm: algorithm,
        keySize: algorithm === 'RSA' ? keySize : 256,
        validityDays,
      })
      onIssued()
      handleClose()
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to issue certificate')
    } finally {
      setLoading(false)
    }
  }

  const handleClose = () => {
    setCommonName('')
    setSans('')
    setAlgorithm('RSA')
    setKeySize(4096)
    setValidityDays(825)
    setError('')
    onClose()
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Issue New Certificate</DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Box sx={{ mt: 1 }}>
          <TextField fullWidth label="Common Name" value={commonName}
            onChange={e => setCommonName(e.target.value)} margin="normal" required
            placeholder="e.g. web.example.com" />
          <TextField fullWidth label="SAN Entries (comma-separated)" value={sans}
            onChange={e => setSans(e.target.value)} margin="normal"
            helperText="e.g. example.com, www.example.com" />
          <TextField fullWidth select label="Key Algorithm" value={algorithm}
            onChange={e => setAlgorithm(e.target.value)} margin="normal">
            <MenuItem value="RSA">RSA</MenuItem>
            <MenuItem value="ECDSA">ECDSA (P-256)</MenuItem>
          </TextField>
          {algorithm === 'RSA' && (
            <TextField fullWidth select label="RSA Key Size" value={keySize}
              onChange={e => setKeySize(Number(e.target.value))} margin="normal">
              <MenuItem value={2048}>2048</MenuItem>
              <MenuItem value={3072}>3072</MenuItem>
              <MenuItem value={4096}>4096</MenuItem>
            </TextField>
          )}
          <TextField fullWidth type="number" label="Validity (days)" value={validityDays}
            onChange={e => setValidityDays(Number(e.target.value))} margin="normal" />
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Cancel</Button>
        <Button variant="contained" onClick={handleIssue}>Issue</Button>
      </DialogActions>
    </Dialog>
  )
}

function CsrDialog({ open, onClose, onIssued, setLoading }: {
  open: boolean
  onClose: () => void
  onIssued: () => void
  setLoading: (loading: boolean) => void
}) {
  const [csrPem, setCsrPem] = useState('')
  const [validityDays, setValidityDays] = useState(825)
  const [error, setError] = useState('')

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = ev => setCsrPem(ev.target?.result as string)
    reader.readAsText(file)
  }

  const handleIssue = async () => {
    setError('')
    setLoading(true)
    try {
      await api.post('/certificates/csr', { csrPem, validityDays })
      onIssued()
      handleClose()
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to issue certificate from CSR')
    } finally {
      setLoading(false)
    }
  }

  const handleClose = () => {
    setCsrPem('')
    setValidityDays(825)
    setError('')
    onClose()
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Issue Certificate from CSR</DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Box sx={{ mt: 1 }}>
          <Button variant="outlined" component="label" startIcon={<UploadFileIcon />} fullWidth sx={{ mb: 2 }}>
            Upload CSR File (.pem, .csr)
            <input type="file" hidden accept=".pem,.csr,.txt" onChange={handleFileUpload} />
          </Button>
          {csrPem && (
            <TextField fullWidth multiline rows={6} label="CSR PEM Content" value={csrPem}
              onChange={e => setCsrPem(e.target.value)} margin="normal"
              sx={{ fontFamily: 'monospace' }} />
          )}
          <TextField fullWidth type="number" label="Validity (days)" value={validityDays}
            onChange={e => setValidityDays(Number(e.target.value))} margin="normal" />
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Cancel</Button>
        <Button variant="contained" onClick={handleIssue} disabled={!csrPem}>Issue from CSR</Button>
      </DialogActions>
    </Dialog>
  )
}

function RevokeDialog({ cert, onClose, onRevoked }: { cert: Cert | null; onClose: () => void; onRevoked: () => void }) {
  const [reason, setReason] = useState('keyCompromise')
  const [error, setError] = useState('')

  const handleRevoke = async () => {
    if (!cert) return
    setError('')
    try {
      await api.post(`/certificates/${cert.serialNumber}/revoke`, { reason })
      onRevoked()
      onClose()
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to revoke certificate')
    }
  }

  return (
    <Dialog open={!!cert} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Revoke Certificate</DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Typography sx={{ mb: 2 }}>Revoke <strong>{cert?.commonName}</strong>?</Typography>
        <TextField fullWidth select label="Reason" value={reason}
          onChange={e => setReason(e.target.value)} margin="normal">
          <MenuItem value="keyCompromise">Key Compromise</MenuItem>
          <MenuItem value="caCompromise">CA Compromise</MenuItem>
          <MenuItem value="affiliationChanged">Affiliation Changed</MenuItem>
          <MenuItem value="superseded">Superseded</MenuItem>
          <MenuItem value="cessationOfOperation">Cessation of Operation</MenuItem>
          <MenuItem value="certificateHold">Certificate Hold</MenuItem>
          <MenuItem value="unspecified">Unspecified</MenuItem>
        </TextField>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button color="error" variant="contained" onClick={handleRevoke}>Revoke</Button>
      </DialogActions>
    </Dialog>
  )
}

function P12PasswordDialog({ cert, onClose }: {
  cert: Cert | null
  onClose: () => void
}) {
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  const handleDownload = async () => {
    if (!cert) return
    if (password.length < 4) {
      setError('Password must be at least 4 characters')
      return
    }
    setError('')
    try {
      const res = await api.get(`/certificates/${cert.serialNumber}/p12?password=${encodeURIComponent(password)}`, {
        responseType: 'blob'
      })
      const url = window.URL.createObjectURL(res.data)
      const a = document.createElement('a')
      a.href = url
      a.download = `${cert.commonName}.p12`
      a.click()
      window.URL.revokeObjectURL(url)
      handleClose()
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to download P12')
    }
  }

  const handleClose = () => {
    setPassword('')
    setError('')
    onClose()
  }

  return (
    <Dialog open={!!cert} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Download P12 File</DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Typography sx={{ mb: 2 }}>
          The P12 file will contain the certificate, private key, and CA chain.
        </Typography>
        <TextField fullWidth type="password" label="P12 Password" value={password}
          onChange={e => setPassword(e.target.value)} margin="normal" required
          helperText="This password protects the P12 file" />
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Cancel</Button>
        <Button variant="contained" onClick={handleDownload}>Download P12</Button>
      </DialogActions>
    </Dialog>
  )
}
