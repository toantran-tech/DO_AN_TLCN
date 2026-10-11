import React from 'react';
import { Box, Button, Card, CardContent, Chip, Divider, Grid, Stack, Typography } from '@mui/material';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import LocalLaundryServiceOutlinedIcon from '@mui/icons-material/LocalLaundryServiceOutlined';
import StorefrontOutlinedIcon from '@mui/icons-material/StorefrontOutlined';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import PeopleAltOutlinedIcon from '@mui/icons-material/PeopleAltOutlined';
import AssignmentLateOutlinedIcon from '@mui/icons-material/AssignmentLateOutlined';
import ArrowUpwardRoundedIcon from '@mui/icons-material/ArrowUpwardRounded';
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded';
import WarningAmberRoundedIcon from '@mui/icons-material/WarningAmberRounded';
import CheckCircleOutlineRoundedIcon from '@mui/icons-material/CheckCircleOutlineRounded';
import { useNavigate } from 'react-router-dom';

const stats = [
  { title: 'Tổng doanh thu', value: '1.250.000.000đ', note: '+12.5% so với tháng trước', icon: <PaymentsOutlinedIcon />, color: '#ff7043', trend: true },
  { title: 'Tổng đơn hàng', value: '4.520', note: '+8.2% so với tháng trước', icon: <LocalLaundryServiceOutlinedIcon />, color: '#2f80ed', trend: true },
  { title: 'Merchant hoạt động', value: '128', note: '+3 mới tuần này', icon: <StorefrontOutlinedIcon />, color: '#27ae60', trend: true },
  { title: 'Locker hoạt động', value: '85', note: '98% đang khả dụng', icon: <LockOutlinedIcon />, color: '#9b51e0', trend: false },
  { title: 'Tổng khách hàng', value: '12.400', note: '+5.4% so với tháng trước', icon: <PeopleAltOutlinedIcon />, color: '#f2994a', trend: true },
  { title: 'Đơn chờ gán', value: '15', note: 'Cần xử lý ngay', icon: <AssignmentLateOutlinedIcon />, color: '#eb5757', trend: false, urgent: true },
];

const merchants = [
  { name: 'Laundry House', orders: '856 đơn', revenue: '245.000.000đ', score: '4.9' },
  { name: 'Clean & Fresh', orders: '742 đơn', revenue: '198.500.000đ', score: '4.8' },
  { name: 'The Laundry', orders: '635 đơn', revenue: '176.200.000đ', score: '4.8' },
  { name: 'Wash & Go', orders: '521 đơn', revenue: '142.800.000đ', score: '4.7' },
  { name: 'Eco Laundry', orders: '489 đơn', revenue: '128.400.000đ', score: '4.7' },
];

const pendingOrders = [
  { code: 'WG2610066513', customer: 'Nguyễn Minh Anh', area: 'Quận 1', time: '10 phút trước' },
  { code: 'WG2610066508', customer: 'Trần Hoàng Nam', area: 'Quận 3', time: '18 phút trước' },
  { code: 'WG2610066499', customer: 'Lê Thu Hà', area: 'Bình Thạnh', time: '25 phút trước' },
];

const alerts = [
  { title: 'Locker L-102 sắp đầy', detail: 'Chỉ còn 2 ô trống', color: '#f2994a', icon: <WarningAmberRoundedIcon /> },
  { title: 'Đơn hàng quá hạn', detail: '3 đơn chưa được xử lý', color: '#eb5757', icon: <AssignmentLateOutlinedIcon /> },
  { title: 'Merchant cần xác minh', detail: '2 hồ sơ đang chờ duyệt', color: '#2f80ed', icon: <CheckCircleOutlineRoundedIcon /> },
];

const PanelTitle: React.FC<{ title: string; action?: React.ReactNode }> = ({ title, action }) => (
  <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1.5 }}>
    <Typography variant="subtitle1" fontWeight={800}>{title}</Typography>{action}
  </Stack>
);

const StatCard: React.FC<typeof stats[number]> = ({ title, value, note, icon, color, trend, urgent }) => (
  <Card variant="outlined" sx={{ height: '100%', borderColor: '#e7ebf0', borderRadius: 2.5, boxShadow: '0 2px 8px rgba(15,23,42,.03)' }}>
    <CardContent sx={{ p: 1.7, '&:last-child': { pb: 1.7 } }}>
      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
        <Box sx={{ minWidth: 0 }}><Typography variant="caption" color="text.secondary" noWrap>{title}</Typography><Typography variant="h6" fontWeight={800} sx={{ mt: .45, letterSpacing: '-.3px' }}>{value}</Typography></Box>
        <Box sx={{ width: 38, height: 38, flexShrink: 0, display: 'grid', placeItems: 'center', borderRadius: 2, color, bgcolor: `${color}16`, '& svg': { fontSize: 20 } }}>{icon}</Box>
      </Stack>
      <Stack direction="row" alignItems="center" spacing={.35} sx={{ mt: 1 }}>
        {trend && <ArrowUpwardRoundedIcon sx={{ fontSize: 14, color: '#27ae60' }} />}
        <Typography variant="caption" color={urgent ? 'error.main' : trend ? '#27ae60' : 'text.secondary'} fontWeight={urgent || trend ? 700 : 500}>{urgent ? 'KHẨN CẤP' : note}</Typography>
        {urgent && <Button size="small" onClick={() => undefined} sx={{ ml: 'auto', minWidth: 0, p: 0, fontSize: 11, textTransform: 'none' }}>Xử lý ngay</Button>}
      </Stack>
    </CardContent>
  </Card>
);

export const DashboardScreen: React.FC = () => {
  const navigate = useNavigate();
  return (
    <Box sx={{ p: { xs: 1.5, sm: 2.5, lg: 3 }, maxWidth: 1600, mx: 'auto' }}>
      <Box sx={{ mb: 2.25 }}><Typography variant="h5" fontWeight={800} letterSpacing="-.4px">Tổng quan hệ thống</Typography><Typography variant="body2" color="text.secondary">Theo dõi và quản lý toàn bộ hoạt động của nền tảng WashGo.</Typography></Box>
      <Grid container spacing={1.5} sx={{ mb: 1.75 }}>{stats.map((stat) => <Grid item xs={6} sm={4} lg={2} key={stat.title}><StatCard {...stat} /></Grid>)}</Grid>

      <Grid container spacing={1.75} sx={{ mb: 1.75 }}>
        <Grid item xs={12} lg={8}><Card variant="outlined" sx={{ height: '100%', borderColor: '#e7ebf0', borderRadius: 2.5 }}><CardContent sx={{ p: { xs: 1.7, sm: 2.25 } }}><PanelTitle title="Doanh thu theo tháng" action={<Chip size="small" label="Năm 2026" variant="outlined" sx={{ borderColor: '#e0e5eb', fontSize: 11 }} />} /><Box sx={{ height: { xs: 190, sm: 235 }, mt: 1 }}><svg viewBox="0 0 760 260" preserveAspectRatio="none" width="100%" height="100%" role="img" aria-label="Biểu đồ doanh thu theo tháng">{[42, 88, 134, 180, 226].map((y, index) => <g key={y}><line x1="48" x2="744" y1={y} y2={y} stroke="#edf0f3" strokeDasharray="3 4" /><text x="2" y={y + 4} fill="#8a94a6" fontSize="10">{['1.5B', '1.2B', '900M', '600M', '300M'][index]}</text></g>)}<path d="M50 184 C90 170 112 166 145 154 S210 138 240 146 S305 116 340 126 S405 95 440 105 S505 75 540 88 S605 55 640 69 S700 37 742 48 L742 226 L50 226 Z" fill="rgba(255,112,67,.12)" /><path d="M50 184 C90 170 112 166 145 154 S210 138 240 146 S305 116 340 126 S405 95 440 105 S505 75 540 88 S605 55 640 69 S700 37 742 48" fill="none" stroke="#ff7043" strokeWidth="3" strokeLinecap="round" />{[['T1', 50, 184], ['T2', 145, 154], ['T3', 240, 146], ['T4', 340, 126], ['T5', 440, 105], ['T6', 540, 88], ['T7', 640, 69], ['T8', 742, 48]].map(([label, x, y]) => <g key={String(label)}><circle cx={Number(x)} cy={Number(y)} r="3.5" fill="#ff7043" /><text x={Number(x) - 7} y="250" fill="#8a94a6" fontSize="10">{label}</text></g>)}</svg></Box></CardContent></Card></Grid>
        <Grid item xs={12} lg={4}><Card variant="outlined" sx={{ height: '100%', borderColor: '#e7ebf0', borderRadius: 2.5 }}><CardContent sx={{ p: { xs: 1.7, sm: 2.25 } }}><PanelTitle title="Hiệu suất Merchant" action={<Button size="small" onClick={() => navigate('/merchant')} sx={{ textTransform: 'none' }}>Xem tất cả</Button>} /><Stack direction="row" alignItems="flex-end" justifyContent="space-around" sx={{ height: { xs: 190, sm: 235 }, pt: 2, pb: 1 }}>{[['Laundry', 82, '#ff7043'], ['Clean', 68, '#2f80ed'], ['The Laundry', 58, '#27ae60'], ['WashGo', 48, '#9b51e0'], ['Eco', 38, '#f2994a']].map(([label, value, color]) => <Stack key={String(label)} alignItems="center" spacing={.7} sx={{ height: '100%', justifyContent: 'flex-end' }}><Box sx={{ width: { xs: 22, sm: 28 }, height: `${Number(value)}%`, minHeight: 24, borderRadius: '5px 5px 2px 2px', bgcolor: color }} /><Typography variant="caption" color="text.secondary" sx={{ fontSize: 10 }}>{label}</Typography></Stack>)}</Stack></CardContent></Card></Grid>
      </Grid>

      <Grid container spacing={1.75}>
        <Grid item xs={12} lg={4}><Card variant="outlined" sx={{ height: '100%', borderColor: '#e7ebf0', borderRadius: 2.5 }}><CardContent sx={{ p: 2 }}><PanelTitle title="Top 5 Merchant Tốt Nhất" action={<Button size="small" onClick={() => navigate('/merchant')} sx={{ textTransform: 'none' }}>Xem tất cả</Button>} /><Stack divider={<Divider flexItem />} spacing={0}>{merchants.map((merchant, index) => <Stack key={merchant.name} direction="row" alignItems="center" spacing={1} sx={{ py: 1.05 }}><Box sx={{ width: 27, height: 27, borderRadius: '50%', display: 'grid', placeItems: 'center', bgcolor: index === 0 ? '#fff1eb' : '#f4f6f8', color: index === 0 ? '#ff7043' : '#667085', fontWeight: 800, fontSize: 12 }}>{index + 1}</Box><Box sx={{ flex: 1, minWidth: 0 }}><Typography variant="body2" fontWeight={700} noWrap>{merchant.name}</Typography><Typography variant="caption" color="text.secondary">{merchant.orders} • {merchant.revenue}</Typography></Box><Typography variant="caption" fontWeight={800} color="#f2994a">★ {merchant.score}</Typography></Stack>)}</Stack></CardContent></Card></Grid>
        <Grid item xs={12} lg={4}><Card variant="outlined" sx={{ height: '100%', borderColor: '#e7ebf0', borderRadius: 2.5 }}><CardContent sx={{ p: 2 }}><PanelTitle title="Đơn chờ phân công" action={<Chip size="small" label="15 đơn" sx={{ bgcolor: '#fff0ee', color: '#eb5757', fontWeight: 700 }} />} /><Stack divider={<Divider flexItem />} spacing={0}>{pendingOrders.map((order) => <Stack key={order.code} direction="row" alignItems="center" spacing={1} sx={{ py: 1.02 }}><Box sx={{ flex: 1, minWidth: 0 }}><Typography variant="body2" fontWeight={700}>{order.code}</Typography><Typography variant="caption" color="text.secondary">{order.customer} • {order.area}</Typography><Typography variant="caption" display="block" color="text.secondary">{order.time}</Typography></Box><Button size="small" variant="outlined" onClick={() => navigate('/wash-order')} sx={{ textTransform: 'none', whiteSpace: 'nowrap', minWidth: 0, px: 1 }}>Gán ngay</Button></Stack>)}</Stack></CardContent></Card></Grid>
        <Grid item xs={12} lg={4}><Card variant="outlined" sx={{ height: '100%', borderColor: '#e7ebf0', borderRadius: 2.5 }}><CardContent sx={{ p: 2 }}><PanelTitle title="Cảnh báo AI Hệ thống" action={<Chip size="small" label="3 cảnh báo" sx={{ bgcolor: '#fff8e8', color: '#c77a00', fontWeight: 700 }} />} /><Stack spacing={1.1}>{alerts.map((alert) => <Stack key={alert.title} direction="row" spacing={1} alignItems="center" sx={{ p: 1, borderRadius: 1.7, bgcolor: `${alert.color}0d` }}><Box sx={{ width: 30, height: 30, flexShrink: 0, display: 'grid', placeItems: 'center', color: alert.color, '& svg': { fontSize: 19 } }}>{alert.icon}</Box><Box sx={{ minWidth: 0 }}><Typography variant="body2" fontWeight={700}>{alert.title}</Typography><Typography variant="caption" color="text.secondary">{alert.detail}</Typography></Box><ArrowForwardRoundedIcon sx={{ ml: 'auto', fontSize: 17, color: '#98a2b3' }} /></Stack>)}</Stack><Button fullWidth variant="contained" onClick={() => navigate('/notifications')} sx={{ mt: 1.5, textTransform: 'none' }}>Xử lý tất cả cảnh báo</Button></CardContent></Card></Grid>
      </Grid>
    </Box>
  );
};
