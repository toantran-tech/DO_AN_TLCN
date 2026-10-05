import React from 'react';
import {
  Box,
  Grid,
  Card,
  CardContent,
  Typography,
  Chip,
  LinearProgress,
  Button,
  Stack,
  Divider,
} from '@mui/material';
import LocalLaundryServiceIcon from '@mui/icons-material/LocalLaundryService';
import MeetingRoomIcon from '@mui/icons-material/MeetingRoom';
import MonetizationOnIcon from '@mui/icons-material/MonetizationOn';
import AccessTimeIcon from '@mui/icons-material/AccessTime';
import ArrowForwardIcon from '@mui/icons-material/ArrowForward';
import { useNavigate } from 'react-router-dom';

export const DashboardScreen: React.FC = () => {
  const navigate = useNavigate();

  const stats = [
    {
      title: 'Đơn hàng hôm nay',
      value: '128 đơn',
      sub: '+14% so với hôm qua',
      icon: <LocalLaundryServiceIcon sx={{ fontSize: 32, color: 'primary.main' }} />,
      color: 'primary.main',
      progress: 78,
    },
    {
      title: 'Công suất Tủ Locker',
      value: '84 / 120 ô',
      sub: 'Tỷ lệ lấp đầy 70.0%',
      icon: <MeetingRoomIcon sx={{ fontSize: 32, color: 'warning.main' }} />,
      color: 'warning.main',
      progress: 70,
    },
    {
      title: 'Doanh thu trong ngày',
      value: '12.450.000 ₫',
      sub: 'Mục tiêu: 15.000.000 ₫',
      icon: <MonetizationOnIcon sx={{ fontSize: 32, color: 'success.main' }} />,
      color: 'success.main',
      progress: 83,
    },
    {
      title: 'Đơn đang xử lý giặt',
      value: '42 đơn',
      sub: '6 đơn sắp chạm SLA 24h',
      icon: <AccessTimeIcon sx={{ fontSize: 32, color: 'info.main' }} />,
      color: 'info.main',
      progress: 55,
    },
  ];

  return (
    <Box sx={{ p: { xs: 2, md: 3 } }}>
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 800, letterSpacing: -0.5 }}>
            Bảng điều khiển WashGo
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Giám sát vận hành đơn hàng, tủ đồ thông minh và hiệu suất giặt sấy thời gian thực
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button
            variant="contained"
            startIcon={<LocalLaundryServiceIcon />}
            onClick={() => navigate('/wash-order')}
          >
            Quản lý đơn hàng
          </Button>
        </Stack>
      </Box>

      {/* KPI Cards */}
      <Grid container spacing={2.5} sx={{ mb: 3 }}>
        {stats.map((item, index) => (
          <Grid item xs={12} sm={6} md={3} key={index}>
            <Card
              elevation={0}
              sx={{
                p: 1,
                border: '1px solid',
                borderColor: 'divider',
                borderRadius: 2.5,
                transition: 'all 0.2s ease',
                '&:hover': { transform: 'translateY(-2px)', boxShadow: '0 8px 20px rgba(0,0,0,0.06)' },
              }}
            >
              <CardContent>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1.5 }}>
                  <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 600 }}>
                    {item.title}
                  </Typography>
                  <Box
                    sx={{
                      p: 0.8,
                      borderRadius: 1.5,
                      backgroundColor: 'action.hover',
                      display: 'flex',
                      alignItems: 'center',
                    }}
                  >
                    {item.icon}
                  </Box>
                </Box>
                <Typography variant="h5" sx={{ fontWeight: 800, mb: 0.5 }}>
                  {item.value}
                </Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
                  {item.sub}
                </Typography>
                <LinearProgress
                  variant="determinate"
                  value={item.progress}
                  sx={{ height: 6, borderRadius: 3 }}
                />
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {/* Quick View Sections */}
      <Grid container spacing={2.5}>
        <Grid item xs={12} md={7}>
          <Card
            elevation={0}
            sx={{
              p: 2.5,
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 2.5,
              height: '100%',
            }}
          >
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                Trạm Locker đang hoạt động
              </Typography>
              <Button
                size="small"
                endIcon={<ArrowForwardIcon />}
                onClick={() => navigate('/locker')}
              >
                Xem tất cả
              </Button>
            </Box>
            <Divider sx={{ mb: 2 }} />

            <Stack spacing={2}>
              {[
                { name: 'Locker Vinhome Central Park - Tòa Landmark 3', boxes: '18/24', status: 'Active', district: 'Bình Thạnh' },
                { name: 'Locker Masteri Thảo Điền - Tháp T1', boxes: '15/20', status: 'Active', district: 'Thủ Đức' },
                { name: 'Locker Sunrise City - Central Tower', boxes: '20/24', status: 'Active', district: 'Quận 7' },
                { name: 'Locker Saigon Pearl - Topaz Tower', boxes: '12/16', status: 'Maintenance', district: 'Bình Thạnh' },
              ].map((loc, i) => (
                <Box
                  key={i}
                  sx={{
                    p: 1.5,
                    border: '1px solid',
                    borderColor: 'divider',
                    borderRadius: 2,
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                  }}
                >
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {loc.name}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {loc.district} • Đang dùng {loc.boxes} ô tủ
                    </Typography>
                  </Box>
                  <Chip
                    size="small"
                    label={loc.status === 'Active' ? 'Hoạt động' : 'Bảo trì'}
                    color={loc.status === 'Active' ? 'success' : 'warning'}
                  />
                </Box>
              ))}
            </Stack>
          </Card>
        </Grid>

        <Grid item xs={12} md={5}>
          <Card
            elevation={0}
            sx={{
              p: 2.5,
              border: '1px solid',
              borderColor: 'divider',
              borderRadius: 2.5,
              height: '100%',
            }}
          >
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                Quy trình vận hành WashGo
              </Typography>
            </Box>
            <Divider sx={{ mb: 2 }} />

            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
              {[
                { step: '1', title: 'Khách gửi đồ tại Locker', desc: 'Chọn dịch vụ trên app, quét QR nhận ô tủ' },
                { step: '2', title: 'Shipper lấy đồ đi giặt', desc: 'Quét mã xác nhận mở ô tủ và chuyển tới Tiệm' },
                { step: '3', title: 'Tiệm giặt xử lý dịch vụ', desc: 'Giặt sấy, là ủi, kiểm tra chất lượng theo SLA' },
                { step: '4', title: 'Giao trả lại Locker & Nhận đồ', desc: 'Shipper trả đồ vào tủ, khách nhận mã PIN mở tủ' },
              ].map((s, idx) => (
                <Box key={idx} sx={{ display: 'flex', gap: 1.5, alignItems: 'flex-start' }}>
                  <Box
                    sx={{
                      width: 28,
                      height: 28,
                      borderRadius: '50%',
                      backgroundColor: 'primary.main',
                      color: 'primary.contrastText',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontWeight: 700,
                      fontSize: 12,
                      flexShrink: 0,
                    }}
                  >
                    {s.step}
                  </Box>
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {s.title}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {s.desc}
                    </Typography>
                  </Box>
                </Box>
              ))}
            </Box>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
};
