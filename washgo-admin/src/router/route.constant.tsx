import React from 'react';
import DashboardIcon from '@mui/icons-material/Dashboard';
import LocalLaundryServiceIcon from '@mui/icons-material/LocalLaundryService';
import MeetingRoomIcon from '@mui/icons-material/MeetingRoom';
import DryCleaningIcon from '@mui/icons-material/DryCleaning';
import MapIcon from '@mui/icons-material/Map';
import StorefrontIcon from '@mui/icons-material/Storefront';

export interface RouteConfig {
  path: string;
  label: string;
  icon: React.ReactNode;
  permission?: string;
  role?: string[];
  element?: string;
}

export const ROUTE_ITEMS: RouteConfig[] = [
  {
    path: '/',
    label: 'Bảng điều khiển',
    icon: <DashboardIcon />,
  },
  {
    path: '/wash-order',
    label: 'Quản lý Đơn hàng',
    icon: <LocalLaundryServiceIcon />,
    permission: 'WashGo.WashOrders',
  },
  {
    path: '/locker',
    label: 'Tủ đồ Locker',
    icon: <MeetingRoomIcon />,
    permission: 'WashGo.Lockers',
  },
  {
    path: '/wash-service',
    label: 'Bảng giá & Dịch vụ',
    icon: <DryCleaningIcon />,
    permission: 'WashGo.Services',
  },
  {
    path: '/service-area',
    label: 'Vùng phục vụ',
    icon: <MapIcon />,
    permission: 'WashGo.ServiceAreas',
  },
  {
    path: '/merchant',
    label: 'Đối tác giặt sấy',
    icon: <StorefrontIcon />,
    permission: 'WashGo.Merchants',
  },
];
