import React, { useEffect, useState, useMemo } from 'react';
import {
  Box,
  Typography,
  Grid,
  Card,
  CardContent,
  Chip,
  Button,
  Stack,
  Divider,
  TextField,
  InputAdornment,
  Paper,
} from '@mui/material';
import MeetingRoomIcon from '@mui/icons-material/MeetingRoom';
import AddIcon from '@mui/icons-material/Add';
import SearchIcon from '@mui/icons-material/Search';
import { TableLayered } from '../../components/table/table-layerd.component';
import { filterLockers } from '../../apis/locker/locker.api';
import { LockerItem, LockerBoxItem } from '../../apis/locker/locker.interface';
import { LockerBoxStatus, LockerBoxStatusLabels, LockerStatus } from '../../apis/locker/locker.enum';
import { createLockerColumns } from './locker.constant';
import { unwrapBaseResponse } from '../../common/utils/api-base-response.util';

export const LockerScreen: React.FC = () => {
  const [lockers, setLockers] = useState<LockerItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [selectedLocker, setSelectedLocker] = useState<LockerItem | null>(null);
  const [keyword, setKeyword] = useState('');

  const mockLockers: LockerItem[] = [
    {
      id: 'lock-1',
      code: 'LCK-VHM-01',
      name: 'Locker Vinhome Central Park - T3',
      address: '208 Nguyễn Hữu Cảnh',
      district: 'Bình Thạnh',
      city: 'TP. Hồ Chí Minh',
      latitude: 10.793,
      longitude: 106.721,
      status: 1,
      totalBoxes: 12,
      emptyBoxesCount: 8,
      createdOn: 1790000000,
      modifiedOn: 1790000000,
      boxes: [
        { id: 'b-1', lockerId: 'lock-1', boxNumber: 'A-01', size: 1, status: LockerBoxStatus.Available },
        { id: 'b-2', lockerId: 'lock-1', boxNumber: 'A-02', size: 2, status: LockerBoxStatus.Occupied, currentOrderId: 'ord-101' },
        { id: 'b-3', lockerId: 'lock-1', boxNumber: 'A-03', size: 2, status: LockerBoxStatus.Available },
        { id: 'b-4', lockerId: 'lock-1', boxNumber: 'A-04', size: 3, status: LockerBoxStatus.Reserved },
        { id: 'b-5', lockerId: 'lock-1', boxNumber: 'B-01', size: 2, status: LockerBoxStatus.Available },
        { id: 'b-6', lockerId: 'lock-1', boxNumber: 'B-02', size: 2, status: LockerBoxStatus.Available },
        { id: 'b-7', lockerId: 'lock-1', boxNumber: 'B-03', size: 3, status: LockerBoxStatus.Occupied },
        { id: 'b-8', lockerId: 'lock-1', boxNumber: 'B-04', size: 2, status: LockerBoxStatus.Available },
        { id: 'b-9', lockerId: 'lock-1', boxNumber: 'C-01', size: 1, status: LockerBoxStatus.Available },
        { id: 'b-10', lockerId: 'lock-1', boxNumber: 'C-02', size: 2, status: LockerBoxStatus.Maintenance },
        { id: 'b-11', lockerId: 'lock-1', boxNumber: 'C-03', size: 2, status: LockerBoxStatus.Available },
        { id: 'b-12', lockerId: 'lock-1', boxNumber: 'C-04', size: 3, status: LockerBoxStatus.Available },
      ],
    },
    {
      id: 'lock-2',
      code: 'LCK-MAS-02',
      name: 'Locker Masteri Thảo Điền - T1',
      address: '159 Xa lộ Hà Nội',
      district: 'TP. Thủ Đức',
      city: 'TP. Hồ Chí Minh',
      latitude: 10.801,
      longitude: 106.741,
      status: 1,
      totalBoxes: 8,
      emptyBoxesCount: 5,
      createdOn: 1790000000,
      modifiedOn: 1790000000,
      boxes: [
        { id: 'b-21', lockerId: 'lock-2', boxNumber: 'M-01', size: 2, status: LockerBoxStatus.Available },
        { id: 'b-22', lockerId: 'lock-2', boxNumber: 'M-02', size: 2, status: LockerBoxStatus.Available },
        { id: 'b-23', lockerId: 'lock-2', boxNumber: 'M-03', size: 3, status: LockerBoxStatus.Occupied },
        { id: 'b-24', lockerId: 'lock-2', boxNumber: 'M-04', size: 2, status: LockerBoxStatus.Available },
      ],
    },
  ];

  const fetchList = async () => {
    try {
      setLoading(true);
      const res = await filterLockers({ page: 1, take: 20 });
      const resList = unwrapBaseResponse<{ list: LockerItem[]; total: number }>(res);
      const items = resList?.list || (Array.isArray(res) ? res : mockLockers);
      setLockers(items.length > 0 ? items : mockLockers);
      setTotal(resList?.total || items.length);
      setSelectedLocker(items[0] || mockLockers[0]);
    } catch {
      setLockers(mockLockers);
      setTotal(mockLockers.length);
      setSelectedLocker(mockLockers[0]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchList();
  }, []);

  const columns = useMemo(() => createLockerColumns((row) => setSelectedLocker(row)), []);
  const totalBoxes = lockers.reduce((sum, locker) => sum + (locker.totalBoxes || locker.boxes?.length || 0), 0);
  const availableBoxes = lockers.reduce((sum, locker) => sum + (locker.emptyBoxesCount || 0), 0);
  const activeLockers = lockers.filter((locker) => locker.status === LockerStatus.Active).length;
  const visibleLockers = useMemo(() => {
    const query = keyword.trim().toLowerCase();
    if (!query) return lockers;
    return lockers.filter((locker) => [locker.code, locker.name, locker.address, locker.district, locker.city].some((value) => value?.toLowerCase().includes(query)));
  }, [keyword, lockers]);

  return (
    <Box sx={{ p: { xs: 1.5, sm: 2.5, lg: 3 }, maxWidth: 1500, mx: 'auto' }}>
      <Box sx={{ mb: 2.25, display: 'flex', justifyContent: 'space-between', alignItems: { xs: 'flex-start', sm: 'center' }, flexDirection: { xs: 'column', sm: 'row' }, gap: 1.5 }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
            Quản lý Locker
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Giám sát danh sách trạm tủ và trạng thái từng ô tủ gửi/nhận thời gian thực
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />}>
          Thêm trạm Locker
        </Button>
      </Box>

      <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
        {[
          { title: 'Tổng trạm locker', value: total, icon: '▦', tint: '#eef4ff', color: '#2563eb' },
          { title: 'Đang hoạt động', value: activeLockers, icon: '●', tint: '#eaf8ef', color: '#16a34a' },
          { title: 'Đang bảo trì', value: lockers.filter((locker) => locker.status === LockerStatus.Maintenance).length, icon: '⚒', tint: '#fff4e8', color: '#f97316' },
          { title: 'Tổng ô còn trống', value: availableBoxes || Math.max(0, totalBoxes - lockers.reduce((sum, locker) => sum + (locker.boxes?.filter((box) => box.status !== LockerBoxStatus.Available).length || 0), 0)), icon: '▣', tint: '#f5efff', color: '#8b5cf6' },
        ].map((stat) => (
          <Grid item xs={6} md={3} key={stat.title}>
            <Card variant="outlined" sx={{ borderColor: 'divider' }}>
              <CardContent sx={{ py: '14px !important', display: 'flex', alignItems: 'center', gap: 1.5 }}>
                <Box sx={{ width: 38, height: 38, borderRadius: 2, bgcolor: stat.tint, color: stat.color, display: 'grid', placeItems: 'center', fontWeight: 800 }}>{stat.icon}</Box>
                <Box><Typography variant="caption" color="text.secondary">{stat.title}</Typography><Typography variant="h6" fontWeight={800}>{stat.value}</Typography></Box>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      <Grid container spacing={2}>
        {/* Left: Locker Table */}
        <Grid item xs={12}>
          <Paper variant="outlined" sx={{ p: { xs: 1.25, sm: 2 }, borderRadius: 2, borderColor: '#e7ebf0' }}>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ mb: 1.5 }}>
              <TextField size="small" value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder="Tìm kiếm tên locker, địa chỉ..." sx={{ flex: 1, maxWidth: { sm: 420 } }} InputProps={{ startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }} />
              <Button variant="outlined" color="inherit">Tất cả khu vực</Button>
              <Button variant="outlined" color="inherit">Tất cả trạng thái</Button>
            </Stack>
            <TableLayered
              rows={visibleLockers}
              columns={columns}
              loading={loading}
              total={visibleLockers.length}
              page={1}
              take={20}
            />
          </Paper>
        </Grid>

        {/* Right: Visual Box Layout of Selected Locker */}
        <Grid item xs={12}>
          <Card elevation={0} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 2.5, p: 2 }}>
            <CardContent sx={{ p: 1 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                <MeetingRoomIcon color="primary" />
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  {selectedLocker?.name || 'Chọn một trạm tủ'}
                </Typography>
              </Box>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                Mã trạm: <strong>{selectedLocker?.code}</strong> • Địa chỉ: {selectedLocker?.address}
              </Typography>

              {/* Status legend */}
              <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: 'wrap', gap: 1 }}>
                <Chip size="small" label="Trống (Sẵn sàng)" color="success" />
                <Chip size="small" label="Có đồ" color="error" />
                <Chip size="small" label="Đã giữ chỗ" color="warning" />
                <Chip size="small" label="Bảo trì" color="default" />
              </Stack>

              <Divider sx={{ mb: 2 }} />

              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
                Sơ đồ bố trí ô tủ (Compartments):
              </Typography>

              {/* Box grid layout mimicking physical smart locker */}
              <Grid container spacing={1.5}>
                {selectedLocker?.boxes && selectedLocker.boxes.length > 0 ? (
                  selectedLocker.boxes.map((b: LockerBoxItem) => {
                    const st = LockerBoxStatusLabels[b.status] || { label: 'Trống', color: 'success' };
                    return (
                      <Grid item xs={3} sm={3} key={b.id}>
                        <Box
                          sx={{
                            p: 1.5,
                            border: '2px solid',
                            borderColor:
                              b.status === LockerBoxStatus.Available
                                ? 'success.main'
                                : b.status === LockerBoxStatus.Occupied
                                ? 'error.main'
                                : b.status === LockerBoxStatus.Reserved
                                ? 'warning.main'
                                : 'grey.400',
                            borderRadius: 2,
                            textAlign: 'center',
                            backgroundColor:
                              b.status === LockerBoxStatus.Available
                                ? 'success.light'
                                : b.status === LockerBoxStatus.Occupied
                                ? 'error.light'
                                : 'action.hover',
                            cursor: 'pointer',
                            transition: 'all 0.15s ease',
                            '&:hover': { transform: 'scale(1.04)' },
                          }}
                        >
                          <Typography variant="subtitle2" sx={{ fontWeight: 800 }}>
                            {b.boxNumber}
                          </Typography>
                          <Typography variant="caption" sx={{ fontSize: '0.68rem', fontWeight: 600 }}>
                            {st.label}
                          </Typography>
                        </Box>
                      </Grid>
                    );
                  })
                ) : (
                  <Typography variant="body2" color="text.secondary" sx={{ p: 2 }}>
                    Chưa có cấu hình ô tủ cho trạm này.
                  </Typography>
                )}
              </Grid>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
};
