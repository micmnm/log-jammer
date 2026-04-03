import { useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Table from '@mui/material/Table';
import TableHead from '@mui/material/TableHead';
import TableBody from '@mui/material/TableBody';
import TableRow from '@mui/material/TableRow';
import TableCell from '@mui/material/TableCell';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Switch from '@mui/material/Switch';
import Chip from '@mui/material/Chip';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import DeleteIcon from '@mui/icons-material/Delete';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import CircularProgress from '@mui/material/CircularProgress';
import { useUsers, useUpdateUser, useDeleteUser } from '../api/hooks/useUsers';
import { useCreateInvite, useInvites } from '../api/hooks/useInvites';
import { useAuth } from '../api/hooks/useAuth';

export default function Users() {
  useAuth();
  const { data: users, isLoading } = useUsers();
  useInvites();
  const updateUser = useUpdateUser();
  const deleteUser = useDeleteUser();
  const createInvite = useCreateInvite();

  const [inviteDialogOpen, setInviteDialogOpen] = useState(false);
  const [grantCanInvite, setGrantCanInvite] = useState(false);
  const [copiedUrl, setCopiedUrl] = useState<string | null>(null);
  const [snackbar, setSnackbar] = useState<string | null>(null);

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  function handleCreateInvite() {
    createInvite.mutate(grantCanInvite, {
      onSuccess: (data) => {
        setInviteDialogOpen(false);
        setGrantCanInvite(false);
        if (data.inviteUrl) {
          void navigator.clipboard.writeText(data.inviteUrl);
          setCopiedUrl(data.inviteUrl);
          setSnackbar('Invite link copied to clipboard');
        }
      },
    });
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 600 }}>
          Users
        </Typography>
        <Button
          variant="contained"
          startIcon={<PersonAddIcon />}
          onClick={() => setInviteDialogOpen(true)}
        >
          Create Invite
        </Button>
      </Box>

      <Paper>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>User</TableCell>
              <TableCell>Role</TableCell>
              <TableCell>Can Invite</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Invited By</TableCell>
              <TableCell>Joined</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users?.map((u) => (
              <TableRow key={u.id}>
                <TableCell>
                  <Typography variant="body2" sx={{ fontWeight: 500 }}>
                    {u.displayName}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {u.username}
                  </Typography>
                </TableCell>
                <TableCell>
                  {u.isAdmin && <Chip label="Admin" size="small" color="primary" />}
                </TableCell>
                <TableCell>
                  <Switch
                    checked={u.canInvite}
                    onChange={(e) =>
                      updateUser.mutate({ id: u.id, canInvite: e.target.checked })
                    }
                    disabled={u.isAdmin}
                    size="small"
                  />
                </TableCell>
                <TableCell>
                  <Chip
                    label={u.isDisabled ? 'Disabled' : 'Active'}
                    size="small"
                    color={u.isDisabled ? 'error' : 'success'}
                    variant="outlined"
                  />
                </TableCell>
                <TableCell>
                  <Typography variant="body2" color="text.secondary">
                    {u.invitedBy ?? '—'}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Typography variant="body2" color="text.secondary">
                    {new Date(u.createdAt).toLocaleDateString()}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  {!u.isAdmin && (
                    <>
                      <Tooltip title={u.isDisabled ? 'Enable' : 'Disable'}>
                        <Button
                          size="small"
                          onClick={() =>
                            updateUser.mutate({ id: u.id, isDisabled: !u.isDisabled })
                          }
                        >
                          {u.isDisabled ? 'Enable' : 'Disable'}
                        </Button>
                      </Tooltip>
                      <Tooltip title="Delete">
                        <IconButton
                          size="small"
                          color="error"
                          onClick={() => {
                            if (confirm(`Delete user ${u.displayName}?`))
                              deleteUser.mutate(u.id);
                          }}
                        >
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>

      {/* Invite Dialog */}
      <Dialog open={inviteDialogOpen} onClose={() => setInviteDialogOpen(false)}>
        <DialogTitle>Create Invite</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Generate an invite link for a new user. The link expires in 24 hours.
          </Typography>
          <FormControlLabel
            control={
              <Checkbox
                checked={grantCanInvite}
                onChange={(e) => setGrantCanInvite(e.target.checked)}
              />
            }
            label="Allow this user to invite others"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setInviteDialogOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            onClick={handleCreateInvite}
            disabled={createInvite.isPending}
          >
            {createInvite.isPending ? 'Creating…' : 'Create & Copy Link'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Copied URL display */}
      {copiedUrl && (
        <Paper sx={{ mt: 2, p: 2, display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography variant="body2" sx={{ fontFamily: 'monospace', flex: 1, wordBreak: 'break-all' }}>
            {copiedUrl}
          </Typography>
          <IconButton
            size="small"
            onClick={() => {
              void navigator.clipboard.writeText(copiedUrl);
              setSnackbar('Copied!');
            }}
          >
            <ContentCopyIcon fontSize="small" />
          </IconButton>
        </Paper>
      )}

      <Snackbar
        open={!!snackbar}
        autoHideDuration={3000}
        onClose={() => setSnackbar(null)}
      >
        <Alert severity="success" onClose={() => setSnackbar(null)}>
          {snackbar}
        </Alert>
      </Snackbar>
    </Box>
  );
}
