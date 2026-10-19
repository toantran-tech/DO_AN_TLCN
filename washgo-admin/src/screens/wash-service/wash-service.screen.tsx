import React, { useEffect, useMemo, useState } from 'react';
import { Box, Typography, Button, Grid, Card, CardContent, Paper, TextField, InputAdornment, Tabs, Tab } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import SearchIcon from '@mui/icons-material/Search';
import { filterWashServices } from '../../apis/wash-service/wash-service.api';
import { WashServiceItem, WashServiceType } from '../../apis/wash-service/wash-service.interface';
import { unwrapBaseResponse } from '../../common/utils/api-base-response.util';
import { TableLayered } from '../../components/table/table-layerd.component';
import { WASH_SERVICE_COLUMNS } from './wash-service.constant';

export const WashServiceScreen: React.FC = () => {
  const [services, setServices] = useState<WashServiceItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [tab, setTab] = useState<'all' | 'active' | 'inactive'>('all');

  const mockServices: WashServiceItem[] = [
    {
      id: 'srv-1',
      code: 'SRV-WASH-STD',
      name: 'Giặt Sấy Tiêu Chuẩn (Quần áo hàng ngày)',
      description: 'Giặt sạch bằng nước thơm chuyên dụng, sấy khô tiệt trùng 70°C, gấp gọn gàng',
      unitPrice: 25000,
      unit: 'kg',
      serviceType: WashServiceType.StandardWash,
      estimatedDurationHours: 12,
      isActive: true,
      createdOn: 1790000000,
      modifiedOn: 1790000000,
    },
    {
      id: 'srv-2',
      code: 'SRV-DRY-CLEAN',
      name: 'Giặt Hấp / Giặt Khô Cao Cấp (Suit, Váy dạ hội)',
      description: 'Công nghệ Hydrocarbon thân thiện môi trường, giữ form áo vest và vải nhung/lụa',
      unitPrice: 120000,
      unit: 'bộ',
      serviceType: WashServiceType.DryClean,
      estimatedDurationHours: 24,
      isActive: true,
      createdOn: 1790000000,
      modifiedOn: 1790000000,
    },
    {
      id: 'srv-3',
      code: 'SRV-BLANKET',
      name: 'Giặt Chăn Drap & Mền Lông Lớn',
      description: 'Giặt lồng quay công nghiệp lớn, sấy khô triệt để, khử khuẩn tia UV',
      unitPrice: 75000,
      unit: 'cái',
      serviceType: WashServiceType.BlanketWash,
      estimatedDurationHours: 18,
      isActive: true,
      createdOn: 1790000000,
      modifiedOn: 1790000000,
    },
    {
      id: 'srv-4',
      code: 'SRV-SHOE-SPA',
      name: 'Spa & Vệ Sinh Giày Sneaker Chuyên Sâu',
      description: 'Làm sạch đế, upper, lót giày bằng dung dịch Jason Markk, hấp khử mùi nano bạc',
      unitPrice: 85000,
      unit: 'đôi',
      serviceType: WashServiceType.ShoeCare,
      estimatedDurationHours: 36,
      isActive: true,
      createdOn: 1790000000,
      modifiedOn: 1790000000,
    },
  ];

  const fetchList = async () => {
    try {
      setLoading(true);
      const res = await filterWashServices({ page: 1, take: 20 });
      const resList = unwrapBaseResponse<{ list: WashServiceItem[]; total: number }>(res);
      const items = resList?.list || (Array.isArray(res) ? res : mockServices);
      setServices(items.length > 0 ? items : mockServices);
      setTotal(resList?.total || items.length);
    } catch {
      setServices(mockServices);
      setTotal(mockServices.length);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchList();
  }, []);

  const visibleServices = useMemo(() => services.filter((service) => {
    const matchesKeyword = [service.code, service.name, service.description].some((value) => value?.toLowerCase().includes(keyword.trim().toLowerCase()));
    const matchesTab = tab === 'all' || (tab === 'active' ? service.isActive : !service.isActive);
    return matchesKeyword && matchesTab;
  }), [services, keyword, tab]);

  return (
    <Box sx={{ p: { xs: 1.5, sm: 2.5, lg: 3 }, maxWidth: 1500, mx: 'auto' }}>
      <Box sx={{ mb: 2.25, display: 'flex', justifyContent: 'space-between', alignItems: { xs: 'flex-start', sm: 'center' }, flexDirection: { xs: 'column', sm: 'row' }, gap: 1.5 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
            Quản lý Dịch vụ
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Thiết lập bảng giá, thời gian cam kết SLA và quy chuẩn giặt sấy cho toàn hệ sinh thái
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />}>
          Thêm gói dịch vụ
        </Button>
      </Box>

      <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
        {[
          { label: 'Tổng dịch vụ', value: total || services.length, tone: 'primary' as const },
          { label: 'Đã duyệt', value: services.filter((service) => service.isActive).length, tone: 'success' as const },
          { label: 'Chờ duyệt', value: services.filter((service) => !service.isActive).length, tone: 'warning' as const },
          { label: 'Từ chối', value: 0, tone: 'error' as const },
        ].map((stat) => (
          <Grid item xs={6} lg={3} key={stat.label}>
            <Card variant="outlined" sx={{ borderColor: 'divider' }}>
              <CardContent sx={{ py: '14px !important' }}>
                <Typography variant="caption" color="text.secondary">{stat.label}</Typography>
                <Typography variant="h6" fontWeight={800} color={`${stat.tone}.main`}>{stat.value}</Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      <Paper variant="outlined" sx={{ p: { xs: 1.25, sm: 2 }, borderRadius: 2, borderColor: '#e7ebf0' }}>
        <Box sx={{ display: 'flex', alignItems: { xs: 'stretch', md: 'center' }, flexDirection: { xs: 'column', md: 'row' }, justifyContent: 'space-between', gap: 1.5, mb: 1.5 }}>
          <Tabs value={tab} onChange={(_, value) => setTab(value)} sx={{ minHeight: 38, '& .MuiTab-root': { minHeight: 38, textTransform: 'none', fontWeight: 700 } }}>
            <Tab value="all" label="Tất cả" /><Tab value="inactive" label="Chờ duyệt" /><Tab value="active" label="Đã duyệt" />
          </Tabs>
          <TextField size="small" value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder="Tìm tên dịch vụ, merchant..." sx={{ width: { xs: '100%', md: 340 } }} InputProps={{ startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }} />
        </Box>
        <TableLayered rows={visibleServices} columns={WASH_SERVICE_COLUMNS} loading={loading} total={visibleServices.length} page={1} take={20} />
      </Paper>
    </Box>
  );
};
