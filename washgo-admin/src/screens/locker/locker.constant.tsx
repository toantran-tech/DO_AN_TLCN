import React from 'react';
import { Chip, Typography, Button, Box } from '@mui/material';
import VisibilityIcon from '@mui/icons-material/Visibility';
import { Column } from '../../components/table/table.interface';
import { LockerItem } from '../../apis/locker/locker.interface';
import { LockerStatusLabels } from '../../apis/locker/locker.enum';

export const createLockerColumns = (
  onSelectLocker: (row: LockerItem) => void
): Column<LockerItem>[] => [
  {
    id: 'code',
    label: 'Mã trạm',
    minWidth: 120,
    sortable: true,
    render: (row) => (
      <Typography
        variant="body2"
        sx={{ fontWeight: 700, fontFamily: 'monospace', color: 'primary.main', cursor: 'pointer' }}
        onClick={() => onSelectLocker(row)}
      >
        {row.code}
      </Typography>
    ),
  },
  {
    id: 'name',
    label: 'Tên trạm Locker',
    minWidth: 200,
    render: (row) => (
      <Box>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>{row.name}</Typography>
        <Typography variant="caption" color="text.secondary">{row.address}, {row.district}</Typography>
      </Box>
    ),
  },
  {
    id: 'city',
    label: 'Khu vực',
    minWidth: 140,
    render: (row) => `${row.district}, ${row.city}`,
  },
  {
    id: 'totalBoxes',
    label: 'Số ô tủ',
    minWidth: 120,
    align: 'center',
    render: (row) => (
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        {row.boxes?.length || row.totalBoxes || 0} ô
      </Typography>
    ),
  },
  {
    id: 'status',
    label: 'Trạng thái',
    minWidth: 140,
    render: (row) => {
      const s = LockerStatusLabels[row.status] || { label: 'Hoạt động', color: 'success' };
      return <Chip size="small" label={s.label} color={s.color} sx={{ fontWeight: 600 }} />;
    },
  },
  {
    id: 'action',
    label: 'Thao tác',
    minWidth: 100,
    align: 'center',
    render: (row) => (
      <Button
        size="small"
        startIcon={<VisibilityIcon />}
        onClick={() => onSelectLocker(row)}
        sx={{ textTransform: 'none' }}
      >
        Xem ô tủ
      </Button>
    ),
  },
];
