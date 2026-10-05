import React from 'react';
import { Chip, Typography, Box } from '@mui/material';
import { Column } from '../../components/table/table.interface';
import { WashServiceItem, WashServiceTypeLabels } from '../../apis/wash-service/wash-service.interface';

export const WASH_SERVICE_COLUMNS: Column<WashServiceItem>[] = [
  {
    id: 'code',
    label: 'Mã dịch vụ',
    minWidth: 120,
    sortable: true,
    render: (row) => (
      <Typography variant="body2" sx={{ fontWeight: 700, fontFamily: 'monospace', color: 'primary.main' }}>
        {row.code}
      </Typography>
    ),
  },
  {
    id: 'name',
    label: 'Tên dịch vụ',
    minWidth: 200,
    render: (row) => (
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>{row.name}</Typography>
        {row.description && <Typography variant="caption" color="text.secondary">{row.description}</Typography>}
      </Box>
    ),
  },
  {
    id: 'serviceType',
    label: 'Phân loại',
    minWidth: 180,
    render: (row) => WashServiceTypeLabels[row.serviceType] || 'Tiêu chuẩn',
  },
  {
    id: 'unitPrice',
    label: 'Đơn giá',
    minWidth: 130,
    align: 'right',
    sortable: true,
    render: (row) => (
      <Typography variant="body2" sx={{ fontWeight: 700, color: 'text.primary' }}>
        {new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(row.unitPrice)} / {row.unit}
      </Typography>
    ),
  },
  {
    id: 'estimatedDurationHours',
    label: 'Thời gian SLA',
    minWidth: 120,
    align: 'center',
    render: (row) => `${row.estimatedDurationHours} giờ`,
  },
  {
    id: 'isActive',
    label: 'Trạng thái',
    minWidth: 120,
    render: (row) => (
      <Chip
        size="small"
        label={row.isActive ? 'Đang mở' : 'Tạm ẩn'}
        color={row.isActive ? 'success' : 'default'}
        sx={{ fontWeight: 600 }}
      />
    ),
  },
];
