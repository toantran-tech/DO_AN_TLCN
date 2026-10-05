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
            main: '#2563eb', // Royal Blue
            light: '#60a5fa',
            dark: '#1d4ed8',
            contrastText: '#ffffff',
          },
          secondary: {
            main: '#06b6d4', // Cyan
            light: '#67e8f9',
            dark: '#0e7490',
          },
          background: {
            default: themeMode === 'light' ? '#f8fafc' : '#0f172a',
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
                borderRadius: 8,
                boxShadow: 'none',
                '&:hover': {
                  boxShadow: '0 4px 12px rgba(37, 99, 235, 0.2)',
                },
              },
            },
          },
          MuiCard: {
            styleOverrides: {
              root: {
                backgroundImage: 'none',
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
