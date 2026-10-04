import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, Typography, Box } from '@mui/material'
import { useEffect, useState } from 'react'
import api from '../api/client'

interface AuditEntry {
  timestamp: string
  action: string
  details: string
  actor?: string
}

export default function Audit() {
  const [entries, setEntries] = useState<AuditEntry[]>([])

  useEffect(() => {
    api.get('/audit').then(res => setEntries(res.data))
  }, [])

  return (
    <Box>
      <Typography variant="h4" gutterBottom>Audit Log</Typography>
      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Timestamp</TableCell>
              <TableCell>Action</TableCell>
              <TableCell>Details</TableCell>
              <TableCell>Actor</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {entries.map((entry, i) => (
              <TableRow key={i}>
                <TableCell>{new Date(entry.timestamp).toLocaleString()}</TableCell>
                <TableCell sx={{ fontWeight: 'bold' }}>{entry.action}</TableCell>
                <TableCell>{entry.details}</TableCell>
                <TableCell>{entry.actor || '-'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  )
}
