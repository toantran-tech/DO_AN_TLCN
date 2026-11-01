import React, { useEffect, useState, useCallback } from 'react';
import {
  Box,
  Typography,
  Button,
  Stack,
  Chip,
  TextField,
  InputAdornment,
  MenuItem,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  CircularProgress,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Grid,
  Alert,
  Snackbar,
  Rating,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import RefreshIcon from '@mui/icons-material/Refresh';
import { filterRatings, setRatingVisibility, toImageUrl } from '../../apis/feedback/feedback.api';
import { RatingItem } from '../../apis/feedback/feedback.interface';
import { getApiErrorMessage } from '../../common/utils/api-base-response.util';

const formatUnix = (seconds: number) => new Date(seconds * 1000).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });

const CRITERIA: { key: keyof RatingItem; label: string }[] = [
  { key: 'qualityStars', label: 'Chất lượng giặt' },
  { key: 'speedStars', label: 'Tốc độ xử lý' },
  { key: 'attitudeStars', label: 'Thái độ phục vụ' },
  { key: 'packagingStars', label: 'Đóng gói' },
];

export const RatingScreen: React.FC = () => {
  const [data, setData] = useState<{ list: RatingItem[]; total: number }>({ list: [], total: 0 });
  const [loading, setLoading] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [stars, setStars] = useState<number | ''>('');
  const [visibility, setVisibility] = useState<'' | 'visible' | 'hidden'>('');
  const [selected, setSelected] = useState<RatingItem | null>(null);
  const [hideReason, setHideReason] = useState('');
  const [dialogError, setDialogError] = useState('');
  const [toast, setToast] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const res = await filterRatings({
        page: 1,
        take: 100,
        keyword,
        stars: stars === '' ? undefined : stars,
        isHidden: visibility === '' ? undefined : visibility === 'hidden',
      });
      setData({ list: res.list || [], total: res.total || 0 });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không tải được danh sách đánh giá.'), severity: 'error' });
    } finally {
      setLoading(false);
    }
  }, [keyword, stars, visibility]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleVisibility = async (hide: boolean) => {
    if (!selected) return;
    try {
      const res = await setRatingVisibility(selected.id, hide, hide ? hideReason : undefined);
      setSelected(res.data);
      setDialogError('');
      setToast({ message: res.message || 'Cập nhật thành công.', severity: 'success' });
      fetchData();
    } catch (err) {
      setDialogError(getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'));
    }
  };

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Box sx={{ mb: 2.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
            Quản Lý Đánh Giá
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Theo dõi đánh giá của khách hàng và ẩn nội dung không phù hợp
          </Typography>
        </Box>
        <Button variant="outlined" startIcon={<RefreshIcon />} onClick={fetchData}>
          Làm mới
        </Button>
      </Box>

      <Box sx={{ mb: 2, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <TextField
          size="small"
          placeholder="Mã đơn, khách hàng hoặc nhận xét..."
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          sx={{ width: 320 }}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" />
              </InputAdornment>
            ),
          }}
        />
        <TextField
          select
          size="small"
          label="Số sao"
          value={stars}
          onChange={(e) => setStars(e.target.value === '' ? '' : Number(e.target.value))}
          sx={{ width: 140 }}
        >
          <MenuItem value="">Tất cả</MenuItem>
          {[5, 4, 3, 2, 1].map((n) => (
            <MenuItem key={n} value={n}>{n} sao</MenuItem>
          ))}
        </TextField>
        <TextField
          select
          size="small"
          label="Hiển thị"
          value={visibility}
          onChange={(e) => setVisibility(e.target.value as '' | 'visible' | 'hidden')}
          sx={{ width: 160 }}
        >
          <MenuItem value="">Tất cả</MenuItem>
          <MenuItem value="visible">Đang hiển thị</MenuItem>
          <MenuItem value="hidden">Đã ẩn</MenuItem>
        </TextField>
      </Box>

      <TableContainer component={Paper} elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
        <Table size="medium">
          <TableHead sx={{ backgroundColor: 'action.hover' }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>Mã đơn</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Khách hàng</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Merchant</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Số sao</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Nhận xét</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Ngày đánh giá</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Trạng thái</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 4 }}>
                  <CircularProgress size={28} />
                </TableCell>
              </TableRow>
            ) : data.list.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 4 }}>
                  <Typography variant="body2" color="text.secondary">Chưa có đánh giá nào.</Typography>
                </TableCell>
              </TableRow>
            ) : (
              data.list.map((row) => (
                <TableRow
                  key={row.id}
                  hover
                  sx={{ cursor: 'pointer' }}
                  onClick={() => { setSelected(row); setHideReason(''); setDialogError(''); }}
                >
                  <TableCell sx={{ fontWeight: 700 }}>{row.orderCode}</TableCell>
                  <TableCell>{row.customerName}</TableCell>
                  <TableCell>{row.merchantName || '—'}</TableCell>
                  <TableCell><Rating value={row.overallStars} readOnly size="small" /></TableCell>
                  <TableCell sx={{ maxWidth: 280 }}>
                    <Typography variant="body2" noWrap title={row.comment}>{row.comment || '—'}</Typography>
                  </TableCell>
                  <TableCell>{formatUnix(row.createdOn)}</TableCell>
                  <TableCell>
                    <Chip label={row.isHidden ? 'Đã ẩn' : 'Hiển thị'} color={row.isHidden ? 'default' : 'success'} size="small" />
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={!!selected} onClose={() => setSelected(null)} maxWidth="sm" fullWidth>
        {selected && (
          <>
            <DialogTitle sx={{ fontWeight: 800 }}>Đánh giá đơn {selected.orderCode}</DialogTitle>
            <DialogContent dividers>
              {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
              <Typography variant="body2" sx={{ mb: 1 }}>
                <b>{selected.customerName}</b> · {selected.merchantName || '—'} · {formatUnix(selected.createdOn)}
              </Typography>
              <Rating value={selected.overallStars} readOnly />
              <Grid container spacing={1} sx={{ my: 1 }}>
                {CRITERIA.map((c) => (
                  <Grid item xs={6} key={c.key}>
                    <Stack direction="row" alignItems="center" spacing={1}>
                      <Typography variant="caption" sx={{ width: 110 }}>{c.label}</Typography>
                      {selected[c.key] ? (
                        <Rating value={Number(selected[c.key])} readOnly size="small" />
                      ) : (
                        <Typography variant="caption" color="text.secondary">—</Typography>
                      )}
                    </Stack>
                  </Grid>
                ))}
              </Grid>
              <Typography variant="body2" sx={{ mb: 1.5 }}>{selected.comment || 'Không có nhận xét.'}</Typography>
              <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', mb: 2 }}>
                {selected.imageUrls.map((url) => (
                  <a key={url} href={toImageUrl(url)} target="_blank" rel="noreferrer">
                    <Box component="img" src={toImageUrl(url)} alt="Ảnh đánh giá"
                      sx={{ width: 100, height: 100, objectFit: 'cover', borderRadius: 1, border: '1px solid', borderColor: 'divider' }} />
                  </a>
                ))}
              </Stack>
              {selected.isHidden ? (
                <Alert severity="info">Đã ẩn. Lý do: {selected.hiddenReason}</Alert>
              ) : (
                <TextField fullWidth size="small" label="Lý do ẩn đánh giá" value={hideReason}
                  onChange={(e) => setHideReason(e.target.value)} />
              )}
            </DialogContent>
            <DialogActions sx={{ px: 3, py: 2 }}>
              <Button onClick={() => setSelected(null)}>Đóng</Button>
              {selected.isHidden ? (
                <Button variant="contained" onClick={() => handleVisibility(false)}>Hiển thị lại</Button>
              ) : (
                <Button variant="contained" color="error" onClick={() => handleVisibility(true)}>Ẩn đánh giá</Button>
              )}
            </DialogActions>
          </>
        )}
      </Dialog>

      <Snackbar open={!!toast} autoHideDuration={4000} onClose={() => setToast(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}>
        <Alert severity={toast?.severity ?? 'success'} onClose={() => setToast(null)} variant="filled">
          {toast?.message}
        </Alert>
      </Snackbar>
    </Box>
  );
};
