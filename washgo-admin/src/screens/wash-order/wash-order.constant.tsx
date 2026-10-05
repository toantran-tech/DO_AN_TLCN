import React from 'react';
import { Chip, Box, Typography, Button } from '@mui/material';
import VisibilityIcon from '@mui/icons-material/Visibility';
import { Column } from '../../components/table/table.interface';
import { WashOrderRow } from '../../apis/wash-order/wash-order.interface';
import {
  WashOrderStatusLabels,
  PaymentStatusLabels,
} from '../../apis/wash-order/wash-order.enum';

export const createWashOrderColumns = (
  onViewDetail: (row: WashOrderRow) => void
): Column<WashOrderRow>[] => [
  {
    id: 'orderCode',
    label: 'Mã đơn',
    minWidth: 140,
    sortable: true,
    filterable: true,
    render: (row) => (
      <Typography
        variant="body2"
        sx={{
          fontWeight: 700,
          fontFamily: 'monospace',
          color: 'primary.main',
          cursor: 'pointer',
        }}
        onClick={() => onViewDetail(row)}
      >
        {row.orderCode}
      </Typography>
    ),
  },
  {
    id: 'customerName',
    label: 'Khách hàng',
    minWidth: 160,
    sortable: true,
    filterable: true,
    render: (row) => (
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {row.customerName}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {row.customerPhoneNumber}
        </Typography>
      </Box>
    ),
  },
  {
    id: 'lockerName',
    label: 'Tủ & Ô gửi',
    minWidth: 180,
    filterable: true,
    render: (row) => (
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 500 }}>
          {row.lockerName || 'Trạm trung tâm'}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          Ô số: <strong>{row.boxNumber || '-'}</strong>
        </Typography>
      </Box>
    ),
  },
  {
    id: 'finalAmount',
    label: 'Tổng tiền',
    minWidth: 130,
    align: 'right',
    sortable: true,
    render: (row) => (
      <Typography variant="body2" sx={{ fontWeight: 700, color: 'text.primary' }}>
        {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(
          row.finalAmount || 0
        )}
      </Typography>
    ),
  },
  {
    id: 'status',
    label: 'Trạng thái',
    minWidth: 150,
    filterable: true,
    render: (row) => {
      const info = WashOrderStatusLabels[row.status] || { label: 'Không xác định', color: 'default' };
      return (
        <Chip
          size="small"
          label={info.label}
          color={info.color}
          sx={{ fontWeight: 600, fontSize: '0.75rem' }}
        />
      );
    },
  },
  {
    id: 'paymentStatus',
    label: 'Thanh toán',
    minWidth: 140,
    filterable: true,
    render: (row) => {
      const pInfo = PaymentStatusLabels[row.paymentStatus] || { label: 'Chưa rõ', color: 'default' };
      return (
        <Chip
          variant="outlined"
          size="small"
          label={pInfo.label}
          color={pInfo.color}
          sx={{ fontWeight: 600, fontSize: '0.72rem' }}
        />
      );
    },
  },
  {
    id: 'createdOn',
    label: 'Thời gian tạo',
    minWidth: 150,
    sortable: true,
    render: (row) => {
      const date = row.createdOn ? new Date(row.createdOn * 1000) : new Date();
      return (
        <Typography variant="caption" color="text.secondary">
          {date.toLocaleString('vi-VN', {
            hour: '2-digit',
            minute: '2-digit',
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
          })}
        </Typography>
      );
    },
  },
  {
    id: 'actions',
    label: 'Thao tác',
    minWidth: 100,
    align: 'center',
    render: (row) => (
      <Button
        size="small"
        variant="text"
        startIcon={<VisibilityIcon />}
        onClick={() => onViewDetail(row)}
        sx={{ textTransform: 'none', py: 0.5 }}
      >
        Chi tiết
      </Button>
    ),
  },
];
