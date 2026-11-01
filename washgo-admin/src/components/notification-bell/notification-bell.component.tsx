import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useSelector } from 'react-redux';
import {
  Badge,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  List,
  ListItemButton,
  Popover,
  Snackbar,
  Alert,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import NotificationsOutlinedIcon from '@mui/icons-material/NotificationsOutlined';
import CloseIcon from '@mui/icons-material/Close';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { RootState } from '../../redux/store.redux';
import {
  NOTIFICATION_HUB_URL,
  NotificationItem,
  deleteAllNotifications,
  deleteNotification,
  getNotifications,
  getUnreadCount,
  markAllNotificationsRead,
  markNotificationRead,
} from '../../apis/notification/notification.api';
import { getApiErrorMessage } from '../../common/utils/api-base-response.util';

const timeAgo = (seconds: number) => {
  const diff = Math.max(0, Date.now() / 1000 - seconds);
  if (diff < 60) return 'Vừa xong';
  if (diff < 3600) return `${Math.floor(diff / 60)} phút trước`;
  if (diff < 86400) return `${Math.floor(diff / 3600)} giờ trước`;
  return new Date(seconds * 1000).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });
};

// Order codes appear in the text as #WG...; the order list filters by that keyword.
const targetPath = (n: NotificationItem) => {
  if (n.linkType === 'incident') return '/incident';
  if (n.linkType === 'rating') return '/rating';
  if (n.linkType === 'order') {
    const code = n.content.match(/#(WG\d+)/)?.[1];
    return code ? `/wash-order?keyword=${code}&page=1` : '/wash-order';
  }
  return null;
};

export const NotificationBell: React.FC = () => {
  const navigate = useNavigate();
  const { accessToken } = useSelector((state: RootState) => state.account);
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [unread, setUnread] = useState(0);
  const [loading, setLoading] = useState(false);
  const [confirmClear, setConfirmClear] = useState(false);
  const [toast, setToast] = useState<{ message: string; severity: 'success' | 'error' | 'info' } | null>(null);

  const refreshCount = useCallback(async () => {
    try {
      const res = await getUnreadCount();
      setUnread(res.data ?? 0);
    } catch {
      // The badge is best-effort; the list call reports errors.
    }
  }, []);

  const loadList = useCallback(async () => {
    try {
      setLoading(true);
      const res = await getNotifications(1, 30);
      setItems(res.list || []);
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không tải được thông báo.'), severity: 'error' });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refreshCount();
  }, [refreshCount]);

  // Live updates: new notifications bump the badge, show a toast and join the open list.
  useEffect(() => {
    if (!accessToken) return;
    const connection = new HubConnectionBuilder()
      .withUrl(NOTIFICATION_HUB_URL, { accessTokenFactory: () => accessToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on('notification', (n: NotificationItem) => {
      setUnread((count) => count + 1);
      setItems((list) => [n, ...list.filter((x) => x.id !== n.id)]);
      setToast({ message: `${n.title}: ${n.content}`, severity: 'info' });
    });
    connection.start().catch(() => undefined);
    return () => {
      connection.stop();
    };
  }, [accessToken]);

  const openPanel = (event: React.MouseEvent<HTMLElement>) => {
    setAnchor(event.currentTarget);
    loadList();
  };

  const handleClick = async (n: NotificationItem) => {
    if (!n.isRead) {
      try {
        await markNotificationRead(n.id);
        setItems((list) => list.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
        setUnread((count) => Math.max(0, count - 1));
      } catch (err) {
        setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'), severity: 'error' });
      }
    }
    const path = targetPath(n);
    if (path) {
      setAnchor(null);
      navigate(path);
    }
  };

  const handleReadAll = async () => {
    try {
      const res = await markAllNotificationsRead();
      setItems((list) => list.map((x) => ({ ...x, isRead: true })));
      setUnread(0);
      setToast({ message: res.message || 'Đã đánh dấu tất cả là đã đọc.', severity: 'success' });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'), severity: 'error' });
    }
  };

  const handleDelete = async (event: React.MouseEvent, n: NotificationItem) => {
    event.stopPropagation();
    try {
      await deleteNotification(n.id);
      setItems((list) => list.filter((x) => x.id !== n.id));
      if (!n.isRead) setUnread((count) => Math.max(0, count - 1));
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'), severity: 'error' });
    }
  };

  const handleClearAll = async () => {
    setConfirmClear(false);
    try {
      await deleteAllNotifications();
      setItems([]);
      setUnread(0);
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'), severity: 'error' });
    }
  };

  return (
    <>
      <Tooltip title="Thông báo hệ thống">
        <IconButton size="small" onClick={openPanel}>
          <Badge badgeContent={unread} color="error" max={99}>
            <NotificationsOutlinedIcon />
          </Badge>
        </IconButton>
      </Tooltip>

      <Popover
        open={!!anchor}
        anchorEl={anchor}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: 380, maxHeight: 520, display: 'flex', flexDirection: 'column' } } }}
      >
        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 2, py: 1.25 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>Thông báo</Typography>
          <Stack direction="row" spacing={0.5}>
            <Button size="small" disabled={unread === 0} onClick={handleReadAll}>Đánh dấu tất cả đã đọc</Button>
            <Button size="small" color="error" disabled={items.length === 0} onClick={() => setConfirmClear(true)}>Xóa tất cả</Button>
          </Stack>
        </Stack>
        <Divider />
        <Box sx={{ overflowY: 'auto' }}>
          {loading ? (
            <Box sx={{ py: 4, textAlign: 'center' }}><CircularProgress size={24} /></Box>
          ) : items.length === 0 ? (
            <Typography variant="body2" color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>
              Bạn chưa có thông báo nào.
            </Typography>
          ) : (
            <List disablePadding>
              {items.map((n) => (
                <ListItemButton
                  key={n.id}
                  onClick={() => handleClick(n)}
                  sx={{ alignItems: 'flex-start', gap: 1, bgcolor: n.isRead ? 'transparent' : 'action.hover', borderBottom: '1px solid', borderColor: 'divider' }}
                >
                  <Box sx={{ width: 8, height: 8, mt: 0.9, borderRadius: '50%', flexShrink: 0, bgcolor: n.isRead ? 'transparent' : 'primary.main' }} />
                  <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                    <Typography variant="body2" sx={{ fontWeight: n.isRead ? 500 : 700 }}>{n.title}</Typography>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>{n.content}</Typography>
                    <Typography variant="caption" color="text.disabled">{timeAgo(n.createdOn)}</Typography>
                  </Box>
                  <IconButton size="small" onClick={(e) => handleDelete(e, n)} aria-label="Xóa thông báo">
                    <CloseIcon fontSize="inherit" />
                  </IconButton>
                </ListItemButton>
              ))}
            </List>
          )}
        </Box>
      </Popover>

      <Dialog open={confirmClear} onClose={() => setConfirmClear(false)}>
        <DialogTitle sx={{ fontWeight: 800 }}>Xóa tất cả thông báo?</DialogTitle>
        <DialogContent>
          <Typography variant="body2">Toàn bộ thông báo của bạn sẽ bị xóa.</Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmClear(false)}>Hủy</Button>
          <Button variant="contained" color="error" onClick={handleClearAll}>Xác nhận</Button>
        </DialogActions>
      </Dialog>

      <Snackbar open={!!toast} autoHideDuration={5000} onClose={() => setToast(null)} anchorOrigin={{ vertical: 'top', horizontal: 'right' }}>
        <Alert severity={toast?.severity ?? 'info'} onClose={() => setToast(null)} variant="filled" sx={{ maxWidth: 420 }}>
          {toast?.message}
        </Alert>
      </Snackbar>
    </>
  );
};
