import React, { useEffect, useState, useCallback } from 'react';
import {
  Box,
  Typography,
  Button,
  Stack,
  Chip,
  TextField,
  InputAdornment,
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
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import SearchIcon from '@mui/icons-material/Search';
import RefreshIcon from '@mui/icons-material/Refresh';
import StorefrontIcon from '@mui/icons-material/Storefront';
import StarIcon from '@mui/icons-material/Star';
import {
  filterMerchants,
  createMerchant,
  updateMerchantStatus,
} from '../../apis/merchant/merchant.api';
import {
  MerchantItem,
  CreateMerchantDto,
  MerchantStatus,
} from '../../apis/merchant/merchant.interface';
import { unwrapBaseResponse } from '../../common/utils/api-base-response.util';

export const MerchantScreen: React.FC = () => {
  const [data, setData] = useState<{ list: MerchantItem[]; total: number }>({ list: [], total: 0 });
  const [loading, setLoading] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const [formData, setFormData] = useState<CreateMerchantDto>({
    userId: 'd3333333-3333-3333-3333-333333333333',
    businessName: '',
    address: '',
    district: 'Quận 1',
    city: 'TP. Hồ Chí Minh',
    latitude: 10.7769,
    longitude: 106.7009,
    phone: '',
    depositAmount: 1000000,
    commissionRate: 20,
    serviceRadiusKm: 5,
    maxCapacity: 50,
  });

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const res = await filterMerchants({ page: 1, take: 50, keyword });
      const resList = unwrapBaseResponse<{ list: MerchantItem[]; total: number }>(res);
      setData({
        list: resList?.list || (Array.isArray(res) ? res : []),
        total: resList?.total || (res as any)?.total || 0,
      });
    } catch {
      // Fallback
      setData({
        list: [
          {
            id: 'd3333333-3333-3333-3333-333333333333',
            businessName: 'Cửa Hàng Giặt Sấy WashGo Q1',
            address: '45 Lê Thánh Tôn',
            district: 'Quận 1',
            city: 'TP. Hồ Chí Minh',
            latitude: 10.7769,
            longitude: 106.7009,
            phone: '0903000003',
            status: MerchantStatus.Active,
            rating: 5.0,
            totalOrders: 15,
            totalRevenue: 3500000,
            depositAmount: 1000000,
            commissionRate: 20,
            serviceRadiusKm: 5,
            currentWorkload: 8,
            maxCapacity: 50,
            createdOn: 1728130000,
            serviceAreaIds: ['e1111111-1111-1111-1111-111111111111'],
          },
        ],
        total: 1,
      });
    } finally {
      setLoading(false);
    }
  }, [keyword]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleCreate = async () => {
    try {
      await createMerchant(formData);
      setCreateOpen(false);
      fetchData();
    } catch (err) {
      console.error(err);
    }
  };

  const handleToggleStatus = async (merchant: MerchantItem) => {
    try {
      const nextStatus =
        merchant.status === MerchantStatus.Active ? MerchantStatus.Inactive : MerchantStatus.Active;
      await updateMerchantStatus({
        merchantId: merchant.id,
        status: nextStatus,
      });
      fetchData();
    } catch (err) {
      console.error(err);
    }
  };

  const renderStatus = (status: MerchantStatus) => {
    switch (status) {
      case MerchantStatus.Active:
        return <Chip label="Đang hoạt động" color="success" size="small" sx={{ fontWeight: 600 }} />;
      case MerchantStatus.Inactive:
        return <Chip label="Tạm dừng" color="default" size="small" sx={{ fontWeight: 600 }} />;
      case MerchantStatus.Suspended:
        return <Chip label="Bị khóa" color="error" size="small" sx={{ fontWeight: 600 }} />;
      default:
        return <Chip label="Chờ duyệt" color="warning" size="small" sx={{ fontWeight: 600 }} />;
    }
  };

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Box sx={{ mb: 2.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
            Quản Lý Đối Tác Giặt Sấy (Merchants)
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Danh sách cửa hàng, tiệm giặt liên kết xử lý đơn hàng theo khu vực
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" startIcon={<RefreshIcon />} onClick={fetchData}>
            Làm mới
          </Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
            Thêm đối tác
          </Button>
        </Stack>
      </Box>

      {/* Search */}
      <Box sx={{ mb: 2, display: 'flex', gap: 2 }}>
        <TextField
          size="small"
          placeholder="Tìm theo tên cửa hàng, số điện thoại, địa chỉ..."
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          sx={{ width: 360 }}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" />
              </InputAdornment>
            ),
          }}
        />
      </Box>

      {/* Table */}
      <TableContainer component={Paper} elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
        <Table size="medium">
          <TableHead sx={{ backgroundColor: 'action.hover' }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>Tên cửa hàng</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Địa chỉ / Khu vực</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Số điện thoại</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Đánh giá</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Tải hiện tại</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Trạng thái</TableCell>
              <TableCell align="right" sx={{ fontWeight: 700 }}>Thao tác</TableCell>
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
                  <Typography variant="body2" color="text.secondary">Chưa có đối tác giặt sấy nào</Typography>
                </TableCell>
              </TableRow>
            ) : (
              data.list.map((row) => (
                <TableRow key={row.id} hover>
                  <TableCell sx={{ fontWeight: 600 }}>
                    <Stack direction="row" alignItems="center" spacing={1}>
                      <StorefrontIcon color="primary" />
                      <span>{row.businessName}</span>
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{row.address}</Typography>
                    <Typography variant="caption" color="text.secondary">{row.district}, {row.city}</Typography>
                  </TableCell>
                  <TableCell>{row.phone}</TableCell>
                  <TableCell>
                    <Stack direction="row" alignItems="center" spacing={0.5}>
                      <StarIcon sx={{ fontSize: 16, color: '#f59e0b' }} />
                      <Typography variant="body2" sx={{ fontWeight: 700 }}>{row.rating || 5.0}</Typography>
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {row.currentWorkload || 0} / {row.maxCapacity || 50} đơn
                    </Typography>
                  </TableCell>
                  <TableCell>{renderStatus(row.status)}</TableCell>
                  <TableCell align="right">
                    <Button
                      size="small"
                      variant="outlined"
                      color={row.status === MerchantStatus.Active ? 'warning' : 'success'}
                      onClick={() => handleToggleStatus(row)}
                    >
                      {row.status === MerchantStatus.Active ? 'Tạm dừng' : 'Kích hoạt'}
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create Dialog */}
      <Dialog open={createOpen} onClose={() => setCreateOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle sx={{ fontWeight: 800 }}>Đăng Ký Đối Tác Giặt Sấy Mới</DialogTitle>
        <DialogContent dividers>
          <Grid container spacing={2} sx={{ pt: 1 }}>
            <Grid item xs={12}>
              <TextField
                fullWidth
                label="Tên tiệm giặt / Doanh nghiệp"
                size="small"
                value={formData.businessName}
                onChange={(e) => setFormData({ ...formData, businessName: e.target.value })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                fullWidth
                label="Số điện thoại liên hệ"
                size="small"
                value={formData.phone}
                onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                fullWidth
                label="Công suất tối đa (đơn/ngày)"
                type="number"
                size="small"
                value={formData.maxCapacity}
                onChange={(e) => setFormData({ ...formData, maxCapacity: Number(e.target.value) })}
              />
            </Grid>
            <Grid item xs={12}>
              <TextField
                fullWidth
                label="Địa chỉ chi tiết"
                size="small"
                value={formData.address}
                onChange={(e) => setFormData({ ...formData, address: e.target.value })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                fullWidth
                label="Quận / Huyện"
                size="small"
                value={formData.district}
                onChange={(e) => setFormData({ ...formData, district: e.target.value })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                fullWidth
                label="Tỉnh / Thành phố"
                size="small"
                value={formData.city}
                onChange={(e) => setFormData({ ...formData, city: e.target.value })}
              />
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setCreateOpen(false)}>Hủy</Button>
          <Button variant="contained" onClick={handleCreate}>Tạo đối tác</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};
