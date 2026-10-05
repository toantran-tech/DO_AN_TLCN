import React, { useEffect, useState } from 'react';
import { Box, Typography, Button } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { TableLayered } from '../../components/table/table-layerd.component';
import { filterWashServices } from '../../apis/wash-service/wash-service.api';
import { WashServiceItem, WashServiceType } from '../../apis/wash-service/wash-service.interface';
import { WASH_SERVICE_COLUMNS } from './wash-service.constant';
import { unwrapBaseResponse } from '../../common/utils/api-base-response.util';

export const WashServiceScreen: React.FC = () => {
  const [services, setServices] = useState<WashServiceItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);

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

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Box sx={{ mb: 2.5, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
            Danh Mục Gói Dịch Vụ WashGo
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Thiết lập bảng giá, thời gian cam kết SLA và quy chuẩn giặt sấy cho toàn hệ sinh thái
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />}>
          Thêm gói dịch vụ
        </Button>
      </Box>

      <TableLayered
        rows={services}
        columns={WASH_SERVICE_COLUMNS}
        loading={loading}
        total={total}
        page={1}
        take={20}
      />
    </Box>
  );
};
