import React from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useSelector, useDispatch } from 'react-redux';
import {
  Box,
  Drawer,
  AppBar,
  Toolbar,
  Typography,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  IconButton,
  Avatar,
  Tooltip,
  Divider,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import Brightness4Icon from '@mui/icons-material/Brightness4';
import Brightness7Icon from '@mui/icons-material/Brightness7';
import NotificationsOutlinedIcon from '@mui/icons-material/NotificationsOutlined';
import LogoutIcon from '@mui/icons-material/Logout';
import { RootState } from '../redux/store.redux';
import { toggleSidebar, toggleThemeMode } from '../redux/system/system.slice';
import { logout } from '../redux/account/account.slice';
import { logoutApi } from '../apis/auth/auth.api';
import { ROUTE_ITEMS } from './route.constant';

const DRAWER_WIDTH = 250;

export const AppLayout: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const dispatch = useDispatch();

  const { sidebarOpen, themeMode } = useSelector((state: RootState) => state.system);
  const { user } = useSelector((state: RootState) => state.account);
  const { refreshToken } = useSelector((state: RootState) => state.account);

  const handleLogout = async () => {
    try {
      if (refreshToken) {
        await logoutApi({ refreshToken });
      }
    } catch {
      // Always clear the local session even if the backend is unavailable.
    } finally {
      dispatch(logout());
      navigate('/login', { replace: true });
    }
  };

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', backgroundColor: 'background.default' }}>
      {/* Top Navbar */}
      <AppBar
        position="fixed"
        elevation={0}
        sx={{
          width: { sm: sidebarOpen ? `calc(100% - ${DRAWER_WIDTH}px)` : '100%' },
          ml: { sm: sidebarOpen ? `${DRAWER_WIDTH}px` : 0 },
          borderBottom: '1px solid',
          borderColor: 'divider',
          backgroundColor: 'background.paper',
          color: 'text.primary',
          transition: 'all 0.2s ease',
          zIndex: (theme) => theme.zIndex.drawer + 1,
        }}
      >
        <Toolbar sx={{ justifyContent: 'space-between' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <IconButton onClick={() => dispatch(toggleSidebar())} edge="start" size="small">
              <MenuIcon />
            </IconButton>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              Hệ Thống Quản Lý Dịch Vụ WashGo
            </Typography>
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            <Tooltip title={`Chuyển sang chế độ ${themeMode === 'light' ? 'Tối' : 'Sáng'}`}>
              <IconButton onClick={() => dispatch(toggleThemeMode())} size="small">
                {themeMode === 'light' ? <Brightness4Icon /> : <Brightness7Icon />}
              </IconButton>
            </Tooltip>

            <Tooltip title="Thông báo hệ thống">
              <IconButton size="small">
                <NotificationsOutlinedIcon />
              </IconButton>
            </Tooltip>

            <Divider orientation="vertical" flexItem sx={{ mx: 0.5, height: 24, my: 'auto' }} />

            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.2 }}>
              <Avatar sx={{ width: 34, height: 34, bgcolor: 'primary.main', fontSize: 14, fontWeight: 700 }}>
                {user?.name?.charAt(0) || 'A'}
              </Avatar>
              <Box sx={{ display: { xs: 'none', md: 'block' } }}>
                <Typography variant="body2" sx={{ fontWeight: 600, lineHeight: 1.2 }}>
                  {user?.name || 'Admin'}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {user?.role || 'Quản trị viên'}
                </Typography>
              </Box>
              <Tooltip title="Đăng xuất">
                <IconButton size="small" onClick={handleLogout} sx={{ ml: 0.5 }}>
                  <LogoutIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </Box>
          </Box>
        </Toolbar>
      </AppBar>

      {/* Sidebar Drawer */}
      <Drawer
        variant="persistent"
        open={sidebarOpen}
        sx={{
          width: sidebarOpen ? DRAWER_WIDTH : 0,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: DRAWER_WIDTH,
            boxSizing: 'border-box',
            borderRight: '1px solid',
            borderColor: 'divider',
            backgroundColor: 'background.paper',
          },
        }}
      >
        <Box sx={{ p: 2.5, display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <Box
            sx={{
              width: 38,
              height: 38,
              borderRadius: 2,
              background: 'linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#fff',
              fontSize: 20,
              fontWeight: 800,
            }}
          >
            🧺
          </Box>
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 900, lineHeight: 1, letterSpacing: -0.5, color: 'primary.main' }}>
              WashGo
            </Typography>
            <Typography variant="caption" sx={{ fontSize: '0.68rem', fontWeight: 600, color: 'text.secondary' }}>
              SMART LAUNDRY PLATFORM
            </Typography>
          </Box>
        </Box>

        <Divider />

        <List sx={{ px: 1.5, py: 2 }}>
          {ROUTE_ITEMS.map((item) => {
            const isActive = location.pathname === item.path;
            return (
              <ListItem key={item.path} disablePadding sx={{ mb: 0.5 }}>
                <ListItemButton
                  onClick={() => navigate(item.path)}
                  sx={{
                    borderRadius: 2,
                    py: 1,
                    px: 1.5,
                    backgroundColor: isActive ? 'primary.main' : 'transparent',
                    color: isActive ? '#fff' : 'text.primary',
                    '&:hover': {
                      backgroundColor: isActive ? 'primary.dark' : 'action.hover',
                    },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 36, color: isActive ? '#fff' : 'text.secondary' }}>
                    {item.icon}
                  </ListItemIcon>
                  <ListItemText
                    primary={
                      <Typography variant="body2" sx={{ fontWeight: isActive ? 700 : 500 }}>
                        {item.label}
                      </Typography>
                    }
                  />
                </ListItemButton>
              </ListItem>
            );
          })}
        </List>
      </Drawer>

      {/* Main Page Area */}
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          pt: '64px',
          width: { sm: sidebarOpen ? `calc(100% - ${DRAWER_WIDTH}px)` : '100%' },
          transition: 'all 0.2s ease',
        }}
      >
        <Outlet />
      </Box>
    </Box>
  );
};
