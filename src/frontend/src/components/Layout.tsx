import { AppBar, Box, Drawer, List, ListItemButton, ListItemIcon, ListItemText, Toolbar, Typography, IconButton, Menu, MenuItem, Dialog, DialogTitle, DialogContent, DialogActions, TextField, Alert, Button } from '@mui/material'
import DashboardIcon from '@mui/icons-material/Dashboard'
import CertIcon from '@mui/icons-material/Verified'
import ConfigIcon from '@mui/icons-material/Settings'
import AuditIcon from '@mui/icons-material/History'
import LogoutIcon from '@mui/icons-material/Logout'
import AccountCircle from '@mui/icons-material/AccountCircle'
import DownloadIcon from '@mui/icons-material/Download'
import { useNavigate, useLocation } from 'react-router-dom'
import { ReactNode, useState } from 'react'
import api from '../api/client'

const drawerWidth = 240

const menuItems = [
  { text: 'Dashboard', icon: <DashboardIcon />, path: '/' },
  { text: 'Certificates', icon: <CertIcon />, path: '/certificates' },
  { text: 'Configuration', icon: <ConfigIcon />, path: '/configuration' },
  { text: 'Audit Log', icon: <AuditIcon />, path: '/audit' },
]

export default function Layout({ children, onLogout }: { children: ReactNode; onLogout: () => void }) {
  const navigate = useNavigate()
  const location = useLocation()
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null)
  const [changePasswordOpen, setChangePasswordOpen] = useState(false)

  const downloadCaChain = async () => {
    try {
      const res = await api.get('/ca/chain', { responseType: 'blob' })
      const url = window.URL.createObjectURL(res.data)
      const a = document.createElement('a')
      a.href = url
      a.download = 'ca-chain.pem'
      a.click()
      window.URL.revokeObjectURL(url)
    } catch {
      // ignore download errors
    }
  }

  return (
    <Box sx={{ display: 'flex' }}>
      <AppBar position="fixed" sx={{ zIndex: theme => theme.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
            MiniPKI Certificate Authority
          </Typography>
          <Button color="inherit" startIcon={<DownloadIcon />} onClick={() => downloadCaChain()}>
            CA Chain
          </Button>
          <IconButton color="inherit" onClick={e => setAnchorEl(e.currentTarget)}>
            <AccountCircle />
          </IconButton>
          <Menu anchorEl={anchorEl} open={!!anchorEl} onClose={() => setAnchorEl(null)}>
            <MenuItem onClick={() => { setAnchorEl(null); setChangePasswordOpen(true) }}>
              Change Password
            </MenuItem>
            <MenuItem onClick={() => { setAnchorEl(null); onLogout() }}>
              <LogoutIcon sx={{ mr: 1 }} /> Logout
            </MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>
      <Drawer
        variant="permanent"
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          '& .MuiDrawer-paper': { width: drawerWidth, boxSizing: 'border-box' },
        }}
      >
        <Toolbar />
        <List>
          {menuItems.map(item => (
            <ListItemButton
              key={item.text}
              onClick={() => navigate(item.path)}
              selected={location.pathname === item.path}
            >
              <ListItemIcon>{item.icon}</ListItemIcon>
              <ListItemText primary={item.text} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>
      <Box component="main" sx={{ flexGrow: 1, p: 3 }}>
        <Toolbar />
        {children}
      </Box>
      <ChangePasswordDialog open={changePasswordOpen} onClose={() => setChangePasswordOpen(false)} />
    </Box>
  )
}

function ChangePasswordDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')
  const [success, setSuccess] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')

    if (newPassword.length < 8) {
      setError('New password must be at least 8 characters')
      return
    }

    if (newPassword !== confirmPassword) {
      setError('New passwords do not match')
      return
    }

    try {
      await api.post('/auth/change-password', {
        currentPassword,
        newPassword,
      })
      setSuccess(true)
      setTimeout(() => {
        setSuccess(false)
        onClose()
        setCurrentPassword('')
        setNewPassword('')
        setConfirmPassword('')
      }, 1500)
    } catch (err: any) {
      setError(err.response?.data?.error || 'Failed to change password')
    }
  }

  const handleClose = () => {
    onClose()
    setCurrentPassword('')
    setNewPassword('')
    setConfirmPassword('')
    setError('')
  }

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Change Password</DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {success && <Alert severity="success" sx={{ mb: 2 }}>Password changed successfully!</Alert>}
        <Box component="form" onSubmit={handleSubmit} sx={{ mt: 1 }}>
          <TextField
            fullWidth
            type="password"
            label="Current Password"
            value={currentPassword}
            onChange={e => setCurrentPassword(e.target.value)}
            margin="normal"
            required
          />
          <TextField
            fullWidth
            type="password"
            label="New Password"
            value={newPassword}
            onChange={e => setNewPassword(e.target.value)}
            margin="normal"
            required
            helperText="Minimum 8 characters"
          />
          <TextField
            fullWidth
            type="password"
            label="Confirm New Password"
            value={confirmPassword}
            onChange={e => setConfirmPassword(e.target.value)}
            margin="normal"
            required
          />
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Cancel</Button>
        <Button variant="contained" type="submit" onClick={handleSubmit}>Change Password</Button>
      </DialogActions>
    </Dialog>
  )
}
