import React, { useEffect, useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Box,
  Typography,
  Grid,
  Chip,
  Divider,
  Paper,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  CircularProgress,
  Stack,
  Alert,
} from '@mui/material';
import QrCode2Icon from '@mui/icons-material/QrCode2';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import { getWashOrderDetail, updateWashOrderStatus } from '../../../apis/wash-order/wash-order.api';
import { WashOrderDetail } from '../../../apis/wash-order/wash-order.interface';
import {
  WashOrderStatus,
  WashOrderStatusLabels,
  PaymentStatusLabels,
} from '../../../apis/wash-order/wash-order.enum';
import { unwrapBaseResponse } from '../../../common/utils/api-base-response.util';

interface WashOrderDetailDialogProps {
  orderId: string | null;
  open: boolean;
  onClose: () => void;
  onStatusUpdated?: () => void;
}

export const WashOrderDetailDialog: React.FC<WashOrderDetailDialogProps> = ({
  orderId,
  open,
  onClose,
  onStatusUpdated,
}) => {
  const [detail, setDetail] = useState<WashOrderDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);

  useEffect(() => {
    if (open && orderId) {
      setLoading(true);
      getWashOrderDetail(orderId)
        .then((res) => {
          const data = unwrapBaseResponse<WashOrderDetail>(res);
          setDetail(data);
        })
        .catch(() => setDetail(null))
        .finally(() => setLoading(false));
    } else {
      setDetail(null);
    }
  }, [open, orderId]);

  const handleNextStatus = async () => {
    if (!detail) return;
    try {
      setActionLoading(true);
      const nextStatusMap: Partial<Record<WashOrderStatus, WashOrderStatus>> = {
        [WashOrderStatus.Pending]: WashOrderStatus.Deposited,
        [WashOrderStatus.Deposited]: WashOrderStatus.Collected,
        [WashOrderStatus.Collected]: WashOrderStatus.Washing,
        [WashOrderStatus.Washing]: WashOrderStatus.Drying,
        [WashOrderStatus.Drying]: WashOrderStatus.Ironing,
        [WashOrderStatus.Ironing]: WashOrderStatus.Folding,
        [WashOrderStatus.Folding]: WashOrderStatus.AwaitingPayment,
        [WashOrderStatus.AwaitingPayment]: WashOrderStatus.Paid,
        [WashOrderStatus.Paid]: WashOrderStatus.ReadyToReturn,
        [WashOrderStatus.ReadyToReturn]: WashOrderStatus.Returned,
        [WashOrderStatus.Returned]: WashOrderStatus.Completed,
      };

      const next = nextStatusMap[detail.status];
      if (!next) return;

      await updateWashOrderStatus({
        orderId: detail.id,
        newStatus: next,
        note: `Chuyển trạng thái bởi Admin`,
      });

      // Reload
      const res = await getWashOrderDetail(detail.id);
      setDetail(unwrapBaseResponse<WashOrderDetail>(res));
      if (onStatusUpdated) onStatusUpdated();
    } finally {
      setActionLoading(false);
    }
  };

  const statusInfo = detail ? WashOrderStatusLabels[detail.status] : null;
  const payInfo = detail ? PaymentStatusLabels[detail.paymentStatus] : null;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ pb: 1 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 800 }}>
              Chi tiết Đơn hàng: {detail?.orderCode || 'Đang tải...'}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              ID: {detail?.id}
            </Typography>
          </Box>
          {statusInfo && payInfo && (
            <Stack direction="row" spacing={1}>
              <Chip size="small" label={statusInfo.label} color={statusInfo.color} sx={{ fontWeight: 600 }} />
              <Chip size="small" variant="outlined" label={payInfo.label} color={payInfo.color} sx={{ fontWeight: 600 }} />
            </Stack>
          )}
        </Box>
      </DialogTitle>

      <DialogContent dividers sx={{ minHeight: 350 }}>
        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
            <CircularProgress />
          </Box>
        ) : !detail ? (
          <Alert severity="error">Không tìm thấy thông tin đơn hàng này.</Alert>
        ) : (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
            {/* Customer & Locker info */}
            <Grid container spacing={2}>
              <Grid item xs={12} sm={6}>
                <Paper variant="outlined" sx={{ p: 2, borderRadius: 2 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1, color: 'primary.main' }}>
                    👤 Khách hàng
                  </Typography>
                  <Typography variant="body2"><strong>Họ tên:</strong> {detail.customerName}</Typography>
                  <Typography variant="body2"><strong>Số điện thoại:</strong> {detail.customerPhoneNumber}</Typography>
                  {detail.customerNote && (
                    <Typography variant="body2" sx={{ mt: 0.5, fontStyle: 'italic', color: 'text.secondary' }}>
                      Ghi chú: "{detail.customerNote}"
                    </Typography>
                  )}
                </Paper>
              </Grid>

              <Grid item xs={12} sm={6}>
                <Paper variant="outlined" sx={{ p: 2, borderRadius: 2 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1, color: 'primary.main' }}>
                    🏢 Trạm Locker & Ô gửi
                  </Typography>
                  <Typography variant="body2"><strong>Trạm:</strong> {detail.lockerName || 'Trạm Trung Tâm'}</Typography>
                  <Typography variant="body2"><strong>Ô tủ gửi:</strong> Ô số {detail.boxNumber}</Typography>
                  <Box sx={{ mt: 1, p: 1, backgroundColor: 'action.hover', borderRadius: 1.5, display: 'flex', gap: 2 }}>
                    <Box>
                      <Typography variant="caption" color="text.secondary">Mã PIN gửi đồ:</Typography>
                      <Typography variant="subtitle2" sx={{ fontFamily: 'monospace', fontWeight: 700 }}>
                        {detail.depositPinCode || '------'}
                      </Typography>
                    </Box>
                    <Box>
                      <Typography variant="caption" color="text.secondary">Mã PIN nhận đồ:</Typography>
                      <Typography variant="subtitle2" sx={{ fontFamily: 'monospace', fontWeight: 700 }}>
                        {detail.pickupPinCode || '------'}
                      </Typography>
                    </Box>
                  </Box>
                </Paper>
              </Grid>
            </Grid>

            {/* QR Code preview */}
            <Paper variant="outlined" sx={{ p: 1.5, borderRadius: 2, display: 'flex', alignItems: 'center', gap: 2 }}>
              <QrCode2Icon sx={{ fontSize: 44, color: 'text.secondary' }} />
              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                  Chuỗi dữ liệu QR Code quét mở khóa Locker:
                </Typography>
                <Typography variant="caption" sx={{ fontFamily: 'monospace', color: 'text.secondary' }}>
                  {detail.qrCodeString || `WASHGO_ORDER:${detail.orderCode}`}
                </Typography>
              </Box>
            </Paper>

            {/* Services Table */}
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                🧺 Chi tiết Gói dịch vụ đã chọn
              </Typography>
              <Table size="small" sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
                <TableHead sx={{ backgroundColor: 'action.hover' }}>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>Tên dịch vụ</TableCell>
                    <TableCell align="center" sx={{ fontWeight: 700 }}>Số lượng</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 700 }}>Đơn giá</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 700 }}>Thành tiền</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {detail.items && detail.items.length > 0 ? (
                    detail.items.map((item, i) => (
                      <TableRow key={i}>
                        <TableCell>{item.serviceName}</TableCell>
                        <TableCell align="center">{item.quantity}</TableCell>
                        <TableCell align="right">
                          {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(item.unitPrice)}
                        </TableCell>
                        <TableCell align="right" sx={{ fontWeight: 600 }}>
                          {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(item.subTotal)}
                        </TableCell>
                      </TableRow>
                    ))
                  ) : (
                    <TableRow>
                      <TableCell colSpan={4} align="center" sx={{ color: 'text.secondary' }}>
                        Không có dịch vụ đính kèm
                      </TableCell>
                    </TableRow>
                  )}
                  <TableRow>
                    <TableCell colSpan={3} align="right" sx={{ fontWeight: 700 }}>
                      Tổng thanh toán:
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 800, color: 'primary.main', fontSize: '1rem' }}>
                      {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(detail.finalAmount)}
                    </TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </Box>

            {/* Timeline */}
            <Box>
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
                🕒 Lịch sử chuyển trạng thái (Audit Timeline)
              </Typography>
              <Paper variant="outlined" sx={{ p: 2, borderRadius: 2 }}>
                {detail.timeline && detail.timeline.length > 0 ? (
                  <Stack spacing={1.5}>
                    {detail.timeline.map((t, i) => {
                      const date = new Date(t.timestamp * 1000);
                      const tInfo = WashOrderStatusLabels[t.status] || { label: 'Cập nhật' };
                      return (
                        <Box key={i} sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
                          <CheckCircleOutlineIcon sx={{ fontSize: 18, color: 'success.main' }} />
                          <Box sx={{ flex: 1 }}>
                            <Typography variant="body2" sx={{ fontWeight: 600 }}>
                              {tInfo.label} — {t.note || 'Thao tác hệ thống'}
                            </Typography>
                            <Typography variant="caption" color="text.secondary">
                              Bởi: {t.actorName || t.actorRole || 'Hệ thống'} • {date.toLocaleString('vi-VN')}
                            </Typography>
                          </Box>
                        </Box>
                      );
                    })}
                  </Stack>
                ) : (
                  <Typography variant="body2" color="text.secondary">Chưa có lịch sử trạng thái</Typography>
                )}
              </Paper>
            </Box>
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ p: 2 }}>
        {detail && detail.status !== WashOrderStatus.Completed && detail.status !== WashOrderStatus.Cancelled && (
          <Button
            variant="contained"
            color="success"
            disabled={actionLoading}
            onClick={handleNextStatus}
          >
            Chuyển bước tiếp theo
          </Button>
        )}
        <Button onClick={onClose}>Đóng</Button>
      </DialogActions>
    </Dialog>
  );
};
