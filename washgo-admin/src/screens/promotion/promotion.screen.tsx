import React, { useEffect, useState, useCallback } from 'react';
import {
  Box,
  Typography,
  Button,
  Stack,
  Chip,
  Switch,
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
  IconButton,
  Tooltip,
  Alert,
  Snackbar,
  FormControlLabel,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import SearchIcon from '@mui/icons-material/Search';
import RefreshIcon from '@mui/icons-material/Refresh';
import EditIcon from '@mui/icons-material/Edit';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import BarChartIcon from '@mui/icons-material/BarChart';
import DownloadIcon from '@mui/icons-material/Download';
import {
  filterPromotions,
  createPromotion,
  updatePromotion,
  deletePromotion,
  togglePromotionActive,
  getPromotionStatistics,
  exportPromotionStatistics,
} from '../../apis/promotion/promotion.api';
import {
  PromotionItem,
  CreatePromotionDto,
  DiscountType,
  PromotionStatus,
  PromotionStatistics,
} from '../../apis/promotion/promotion.interface';
import { unwrapBaseResponse, getApiErrorMessage } from '../../common/utils/api-base-response.util';

const STATUS_META: Record<PromotionStatus, { label: string; color: 'default' | 'success' | 'info' | 'warning' | 'error' }> = {
  [PromotionStatus.Inactive]: { label: 'Ngừng áp dụng', color: 'default' },
  [PromotionStatus.Upcoming]: { label: 'Sắp diễn ra', color: 'info' },
  [PromotionStatus.Active]: { label: 'Đang áp dụng', color: 'success' },
  [PromotionStatus.Expired]: { label: 'Hết hạn', color: 'error' },
  [PromotionStatus.Exhausted]: { label: 'Hết lượt', color: 'warning' },
};

const money = (value?: number | null) => `${Number(value ?? 0).toLocaleString('vi-VN')}đ`;

// datetime-local works in local time; the API stores UTC.
const toLocalInput = (iso: string) => {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};
const fromLocalInput = (value: string) => (value ? new Date(value).toISOString() : '');
const formatDateTime = (iso: string) => new Date(iso).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });

const emptyForm = (): CreatePromotionDto => {
  const start = new Date();
  const end = new Date(start.getTime() + 30 * 24 * 3600 * 1000);
  return {
    code: '',
    description: '',
    discountType: DiscountType.Percentage,
    discountValue: 10,
    minOrderAmount: 0,
    maxDiscountAmount: null,
    startAt: start.toISOString(),
    endAt: end.toISOString(),
    usageLimit: null,
    usageLimitPerCustomer: 1,
    isActive: true,
  };
};

const optionalNumber = (value: string) => (value === '' ? null : Number(value));

export const PromotionScreen: React.FC = () => {
  const [data, setData] = useState<{ list: PromotionItem[]; total: number }>({ list: [], total: 0 });
  const [loading, setLoading] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [status, setStatus] = useState<PromotionStatus | ''>('');
  const [editing, setEditing] = useState<PromotionItem | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [formData, setFormData] = useState<CreatePromotionDto>(emptyForm());
  const [formError, setFormError] = useState('');
  const [deleting, setDeleting] = useState<PromotionItem | null>(null);
  const [statsFor, setStatsFor] = useState<PromotionItem | null>(null);
  const [stats, setStats] = useState<PromotionStatistics | null>(null);
  const [statsMessage, setStatsMessage] = useState('');
  const [toast, setToast] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const res = await filterPromotions({ page: 1, take: 100, keyword, status: status === '' ? undefined : status });
      setData({ list: res.list || [], total: res.total || 0 });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không tải được danh sách khuyến mãi.'), severity: 'error' });
    } finally {
      setLoading(false);
    }
  }, [keyword, status]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const openCreate = () => {
    setEditing(null);
    setFormData(emptyForm());
    setFormError('');
    setFormOpen(true);
  };

  const openEdit = (row: PromotionItem) => {
    setEditing(row);
    setFormData({
      code: row.code,
      description: row.description ?? '',
      discountType: row.discountType,
      discountValue: row.discountValue,
      minOrderAmount: row.minOrderAmount,
      maxDiscountAmount: row.maxDiscountAmount ?? null,
      startAt: row.startAt,
      endAt: row.endAt,
      usageLimit: row.usageLimit ?? null,
      usageLimitPerCustomer: row.usageLimitPerCustomer ?? null,
      isActive: row.isActive,
    });
    setFormError('');
    setFormOpen(true);
  };

  const handleSave = async () => {
    try {
      const res = editing
        ? await updatePromotion({ ...formData, id: editing.id })
        : await createPromotion(formData);
      setFormOpen(false);
      setToast({ message: res.message || 'Lưu khuyến mãi thành công!', severity: 'success' });
      fetchData();
    } catch (err) {
      setFormError(getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'));
    }
  };

  const handleDelete = async () => {
    if (!deleting) return;
    try {
      const res = await deletePromotion(deleting.id);
      setToast({ message: res.message || 'Xóa Khuyến mãi thành công!', severity: 'success' });
      fetchData();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'), severity: 'error' });
    } finally {
      setDeleting(null);
    }
  };

  const handleToggle = async (id: string) => {
    try {
      await togglePromotionActive(id);
      fetchData();
    } catch (err) {
      setToast({ message: getApiErrorMessage(err), severity: 'error' });
    }
  };

  const openStats = async (row: PromotionItem) => {
    setStatsFor(row);
    setStats(null);
    setStatsMessage('');
    try {
      const res = await getPromotionStatistics(row.id);
      setStats(unwrapBaseResponse<PromotionStatistics>(res));
      setStatsMessage(res.message ?? '');
    } catch (err) {
      setStatsMessage(getApiErrorMessage(err, 'Không tải được thống kê.'));
    }
  };

  const handleExport = async () => {
    if (!statsFor) return;
    try {
      const blob = await exportPromotionStatistics(statsFor.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `thong-ke-${statsFor.code}.csv`;
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      setToast({ message: 'Không thể xuất báo cáo. Vui lòng thử lại.', severity: 'error' });
    }
  };

  const maxDailyCount = Math.max(1, ...(stats?.daily.map((d) => d.usedCount) ?? [1]));

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Box sx={{ mb: 2.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
            Quản Lý Khuyến Mãi
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Tạo mã giảm giá toàn hệ thống, giới hạn lượt dùng và theo dõi hiệu quả sử dụng
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" startIcon={<RefreshIcon />} onClick={fetchData}>
            Làm mới
          </Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
            Thêm mới
          </Button>
        </Stack>
      </Box>

      <Box sx={{ mb: 2, display: 'flex', gap: 2 }}>
        <TextField
          size="small"
          placeholder="Tìm theo mã hoặc mô tả..."
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
          label="Trạng thái"
          value={status}
          onChange={(e) => setStatus(e.target.value === '' ? '' : (Number(e.target.value) as PromotionStatus))}
          sx={{ width: 200 }}
        >
          <MenuItem value="">Tất cả</MenuItem>
          {Object.entries(STATUS_META).map(([value, meta]) => (
            <MenuItem key={value} value={Number(value)}>{meta.label}</MenuItem>
          ))}
        </TextField>
      </Box>

      <TableContainer component={Paper} elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
        <Table size="medium">
          <TableHead sx={{ backgroundColor: 'action.hover' }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>Mã</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Mô tả</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Loại giảm</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Mức giảm</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Thời gian</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Lượt dùng</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Trạng thái</TableCell>
              <TableCell align="right" sx={{ fontWeight: 700 }}>Thao tác</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 4 }}>
                  <CircularProgress size={28} />
                </TableCell>
              </TableRow>
            ) : data.list.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 4 }}>
                  <Typography variant="body2" color="text.secondary">Chưa có mã giảm giá nào.</Typography>
                </TableCell>
              </TableRow>
            ) : (
              data.list.map((row) => (
                <TableRow key={row.id} hover>
                  <TableCell>
                    <Chip label={row.code} size="small" variant="outlined" color="primary" sx={{ fontWeight: 700 }} />
                  </TableCell>
                  <TableCell sx={{ maxWidth: 220 }}>
                    <Typography variant="body2" noWrap title={row.description}>{row.description || '—'}</Typography>
                    {row.minOrderAmount > 0 && (
                      <Typography variant="caption" color="text.secondary">Đơn tối thiểu {money(row.minOrderAmount)}</Typography>
                    )}
                  </TableCell>
                  <TableCell>{row.discountType === DiscountType.Percentage ? 'Phần trăm' : 'Số tiền cố định'}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>
                    {row.discountType === DiscountType.Percentage ? `${row.discountValue}%` : money(row.discountValue)}
                    {row.maxDiscountAmount != null && (
                      <Typography variant="caption" display="block" color="text.secondary">Tối đa {money(row.maxDiscountAmount)}</Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{formatDateTime(row.startAt)}</Typography>
                    <Typography variant="caption" color="text.secondary">→ {formatDateTime(row.endAt)}</Typography>
                  </TableCell>
                  <TableCell>
                    {row.usedCount}{row.usageLimit != null ? ` / ${row.usageLimit}` : ''}
                    {row.usageLimitPerCustomer != null && (
                      <Typography variant="caption" display="block" color="text.secondary">{row.usageLimitPerCustomer} lượt/khách</Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    <Chip label={STATUS_META[row.status].label} color={STATUS_META[row.status].color} size="small" sx={{ fontWeight: 600 }} />
                  </TableCell>
                  <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                    <Tooltip title="Thống kê">
                      <IconButton size="small" onClick={() => openStats(row)}><BarChartIcon fontSize="small" /></IconButton>
                    </Tooltip>
                    <Tooltip title="Chỉnh sửa">
                      <IconButton size="small" onClick={() => openEdit(row)}><EditIcon fontSize="small" /></IconButton>
                    </Tooltip>
                    <Tooltip title="Xóa">
                      <IconButton size="small" color="error" onClick={() => setDeleting(row)}><DeleteOutlineIcon fontSize="small" /></IconButton>
                    </Tooltip>
                    <Tooltip title={row.isActive ? 'Ngừng áp dụng' : 'Kích hoạt'}>
                      <Switch checked={row.isActive} onChange={() => handleToggle(row.id)} size="small" />
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create / edit */}
      <Dialog open={formOpen} onClose={() => setFormOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>{editing ? 'Chỉnh Sửa Khuyến Mãi' : 'Thêm Khuyến Mãi Mới'}</DialogTitle>
        <DialogContent dividers>
          {formError && <Alert severity="error" sx={{ mb: 2 }}>{formError}</Alert>}
          <Grid container spacing={2} sx={{ pt: 1 }}>
            <Grid item xs={6}>
              <TextField
                fullWidth
                label="Mã (VD: WASHGO10)"
                size="small"
                value={formData.code}
                disabled={!!editing && editing.usedCount > 0}
                onChange={(e) => setFormData({ ...formData, code: e.target.value.toUpperCase() })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                select
                fullWidth
                label="Loại giảm"
                size="small"
                value={formData.discountType}
                onChange={(e) => setFormData({ ...formData, discountType: Number(e.target.value) as DiscountType })}
              >
                <MenuItem value={DiscountType.Percentage}>Phần trăm (%)</MenuItem>
                <MenuItem value={DiscountType.FixedAmount}>Số tiền cố định (đ)</MenuItem>
              </TextField>
            </Grid>
            <Grid item xs={12}>
              <TextField
                fullWidth
                label="Mô tả"
                size="small"
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                fullWidth
                type="number"
                label={formData.discountType === DiscountType.Percentage ? 'Mức giảm (%)' : 'Mức giảm (đ)'}
                size="small"
                value={formData.discountValue}
                onChange={(e) => setFormData({ ...formData, discountValue: Number(e.target.value) })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                fullWidth
                type="number"
                label="Giảm tối đa (đ, để trống nếu không giới hạn)"
                size="small"
                value={formData.maxDiscountAmount ?? ''}
                onChange={(e) => setFormData({ ...formData, maxDiscountAmount: optionalNumber(e.target.value) })}
              />
            </Grid>
            <Grid item xs={12}>
              <TextField
                fullWidth
                type="number"
                label="Giá trị đơn tối thiểu (đ)"
                size="small"
                value={formData.minOrderAmount}
                onChange={(e) => setFormData({ ...formData, minOrderAmount: Number(e.target.value) })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                fullWidth
                type="datetime-local"
                label="Bắt đầu"
                size="small"
                InputLabelProps={{ shrink: true }}
                value={formData.startAt ? toLocalInput(formData.startAt) : ''}
                onChange={(e) => setFormData({ ...formData, startAt: fromLocalInput(e.target.value) })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                fullWidth
                type="datetime-local"
                label="Kết thúc"
                size="small"
                InputLabelProps={{ shrink: true }}
                value={formData.endAt ? toLocalInput(formData.endAt) : ''}
                onChange={(e) => setFormData({ ...formData, endAt: fromLocalInput(e.target.value) })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                fullWidth
                type="number"
                label="Tổng số lượt (trống = không giới hạn)"
                size="small"
                value={formData.usageLimit ?? ''}
                onChange={(e) => setFormData({ ...formData, usageLimit: optionalNumber(e.target.value) })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                fullWidth
                type="number"
                label="Số lượt mỗi khách (trống = không giới hạn)"
                size="small"
                value={formData.usageLimitPerCustomer ?? ''}
                onChange={(e) => setFormData({ ...formData, usageLimitPerCustomer: optionalNumber(e.target.value) })}
              />
            </Grid>
            <Grid item xs={12}>
              <FormControlLabel
                control={<Switch checked={formData.isActive} onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })} />}
                label="Kích hoạt"
              />
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setFormOpen(false)}>Hủy</Button>
          <Button variant="contained" onClick={handleSave}>Lưu</Button>
        </DialogActions>
      </Dialog>

      {/* Delete confirmation */}
      <Dialog open={!!deleting} onClose={() => setDeleting(null)}>
        <DialogTitle sx={{ fontWeight: 800 }}>Xóa mã giảm giá?</DialogTitle>
        <DialogContent>
          <Typography variant="body2">
            Mã <b>{deleting?.code}</b> sẽ bị xóa khỏi hệ thống. Mã đã có lượt sử dụng không thể xóa — hãy tắt kích hoạt thay vào đó.
          </Typography>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setDeleting(null)}>Hủy</Button>
          <Button variant="contained" color="error" onClick={handleDelete}>Xác nhận</Button>
        </DialogActions>
      </Dialog>

      {/* Statistics */}
      <Dialog open={!!statsFor} onClose={() => setStatsFor(null)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>Thống kê mã {statsFor?.code}</DialogTitle>
        <DialogContent dividers>
          {!stats ? (
            statsMessage ? <Alert severity="error">{statsMessage}</Alert> : <CircularProgress size={28} />
          ) : (
            <>
              <Grid container spacing={2} sx={{ mb: 2 }}>
                {[
                  { label: 'Số lượt đã dùng', value: String(stats.usedCount) },
                  { label: 'Tổng tiền đã giảm', value: money(stats.totalDiscountAmount) },
                  { label: 'Doanh thu từ đơn dùng mã', value: money(stats.revenue) },
                  { label: 'Đã thanh toán', value: money(stats.paidRevenue) },
                ].map((tile) => (
                  <Grid item xs={6} key={tile.label}>
                    <Paper variant="outlined" sx={{ p: 1.5, borderRadius: 2 }}>
                      <Typography variant="caption" color="text.secondary">{tile.label}</Typography>
                      <Typography variant="h6" sx={{ fontWeight: 800 }}>{tile.value}</Typography>
                    </Paper>
                  </Grid>
                ))}
              </Grid>
              {stats.usedCount === 0 ? (
                <Typography variant="body2" color="text.secondary">{statsMessage || 'Mã này chưa được sử dụng lần nào.'}</Typography>
              ) : (
                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>Lượt dùng theo ngày</Typography>
                  <Stack spacing={0.75}>
                    {stats.daily.map((day) => (
                      <Stack key={day.date} direction="row" alignItems="center" spacing={1}>
                        <Typography variant="caption" sx={{ width: 80 }}>{day.date}</Typography>
                        <Box sx={{ flexGrow: 1, bgcolor: 'action.hover', borderRadius: 1, height: 14 }}>
                          <Box sx={{ width: `${(day.usedCount / maxDailyCount) * 100}%`, bgcolor: 'primary.main', height: '100%', borderRadius: 1 }} />
                        </Box>
                        <Typography variant="caption" sx={{ width: 120, textAlign: 'right' }}>
                          {day.usedCount} lượt · {money(day.discountAmount)}
                        </Typography>
                      </Stack>
                    ))}
                  </Stack>
                </Box>
              )}
            </>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button startIcon={<DownloadIcon />} onClick={handleExport} disabled={!stats}>Xuất báo cáo</Button>
          <Button variant="contained" onClick={() => setStatsFor(null)}>Đóng</Button>
        </DialogActions>
      </Dialog>

      <Snackbar open={!!toast} autoHideDuration={4000} onClose={() => setToast(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}>
        <Alert severity={toast?.severity ?? 'success'} onClose={() => setToast(null)} variant="filled">
          {toast?.message}
        </Alert>
      </Snackbar>
    </Box>
  );
};
