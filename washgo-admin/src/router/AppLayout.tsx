import React, { useState } from 'react';
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
  useMediaQuery,
  Stack,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import Brightness4Icon from '@mui/icons-material/Brightness4';
import Brightness7Icon from '@mui/icons-material/Brightness7';
import NotificationsOutlinedIcon from '@mui/icons-material/NotificationsOutlined';
import LogoutIcon from '@mui/icons-material/Logout';
import WaterDropIcon from '@mui/icons-material/WaterDrop';
import HeadsetMicOutlinedIcon from '@mui/icons-material/HeadsetMicOutlined';
import { RootState } from '../redux/store.redux';
import { toggleSidebar, toggleThemeMode } from '../redux/system/system.slice';
import { logout } from '../redux/account/account.slice';
import { logoutApi } from '../apis/auth/auth.api';
import { ROUTE_ITEMS } from './route.constant';

const DRAWER_WIDTH = 228;

export const AppLayout: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const dispatch = useDispatch();
  const isNarrow = useMediaQuery('(max-width: 899px)');
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const { sidebarOpen, themeMode } = useSelector((state: RootState) => state.system);
  const { user } = useSelector((state: RootState) => state.account);
  const { refreshToken } = useSelector((state: RootState) => state.account);
  const activeRoute = ROUTE_ITEMS.find((item) => item.path === location.pathname);
  const drawerOpen = isNarrow ? mobileMenuOpen : sidebarOpen;
  const toggleMenu = () => isNarrow ? setMobileMenuOpen((open) => !open) : dispatch(toggleSidebar());

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
          width: { xs: '100%', md: sidebarOpen ? `calc(100% - ${DRAWER_WIDTH}px)` : '100%' },
          ml: { xs: 0, md: sidebarOpen ? `${DRAWER_WIDTH}px` : 0 },
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
            <IconButton onClick={toggleMenu} edge="start" size="small">
              <MenuIcon />
            </IconButton>
            <Typography variant="body2" color="text.secondary" sx={{ fontSize: 12 }}>
              Trang chủ&nbsp; › &nbsp;<Box component="span" sx={{ color: 'text.primary', fontWeight: 700 }}>{activeRoute?.label || 'WashGo'}</Box>
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
        variant={isNarrow ? 'temporary' : 'persistent'}
        open={drawerOpen}
        onClose={() => setMobileMenuOpen(false)}
        sx={{
          width: !isNarrow && sidebarOpen ? DRAWER_WIDTH : 0,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: DRAWER_WIDTH,
            boxSizing: 'border-box',
            borderRight: 0,
            backgroundColor: '#1d2a3f',
            color: '#fff',
            display: 'flex',
            flexDirection: 'column',
          },
        }}
      >
        <Box sx={{ px: 2.5, height: 88, display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <Box
            sx={{
              width: 40,
              height: 40,
              borderRadius: 2.2,
              background: 'linear-gradient(135deg, #ff8a65 0%, #ff5722 100%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#fff',
            }}
          >
            <WaterDropIcon sx={{ fontSize: 21 }} />
          </Box>
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 900, lineHeight: 1, letterSpacing: -0.5, color: '#fff' }}>
              WashGo
            </Typography>
          </Box>
        </Box>

        <Divider sx={{ borderColor: 'rgba(255,255,255,.08)' }} />

        <List sx={{ px: 1.25, py: 2, flexGrow: 1 }}>
          {ROUTE_ITEMS.map((item) => {
            const isActive = location.pathname === item.path;
            return (
              <ListItem key={item.path} disablePadding sx={{ mb: 0.5 }}>
                <ListItemButton
                  onClick={() => { navigate(item.path); setMobileMenuOpen(false); }}
                  sx={{
                    borderRadius: 1.5,
                    py: 1.05,
                    px: 1.5,
                    backgroundColor: isActive ? 'primary.main' : 'transparent',
                    color: isActive ? '#fff' : '#9eabc0',
                    '&:hover': {
                      backgroundColor: isActive ? 'primary.dark' : 'rgba(255,255,255,.06)',
                    },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 34, color: isActive ? '#fff' : '#9eabc0', '& svg': { fontSize: 19 } }}>
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
        <Box sx={{ mt: 'auto', mx: 1.5, mb: 2, p: 1.5, border: '1px solid rgba(255,255,255,.10)', borderRadius: 2 }}>
          <Typography variant="overline" sx={{ color: '#7f8da3', fontSize: 9, letterSpacing: 1.1 }}>Hỗ trợ kỹ thuật</Typography>
          <Stack direction="row" spacing={1} alignItems="center" sx={{ mt: .25 }}>
            <HeadsetMicOutlinedIcon sx={{ fontSize: 18, color: 'primary.main' }} />
            <Typography variant="body2" sx={{ color: '#b8c2d1', fontWeight: 600 }}>1900 8888</Typography>
          </Stack>
        </Box>
      </Drawer>

      {/* Main Page Area */}
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          pt: '64px',
          minWidth: 0,
          width: { xs: '100%', md: sidebarOpen ? `calc(100% - ${DRAWER_WIDTH}px)` : '100%' },
          transition: 'all 0.2s ease',
        }}
      >
        <Outlet />
      </Box>
    </Box>
  );
};
