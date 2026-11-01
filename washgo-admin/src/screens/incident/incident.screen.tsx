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
  Divider,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import RefreshIcon from '@mui/icons-material/Refresh';
import {
  filterIncidents,
  resolveIncident,
  rejectIncident,
  confirmCompensation,
  toImageUrl,
} from '../../apis/feedback/feedback.api';
import {
  IncidentItem,
  IncidentStatus,
  IncidentSeverity,
  IncidentType,
  CompensationLevel,
  CompensationStatus,
  ResponsibleParty,
  ResolveIncidentDto,
} from '../../apis/feedback/feedback.interface';
import { getApiErrorMessage } from '../../common/utils/api-base-response.util';

const STATUS_META: Record<IncidentStatus, { label: string; color: 'warning' | 'success' | 'default' }> = {
  [IncidentStatus.Pending]: { label: 'Chờ xử lý', color: 'warning' },
  [IncidentStatus.Resolved]: { label: 'Đã xử lý', color: 'success' },
  [IncidentStatus.Rejected]: { label: 'Từ chối', color: 'default' },
};
const SEVERITY_LABEL: Record<IncidentSeverity, string> = {
  [IncidentSeverity.Light]: 'Nhẹ',
  [IncidentSeverity.Medium]: 'Trung bình',
  [IncidentSeverity.Severe]: 'Nặng',
};
const TYPE_LABEL: Record<IncidentType, string> = {
  [IncidentType.Damaged]: 'Hư hỏng',
  [IncidentType.Lost]: 'Mất đồ',
  [IncidentType.Other]: 'Khác',
};
const LEVEL_META: Record<CompensationLevel, { label: string; percent: number }> = {
  [CompensationLevel.None]: { label: 'Không bồi thường', percent: 0 },
  [CompensationLevel.Light]: { label: 'Hư nhẹ (30%)', percent: 30 },
  [CompensationLevel.Medium]: { label: 'Hư trung bình (70%)', percent: 70 },
  [CompensationLevel.Severe]: { label: 'Hư nặng (100%)', percent: 100 },
  [CompensationLevel.Lost]: { label: 'Mất đồ (200%)', percent: 200 },
};
const PARTY_LABEL: Record<ResponsibleParty, string> = {
  [ResponsibleParty.Merchant]: 'Tiệm giặt (Merchant)',
  [ResponsibleParty.Shipper]: 'Shipper',
  [ResponsibleParty.Platform]: 'Nền tảng WashGo',
  [ResponsibleParty.Customer]: 'Khách hàng',
};

const money = (value?: number | null) => `${Number(value ?? 0).toLocaleString('vi-VN')}đ`;
const formatUnix = (seconds: number) => new Date(seconds * 1000).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });

const emptyResolve = (): ResolveIncidentDto => ({
  resolutionNote: '',
  responsibleParty: ResponsibleParty.Merchant,
  compensationLevel: CompensationLevel.None,
  itemValue: 0,
});

export const IncidentScreen: React.FC = () => {
  const [data, setData] = useState<{ list: IncidentItem[]; total: number }>({ list: [], total: 0 });
  const [loading, setLoading] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [status, setStatus] = useState<IncidentStatus | ''>(IncidentStatus.Pending);
  const [severity, setSeverity] = useState<IncidentSeverity | ''>('');
  const [selected, setSelected] = useState<IncidentItem | null>(null);
  const [resolveForm, setResolveForm] = useState<ResolveIncidentDto>(emptyResolve());
  const [rejectReason, setRejectReason] = useState('');
  const [reference, setReference] = useState('');
  const [dialogError, setDialogError] = useState('');
  const [toast, setToast] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const res = await filterIncidents({
        page: 1,
        take: 100,
        keyword,
        status: status === '' ? undefined : status,
        severity: severity === '' ? undefined : severity,
      });
      setData({ list: res.list || [], total: res.total || 0 });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không tải được danh sách khiếu nại.'), severity: 'error' });
    } finally {
      setLoading(false);
    }
  }, [keyword, status, severity]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const openDetail = (row: IncidentItem) => {
    setSelected(row);
    setResolveForm(emptyResolve());
    setRejectReason('');
    setReference('');
    setDialogError('');
  };

  const runAction = async (action: () => Promise<{ data: IncidentItem; message?: string }>) => {
    try {
      const res = await action();
      setSelected(res.data);
      setDialogError('');
      setToast({ message: res.message || 'Thao tác thành công.', severity: 'success' });
      fetchData();
    } catch (err) {
      setDialogError(getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'));
    }
  };

  const previewAmount = Math.round((resolveForm.itemValue * LEVEL_META[resolveForm.compensationLevel].percent) / 100);

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Box sx={{ mb: 2.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
            Quản Lý Khiếu Nại
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Xem báo cáo hư hỏng / mất đồ, xác định trách nhiệm và mức bồi thường
          </Typography>
        </Box>
        <Button variant="outlined" startIcon={<RefreshIcon />} onClick={fetchData}>
          Làm mới
        </Button>
      </Box>

      <Box sx={{ mb: 2, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <TextField
          size="small"
          placeholder="Mã đơn, khách hàng hoặc Merchant..."
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
          onChange={(e) => setStatus(e.target.value === '' ? '' : (Number(e.target.value) as IncidentStatus))}
          sx={{ width: 180 }}
        >
          <MenuItem value="">Tất cả</MenuItem>
          {Object.entries(STATUS_META).map(([value, meta]) => (
            <MenuItem key={value} value={Number(value)}>{meta.label}</MenuItem>
          ))}
        </TextField>
        <TextField
          select
          size="small"
          label="Mức độ"
          value={severity}
          onChange={(e) => setSeverity(e.target.value === '' ? '' : (Number(e.target.value) as IncidentSeverity))}
          sx={{ width: 160 }}
        >
          <MenuItem value="">Tất cả</MenuItem>
          {Object.entries(SEVERITY_LABEL).map(([value, label]) => (
            <MenuItem key={value} value={Number(value)}>{label}</MenuItem>
          ))}
        </TextField>
      </Box>

      <TableContainer component={Paper} elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
        <Table size="medium">
          <TableHead sx={{ backgroundColor: 'action.hover' }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>Mã đơn</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Khách hàng</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Loại sự cố</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Mức độ</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Merchant</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Ngày gửi</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Trạng thái</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Bồi thường</TableCell>
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
                  <Typography variant="body2" color="text.secondary">Chưa có khiếu nại nào.</Typography>
                </TableCell>
              </TableRow>
            ) : (
              data.list.map((row) => (
                <TableRow key={row.id} hover sx={{ cursor: 'pointer' }} onClick={() => openDetail(row)}>
                  <TableCell sx={{ fontWeight: 700 }}>{row.orderCode}</TableCell>
                  <TableCell>{row.customerName}</TableCell>
                  <TableCell>{TYPE_LABEL[row.type]}</TableCell>
                  <TableCell>{SEVERITY_LABEL[row.severity]}</TableCell>
                  <TableCell>{row.merchantName || '—'}</TableCell>
                  <TableCell>{formatUnix(row.createdOn)}</TableCell>
                  <TableCell>
                    <Chip label={STATUS_META[row.status].label} color={STATUS_META[row.status].color} size="small" sx={{ fontWeight: 600 }} />
                  </TableCell>
                  <TableCell>
                    {row.compensationAmount > 0
                      ? `${money(row.compensationAmount)}${row.compensationStatus === CompensationStatus.Paid ? ' · đã chi' : ' · chờ chi'}`
                      : '—'}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={!!selected} onClose={() => setSelected(null)} maxWidth="md" fullWidth>
        {selected && (
          <>
            <DialogTitle sx={{ fontWeight: 800 }}>
              Khiếu nại đơn {selected.orderCode}{' '}
              <Chip label={STATUS_META[selected.status].label} color={STATUS_META[selected.status].color} size="small" />
            </DialogTitle>
            <DialogContent dividers>
              {dialogError && <Alert severity="error" sx={{ mb: 2 }}>{dialogError}</Alert>}
              <Grid container spacing={2}>
                <Grid item xs={6}><Typography variant="body2"><b>Khách hàng:</b> {selected.customerName}</Typography></Grid>
                <Grid item xs={6}><Typography variant="body2"><b>Merchant:</b> {selected.merchantName || '—'}</Typography></Grid>
                <Grid item xs={6}><Typography variant="body2"><b>Loại sự cố:</b> {TYPE_LABEL[selected.type]}</Typography></Grid>
                <Grid item xs={6}><Typography variant="body2"><b>Mức độ:</b> {SEVERITY_LABEL[selected.severity]}</Typography></Grid>
                <Grid item xs={12}>
                  <Typography variant="body2"><b>Mô tả:</b> {selected.description}</Typography>
                </Grid>
                <Grid item xs={12}>
                  <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
                    {selected.imageUrls.map((url) => (
                      <a key={url} href={toImageUrl(url)} target="_blank" rel="noreferrer">
                        <Box component="img" src={toImageUrl(url)} alt="Ảnh minh chứng"
                          sx={{ width: 120, height: 120, objectFit: 'cover', borderRadius: 1, border: '1px solid', borderColor: 'divider' }} />
                      </a>
                    ))}
                  </Stack>
                </Grid>
              </Grid>

              {selected.status !== IncidentStatus.Pending && (
                <>
                  <Divider sx={{ my: 2 }} />
                  <Typography variant="body2"><b>Kết quả xử lý:</b> {selected.resolutionNote}</Typography>
                  {selected.responsibleParty && (
                    <Typography variant="body2"><b>Bên chịu trách nhiệm:</b> {PARTY_LABEL[selected.responsibleParty]}</Typography>
                  )}
                  {selected.compensationAmount > 0 && (
                    <Typography variant="body2">
                      <b>Bồi thường:</b> {LEVEL_META[selected.compensationLevel].label} × {money(selected.itemValue)} ={' '}
                      <b>{money(selected.compensationAmount)}</b>
                      {selected.compensationStatus === CompensationStatus.Paid && ` · đã chi (mã ${selected.compensationReference})`}
                    </Typography>
                  )}
                  {selected.compensationStatus === CompensationStatus.Pending && (
                    <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
                      <TextField size="small" label="Mã tham chiếu chuyển khoản" value={reference}
                        onChange={(e) => setReference(e.target.value)} sx={{ flexGrow: 1 }} />
                      <Button variant="contained" onClick={() => runAction(() => confirmCompensation(selected.id, reference))}>
                        Xác nhận đã chi trả
                      </Button>
                    </Stack>
                  )}
                </>
              )}

              {selected.status === IncidentStatus.Pending && (
                <>
                  <Divider sx={{ my: 2 }} />
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>Xử lý khiếu nại</Typography>
                  <Grid container spacing={2}>
                    <Grid item xs={6}>
                      <TextField select fullWidth size="small" label="Bên chịu trách nhiệm"
                        value={resolveForm.responsibleParty ?? ''}
                        onChange={(e) => setResolveForm({ ...resolveForm, responsibleParty: Number(e.target.value) as ResponsibleParty })}>
                        {Object.entries(PARTY_LABEL).map(([value, label]) => (
                          <MenuItem key={value} value={Number(value)}>{label}</MenuItem>
                        ))}
                      </TextField>
                    </Grid>
                    <Grid item xs={6}>
                      <TextField select fullWidth size="small" label="Mức bồi thường"
                        value={resolveForm.compensationLevel}
                        onChange={(e) => setResolveForm({ ...resolveForm, compensationLevel: Number(e.target.value) as CompensationLevel })}>
                        {Object.entries(LEVEL_META).map(([value, meta]) => (
                          <MenuItem key={value} value={Number(value)}>{meta.label}</MenuItem>
                        ))}
                      </TextField>
                    </Grid>
                    <Grid item xs={6}>
                      <TextField fullWidth size="small" type="number" label="Giá trị đồ (đ)"
                        disabled={resolveForm.compensationLevel === CompensationLevel.None}
                        value={resolveForm.itemValue}
                        onChange={(e) => setResolveForm({ ...resolveForm, itemValue: Number(e.target.value) })} />
                    </Grid>
                    <Grid item xs={6} sx={{ display: 'flex', alignItems: 'center' }}>
                      <Typography variant="body2">Tiền bồi thường dự kiến: <b>{money(previewAmount)}</b></Typography>
                    </Grid>
                    <Grid item xs={12}>
                      <TextField fullWidth size="small" multiline minRows={2} label="Kết quả xử lý"
                        value={resolveForm.resolutionNote}
                        onChange={(e) => setResolveForm({ ...resolveForm, resolutionNote: e.target.value })} />
                    </Grid>
                    <Grid item xs={12}>
                      <TextField fullWidth size="small" label="Lý do từ chối (nếu khiếu nại không hợp lệ)"
                        value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} />
                    </Grid>
                  </Grid>
                </>
              )}
            </DialogContent>
            <DialogActions sx={{ px: 3, py: 2 }}>
              <Button onClick={() => setSelected(null)}>Hủy</Button>
              {selected.status === IncidentStatus.Pending && (
                <>
                  <Button color="error" onClick={() => runAction(() => rejectIncident(selected.id, rejectReason))}>
                    Từ chối
                  </Button>
                  <Button variant="contained" onClick={() => runAction(() => resolveIncident(selected.id, resolveForm))}>
                    Xác nhận xử lý
                  </Button>
                </>
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
