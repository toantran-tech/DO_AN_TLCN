import React, { useMemo } from 'react';
import { useSelector } from 'react-redux';
import { BrowserRouter } from 'react-router-dom';
import { ThemeProvider, createTheme, CssBaseline } from '@mui/material';
import { RootState } from './redux/store.redux';
import { AppRouter } from './router/render.route';

export const AppContent: React.FC = () => {
  const { themeMode } = useSelector((state: RootState) => state.system);

  const theme = useMemo(
    () =>
      createTheme({
        palette: {
          mode: themeMode,
          primary: {
            main: '#ff7043',
            light: '#ffab91',
            dark: '#e85d2a',
            contrastText: '#ffffff',
          },
          secondary: {
            main: '#fb8c00',
            light: '#ffcc80',
            dark: '#ef6c00',
          },
          background: {
            default: themeMode === 'light' ? '#f6f7f9' : '#0f172a',
            paper: themeMode === 'light' ? '#ffffff' : '#1e293b',
          },
          divider: themeMode === 'light' ? '#e2e8f0' : '#334155',
        },
        typography: {
          fontFamily: '"Plus Jakarta Sans", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
          button: {
            textTransform: 'none',
            fontWeight: 600,
          },
        },
        shape: {
          borderRadius: 8,
        },
        components: {
          MuiButton: {
            styleOverrides: {
              root: {
                borderRadius: 10,
                boxShadow: 'none',
                '&:hover': {
                  boxShadow: '0 4px 12px rgba(255, 112, 67, 0.18)',
                },
              },
            },
          },
          MuiCard: {
            styleOverrides: {
              root: {
                backgroundImage: 'none',
                borderRadius: 14,
              },
            },
          },
          MuiPaper: {
            styleOverrides: {
              root: {
                backgroundImage: 'none',
              },
            },
          },
        },
      }),
    [themeMode]
  );

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <BrowserRouter>
        <AppRouter />
      </BrowserRouter>
    </ThemeProvider>
  );
};

export const App: React.FC = () => {
  return <AppContent />;
};

export default App;
