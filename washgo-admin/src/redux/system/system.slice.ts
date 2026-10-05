import { createSlice, PayloadAction } from '@reduxjs/toolkit';

export interface SystemState {
  themeMode: 'light' | 'dark';
  sidebarOpen: boolean;
  isSessionExpiredModalOpen: boolean;
}

const initialState: SystemState = {
  themeMode: 'light',
  sidebarOpen: true,
  isSessionExpiredModalOpen: false,
};

export const systemSlice = createSlice({
  name: 'system',
  initialState,
  reducers: {
    toggleThemeMode: (state) => {
      state.themeMode = state.themeMode === 'light' ? 'dark' : 'light';
    },
    setThemeMode: (state, action: PayloadAction<'light' | 'dark'>) => {
      state.themeMode = action.payload;
    },
    toggleSidebar: (state) => {
      state.sidebarOpen = !state.sidebarOpen;
    },
    setSessionExpiredModal: (state, action: PayloadAction<boolean>) => {
      state.isSessionExpiredModalOpen = action.payload;
    },
  },
});

export const { toggleThemeMode, setThemeMode, toggleSidebar, setSessionExpiredModal } = systemSlice.actions;
export default systemSlice.reducer;
