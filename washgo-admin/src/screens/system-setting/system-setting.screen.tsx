import React, { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Grid,
  InputAdornment,
  Paper,
  Snackbar,
  TextField,
  Typography,
} from '@mui/material';
import SaveIcon from '@mui/icons-material/Save';
import {
  SystemSetting,
  getSystemSetting,
  updateCommissionSettings,
  updatePenaltySettings,
  updateSlaSettings,
} from '../../apis/system-setting/system-setting.api';
import { getApiErrorMessage } from '../../common/utils/api-base-response.util';

type FieldDef = { key: keyof SystemSetting; label: string; unit: string };

const SLA_FIELDS: FieldDef[] = [
  { key: 'depositWindowMinutes', label: 'Thời gian gửi đồ tối đa', unit: 'phút' },
  { key: 'merchantCollectSlaHours', label: 'Thời gian Merchant lấy đồ tối đa', unit: 'giờ' },
  { key: 'merchantReturnSlaHours', label: 'Thời gian Merchant trả đồ tối đa', unit: 'giờ' },
  { key: 'freePickupHours', label: 'Thời gian khách lấy đồ miễn phí', unit: 'giờ' },
];

const OVERDUE_FIELDS: FieldDef[] = [
  { key: 'overdueFeeDay1', label: 'Quá hạn 0-24h', unit: 'đ/ngày' },
  { key: 'overdueFeeDay2', label: 'Quá hạn 24-48h', unit: 'đ/ngày' },
  { key: 'overdueFeeDay3', label: 'Quá hạn 48-72h', unit: 'đ/ngày' },
  { key: 'storageFeePerDay', label: 'Sau 72h: chuyển kho, phí lưu kho', unit: 'đ/ngày' },
];

const CANCEL_FIELDS: FieldDef[] = [
  { key: 'cancelLatePendingPercent', label: 'Chưa gửi đồ, quá hạn gửi', unit: '%' },
  { key: 'cancelDepositedPercent', label: 'Đã gửi đồ vào locker', unit: '%' },
  { key: 'cancelCollectedPercent', label: 'Đã về tiệm và cân', unit: '%' },
];

const COMPENSATION_FIELDS: FieldDef[] = [
  { key: 'compensationLightPercent', label: 'Hư nhẹ', unit: '% giá trị đồ' },
  { key: 'compensationMediumPercent', label: 'Hư trung bình', unit: '% giá trị đồ' },
  { key: 'compensationSeverePercent', label: 'Hư nặng', unit: '% giá trị đồ' },
  { key: 'compensationLostPercent', label: 'Mất đồ', unit: '% giá trị đồ' },
];

export const SystemSettingScreen: React.FC = () => {
  const [form, setForm] = useState<SystemSetting | null>(null);
  const [toast, setToast] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);

  const load = useCallback(async () => {
    try {
      const res = await getSystemSetting();
      setForm(res.data);
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Không tải được cấu hình.'), severity: 'error' });
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const save = async (action: () => Promise<{ data: SystemSetting; message?: string }>) => {
    try {
      const res = await action();
      setForm(res.data);
      setToast({ message: res.message || 'Cấu hình thành công!', severity: 'success' });
    } catch (err) {
      setToast({ message: getApiErrorMessage(err, 'Có lỗi xảy ra. Vui lòng thử lại.'), severity: 'error' });
    }
  };

  if (!form) {
    return <Box sx={{ p: 4, textAlign: 'center' }}><CircularProgress size={28} /></Box>;
  }

  const renderFields = (fields: FieldDef[]) => (
    <Grid container spacing={2}>
      {fields.map((f) => (
        <Grid item xs={12} sm={6} key={f.key}>
          <TextField
            fullWidth
            size="small"
            type="number"
            label={f.label}
            value={form[f.key] ?? ''}
            onChange={(e) => setForm({ ...form, [f.key]: Number(e.target.value) })}
            InputProps={{ endAdornment: <InputAdornment position="end">{f.unit}</InputAdornment> }}
          />
        </Grid>
      ))}
    </Grid>
  );

  const section = (title: string, description: string, children: React.ReactNode, onSave: () => void) => (
    <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 2, mb: 2.5 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 2 }}>
        <Box>
          <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>{title}</Typography>
          <Typography variant="body2" color="text.secondary">{description}</Typography>
        </Box>
        <Button variant="contained" startIcon={<SaveIcon />} onClick={onSave}>Lưu</Button>
      </Box>
      {children}
    </Paper>
  );

  return (
    <Box sx={{ p: { xs: 2, md: 3 }, maxWidth: 980 }}>
      <Box sx={{ mb: 2.5 }}>
        <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
          Cài Đặt Hệ Thống
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Thay đổi được áp dụng ngay cho các thao tác sau thời điểm lưu
          {form.updatedAt ? ` · cập nhật lần cuối ${new Date(form.updatedAt).toLocaleString('vi-VN')}` : ''}
        </Typography>
      </Box>

      {section('Cấu hình SLA', 'Thời gian xử lý tối đa cho từng giai đoạn của đơn hàng.', renderFields(SLA_FIELDS), () =>
        save(() => updateSlaSettings({
          depositWindowMinutes: form.depositWindowMinutes,
          merchantCollectSlaHours: form.merchantCollectSlaHours,
          merchantReturnSlaHours: form.merchantReturnSlaHours,
          freePickupHours: form.freePickupHours,
        })))}

      {section('Phí hoa hồng', 'Tỷ lệ mặc định cho Merchant mới không nhập tỷ lệ riêng.',
        renderFields([{ key: 'defaultCommissionRate', label: 'Phí hoa hồng mặc định', unit: '%' }]),
        () => save(() => updateCommissionSettings(form.defaultCommissionRate)))}

      {section('Phí phạt & bồi thường', 'Phí lưu locker quá hạn, phí hủy đơn và mức bồi thường hư hỏng/mất đồ.', (
        <>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>Phí phạt lưu locker quá hạn</Typography>
          {renderFields(OVERDUE_FIELDS)}
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mt: 2.5, mb: 1 }}>Phí hủy đơn (trên giá đơn)</Typography>
          {renderFields(CANCEL_FIELDS)}
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mt: 2.5, mb: 1 }}>Bồi thường hư hỏng / mất đồ</Typography>
          {renderFields(COMPENSATION_FIELDS)}
          <Alert severity="info" sx={{ mt: 2 }}>
            Trong vòng {form.depositWindowMinutes} phút sau khi tạo đơn, khách hủy miễn phí. Mức bồi thường phải tăng dần theo mức độ hư hỏng.
          </Alert>
        </>
      ), () => save(() => updatePenaltySettings({
        overdueFeeDay1: form.overdueFeeDay1,
        overdueFeeDay2: form.overdueFeeDay2,
        overdueFeeDay3: form.overdueFeeDay3,
        storageFeePerDay: form.storageFeePerDay,
        cancelLatePendingPercent: form.cancelLatePendingPercent,
        cancelDepositedPercent: form.cancelDepositedPercent,
        cancelCollectedPercent: form.cancelCollectedPercent,
        compensationLightPercent: form.compensationLightPercent,
        compensationMediumPercent: form.compensationMediumPercent,
        compensationSeverePercent: form.compensationSeverePercent,
        compensationLostPercent: form.compensationLostPercent,
      })))}

      <Snackbar open={!!toast} autoHideDuration={4000} onClose={() => setToast(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}>
        <Alert severity={toast?.severity ?? 'success'} onClose={() => setToast(null)} variant="filled">
          {toast?.message}
        </Alert>
      </Snackbar>
    </Box>
  );
};
