import React, { useCallback, useEffect, useState } from 'react';
import { useSelector } from 'react-redux';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Grid,
  IconButton,
  InputAdornment,
  MenuItem,
  Paper,
  Rating,
  Snackbar,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import RefreshIcon from '@mui/icons-material/Refresh';
import SearchIcon from '@mui/icons-material/Search';
import UploadIcon from '@mui/icons-material/Upload';
import { RootState } from '../../redux/store.redux';
import {
  ShipperFormDto,
  ShipperItem,
  ShipperStatus,
  VehicleType,
  createShipper,
  filterShippers,
  updateShipper,
  uploadImage,
} from '../../apis/shipper/shipper.api';
import { filterMerchants } from '../../apis/merchant/merchant.api';
import { MerchantItem } from '../../apis/merchant/merchant.interface';
import { toImageUrl } from '../../apis/feedback/feedback.api';
import { getApiErrorMessage } from '../../common/utils/api-base-response.util';

const VEHICLE_LABEL: Record<VehicleType, string> = {
  [VehicleType.Motorbike]: 'Xe máy',
  [VehicleType.Car]: 'Ô tô',
  [VehicleType.Bicycle]: 'Xe đạp',
};

const STATUS_LABEL: Record<ShipperStatus, string> = {
  [ShipperStatus.Available]: 'Sẵn sàng',
  [ShipperStatus.Busy]: 'Đang bận',
  [ShipperStatus.Offline]: 'Ngoại tuyến',
};

const emptyForm = (): ShipperFormDto => ({
  fullName: '',
  phone: '',
  email: '',
  vehicleType: VehicleType.Motorbike,
  vehiclePlate: '',
  identityNumber: '',
  identityImageUrls: [],
  isActive: true,
});

export const ShipperScreen: React.FC = () => {
  const { user } = useSelector((state: RootState) => state.account);
  const isAdmin = user?.role === 'Admin';
  const [data, setData] = useState<{ list: ShipperItem[]; total: number }>({ list: [], total: 0 });
  const [loading, setLoading] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [activeFilter, setActiveFilter] = useState<'' | 'active' | 'inactive'>('');
  const [merchants, setMerchants] = useState<MerchantItem[]>([]);
  const [merchantFilter, setMerchantFilter] = useState('');
  const [editing, setEditing] = useState<ShipperItem | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [form, setForm] = useState<ShipperFormDto>(emptyForm());
  const [formError, setFormError] = useState('');
  const [uploading, setUploading] = useState(false);
  const [toast, setToast] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const res = await filterShippers({
        page: 1,
        take: 100,
        keyword,
        isActive: activeFilter === '' ? undefined : activeFilter === 'active',
        merchantId: merchantFilter || undefined,
      });
      setData({ list: res.list || [], total: res.total || 0 });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không tải được danh sách Shipper.'), severity: 'error' });
    } finally {
      setLoading(false);
    }
  }, [keyword, activeFilter, merchantFilter]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    if (!isAdmin) return;
    filterMerchants({ page: 1, take: 100 })
      .then((res) => setMerchants(res.list || []))
      .catch(() => setMerchants([]));
  }, [isAdmin]);

  const merchantName = (id: string) => merchants.find((m) => m.id === id)?.businessName ?? '—';

  const openCreate = () => {
    setEditing(null);
    setForm({ ...emptyForm(), merchantId: merchantFilter || undefined });
    setFormError('');
    setFormOpen(true);
  };

  const openEdit = (row: ShipperItem) => {
    setEditing(row);
    setForm({
      fullName: row.fullName,
      phone: row.phone ?? '',
      email: row.email,
      vehicleType: row.vehicleType,
      vehiclePlate: row.vehiclePlate ?? '',
      isActive: row.isActive,
    });
    setFormError('');
    setFormOpen(true);
  };

  const handleUpload = async (files: FileList | null) => {
    if (!files?.length) return;
    try {
      setUploading(true);
      const urls: string[] = [];
      for (const file of Array.from(files)) {
        const res = await uploadImage(file);
        urls.push(res.data.url);
      }
      setForm((f) => ({ ...f, identityImageUrls: [...(f.identityImageUrls ?? []), ...urls] }));
    } catch (err) {
      setFormError(getApiErrorMessage(err, 'Không thể upload ảnh. Vui lòng thử lại.'));
    } finally {
      setUploading(false);
    }
  };

  const handleSave = async () => {
    try {
      const res = editing ? await updateShipper(editing.id, form) : await createShipper(form);
      setFormOpen(false);
      setToast({ message: res.message || 'Lưu Shipper thành công!', severity: 'success' });
      fetchData();
    } catch (err) {
      // Keep the dialog open so the Merchant can retry (spec 8b).
      setFormError(getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'));
    }
  };

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Box sx={{ mb: 2.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
            Quản Lý Shipper
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Nhân viên giao nhận của tiệm: thêm tài khoản, cập nhật thông tin và trạng thái làm việc
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" startIcon={<RefreshIcon />} onClick={fetchData}>Làm mới</Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>Thêm mới</Button>
        </Stack>
      </Box>

      <Box sx={{ mb: 2, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <TextField
          size="small"
          placeholder="Tên, SĐT, email hoặc biển số..."
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          sx={{ width: 300 }}
          InputProps={{ startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }}
        />
        <TextField select size="small" label="Trạng thái" value={activeFilter} sx={{ width: 180 }}
          onChange={(e) => setActiveFilter(e.target.value as '' | 'active' | 'inactive')}>
          <MenuItem value="">Tất cả</MenuItem>
          <MenuItem value="active">Đang làm việc</MenuItem>
          <MenuItem value="inactive">Tạm nghỉ</MenuItem>
        </TextField>
        {isAdmin && (
          <TextField select size="small" label="Merchant" value={merchantFilter} sx={{ width: 240 }}
            onChange={(e) => setMerchantFilter(e.target.value)}>
            <MenuItem value="">Tất cả</MenuItem>
            {merchants.map((m) => <MenuItem key={m.id} value={m.id}>{m.businessName}</MenuItem>)}
          </TextField>
        )}
      </Box>

      <TableContainer component={Paper} elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
        <Table size="medium">
          <TableHead sx={{ backgroundColor: 'action.hover' }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>Họ tên</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Số điện thoại</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Phương tiện</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Biển số xe</TableCell>
              {isAdmin && <TableCell sx={{ fontWeight: 700 }}>Merchant</TableCell>}
              <TableCell sx={{ fontWeight: 700 }}>Trạng thái</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Số đơn đã giao</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Đánh giá</TableCell>
              <TableCell align="right" sx={{ fontWeight: 700 }}>Thao tác</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow><TableCell colSpan={9} align="center" sx={{ py: 4 }}><CircularProgress size={28} /></TableCell></TableRow>
            ) : data.list.length === 0 ? (
              <TableRow>
                <TableCell colSpan={9} align="center" sx={{ py: 4 }}>
                  <Typography variant="body2" color="text.secondary">Bạn chưa có Shipper nào.</Typography>
                </TableCell>
              </TableRow>
            ) : (
              data.list.map((row) => (
                <TableRow key={row.id} hover>
                  <TableCell sx={{ fontWeight: 600 }}>
                    {row.fullName}
                    <Typography variant="caption" display="block" color="text.secondary">{row.email}</Typography>
                  </TableCell>
                  <TableCell>{row.phone || '—'}</TableCell>
                  <TableCell>{VEHICLE_LABEL[row.vehicleType]}</TableCell>
                  <TableCell>{row.vehiclePlate || '—'}</TableCell>
                  {isAdmin && <TableCell>{merchantName(row.merchantId)}</TableCell>}
                  <TableCell>
                    <Stack direction="row" spacing={0.5}>
                      <Chip size="small" label={row.isActive ? 'Đang làm việc' : 'Tạm nghỉ'} color={row.isActive ? 'success' : 'default'} />
                      {row.activeTaskCount > 0 && <Chip size="small" variant="outlined" color="warning" label={`${row.activeTaskCount} chuyến đang giao`} />}
                    </Stack>
                    <Typography variant="caption" color="text.secondary">{STATUS_LABEL[row.status]}</Typography>
                  </TableCell>
                  <TableCell>{row.totalDeliveries}</TableCell>
                  <TableCell>
                    {row.rating > 0 ? <Rating value={Number(row.rating)} precision={0.1} readOnly size="small" /> : '—'}
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title="Chỉnh sửa">
                      <IconButton size="small" onClick={() => openEdit(row)}><EditIcon fontSize="small" /></IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={formOpen} onClose={() => setFormOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>{editing ? 'Chỉnh Sửa Shipper' : 'Thêm Shipper Mới'}</DialogTitle>
        <DialogContent dividers>
          {formError && <Alert severity="error" sx={{ mb: 2 }}>{formError}</Alert>}
          <Grid container spacing={2} sx={{ pt: 1 }}>
            {isAdmin && !editing && (
              <Grid item xs={12}>
                <TextField select fullWidth size="small" label="Merchant quản lý" value={form.merchantId ?? ''}
                  onChange={(e) => setForm({ ...form, merchantId: e.target.value })}>
                  {merchants.map((m) => <MenuItem key={m.id} value={m.id}>{m.businessName}</MenuItem>)}
                </TextField>
              </Grid>
            )}
            <Grid item xs={12}>
              <TextField fullWidth size="small" label="Họ tên" value={form.fullName}
                onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
            </Grid>
            <Grid item xs={6}>
              <TextField fullWidth size="small" label="Số điện thoại" value={form.phone}
                onChange={(e) => setForm({ ...form, phone: e.target.value })} />
            </Grid>
            <Grid item xs={6}>
              <TextField fullWidth size="small" label="Email (dùng để đăng nhập)" value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })} />
            </Grid>
            <Grid item xs={6}>
              <TextField select fullWidth size="small" label="Phương tiện" value={form.vehicleType}
                onChange={(e) => setForm({ ...form, vehicleType: Number(e.target.value) as VehicleType })}>
                {Object.entries(VEHICLE_LABEL).map(([value, label]) => <MenuItem key={value} value={Number(value)}>{label}</MenuItem>)}
              </TextField>
            </Grid>
            <Grid item xs={6}>
              <TextField fullWidth size="small" label="Biển số xe" value={form.vehiclePlate}
                onChange={(e) => setForm({ ...form, vehiclePlate: e.target.value.toUpperCase() })} />
            </Grid>
            {!editing && (
              <>
                <Grid item xs={12}>
                  <TextField fullWidth size="small" label="Số CMND/CCCD" value={form.identityNumber}
                    onChange={(e) => setForm({ ...form, identityNumber: e.target.value })} />
                </Grid>
                <Grid item xs={12}>
                  <Stack direction="row" spacing={1} alignItems="center" sx={{ flexWrap: 'wrap' }}>
                    <Button component="label" variant="outlined" size="small" startIcon={<UploadIcon />} disabled={uploading}>
                      {uploading ? 'Đang upload...' : 'Ảnh CMND/CCCD'}
                      <input hidden type="file" accept="image/png,image/jpeg,image/webp" multiple
                        onChange={(e) => handleUpload(e.target.files)} />
                    </Button>
                    {(form.identityImageUrls ?? []).map((url) => (
                      <Box key={url} component="img" src={toImageUrl(url)} alt="CMND"
                        sx={{ width: 56, height: 56, objectFit: 'cover', borderRadius: 1, border: '1px solid', borderColor: 'divider' }} />
                    ))}
                  </Stack>
                </Grid>
              </>
            )}
            {editing && (
              <Grid item xs={12}>
                <FormControlLabel
                  control={<Switch checked={!!form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />}
                  label={form.isActive ? 'Đang làm việc' : 'Tạm nghỉ'}
                />
              </Grid>
            )}
          </Grid>
          {!editing && (
            <Alert severity="info" sx={{ mt: 2 }}>
              Hệ thống tạo tài khoản Shipper và gửi mật khẩu tạm thời tới email trên.
            </Alert>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setFormOpen(false)}>Hủy</Button>
          <Button variant="contained" onClick={handleSave} disabled={uploading}>Lưu</Button>
        </DialogActions>
      </Dialog>

      <Snackbar open={!!toast} autoHideDuration={4000} onClose={() => setToast(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}>
        <Alert severity={toast?.severity ?? 'success'} onClose={() => setToast(null)} variant="filled">{toast?.message}</Alert>
      </Snackbar>
    </Box>
  );
};
