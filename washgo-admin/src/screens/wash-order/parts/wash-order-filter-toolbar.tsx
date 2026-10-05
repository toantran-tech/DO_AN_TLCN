import React from 'react';
import {
  Box,
  TextField,
  InputAdornment,
  Button,
  Tabs,
  Tab,
  Stack,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import RefreshIcon from '@mui/icons-material/Refresh';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import { WashOrderStatus } from '../../../apis/wash-order/wash-order.enum';

interface WashOrderFilterToolbarProps {
  keyword: string;
  onKeywordChange: (val: string) => void;
  status?: WashOrderStatus;
  onStatusChange: (status?: WashOrderStatus) => void;
  onRefresh: () => void;
  onReset: () => void;
}

export const WashOrderFilterToolbar: React.FC<WashOrderFilterToolbarProps> = ({
  keyword,
  onKeywordChange,
  status,
  onStatusChange,
  onRefresh,
  onReset,
}) => {
  return (
    <Box sx={{ mb: 2 }}>
      {/* Status quick tabs */}
      <Tabs
        value={status || 0}
        onChange={(_, val) => onStatusChange(val === 0 ? undefined : val)}
        variant="scrollable"
        scrollButtons="auto"
        sx={{
          mb: 1.5,
          borderBottom: 1,
          borderColor: 'divider',
          '& .MuiTab-root': { textTransform: 'none', fontWeight: 600, minHeight: 42 },
        }}
      >
        <Tab label="Tất cả đơn" value={0} />
        <Tab label="Chờ gửi tủ" value={WashOrderStatus.Pending} />
        <Tab label="Đã gửi tủ" value={WashOrderStatus.Deposited} />
        <Tab label="Đang giặt" value={WashOrderStatus.Washing} />
        <Tab label="Chờ khách lấy" value={WashOrderStatus.Returned} />
        <Tab label="Đã hoàn tất" value={WashOrderStatus.Completed} />
      </Tabs>

      {/* Search and Action buttons */}
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} alignItems="center">
        <TextField
          size="small"
          placeholder="Tìm theo mã đơn, tên hoặc SĐT khách hàng..."
          value={keyword}
          onChange={(e) => onKeywordChange(e.target.value)}
          sx={{ flex: 1, minWidth: 260 }}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon sx={{ color: 'text.secondary', fontSize: 20 }} />
              </InputAdornment>
            ),
          }}
        />

        <Stack direction="row" spacing={1} sx={{ width: { xs: '100%', sm: 'auto' } }}>
          <Button
            variant="outlined"
            size="small"
            startIcon={<FilterListOffIcon />}
            onClick={onReset}
            sx={{ textTransform: 'none' }}
          >
            Đặt lại
          </Button>
          <Button
            variant="contained"
            size="small"
            startIcon={<RefreshIcon />}
            onClick={onRefresh}
            sx={{ textTransform: 'none' }}
          >
            Làm mới
          </Button>
        </Stack>
      </Stack>
    </Box>
  );
};
