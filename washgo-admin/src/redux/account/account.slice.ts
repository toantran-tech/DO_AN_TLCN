import { createSlice, createAsyncThunk, PayloadAction } from '@reduxjs/toolkit';
import { UserSnapshot } from '../../common/interfaces/api.interface';
import { loginApi, refreshTokenApi } from '../../apis/auth/auth.api';
import { LoginParams } from '../../apis/auth/auth.interface';

export interface AccountState {
  accessToken: string | null;
  refreshToken: string | null;
  user: UserSnapshot | null;
  permissions: string[];
  userUnitPositionId?: string | null;
  deviceId?: string | null;
  isAuthenticated: boolean;
  loading: boolean;
  error?: string | null;
}

const initialState: AccountState = {
  accessToken: null,
  refreshToken: null,
  user: null,
  permissions: ['WashGo.WashOrders', 'WashGo.Lockers', 'WashGo.Services', 'WashGo.Merchants', 'WashGo.ServiceAreas'],
  userUnitPositionId: 'POS-001',
  deviceId: 'DEV-BROWSER-01',
  isAuthenticated: false,
  loading: false,
  error: null,
};

export const loginThunk = createAsyncThunk(
  'account/login',
  async (params: LoginParams, { rejectWithValue }) => {
    try {
      const res = await loginApi(params);
      if (res.data) {
        return res.data;
      }
      return rejectWithValue(res.message || 'Đăng nhập không thành công');
    } catch (err: any) {
      return rejectWithValue(err?.message || 'Email hoặc mật khẩu không chính xác');
    }
  }
);

export const refreshTokenThunk = createAsyncThunk(
  'account/refreshToken',
  async ({ refreshToken }: { refreshToken: string | null }, { rejectWithValue }) => {
    try {
      if (!refreshToken) return rejectWithValue('No refresh token provided');
      const res = await refreshTokenApi({ refreshToken });
      if (res.data) {
        return res.data;
      }
      return rejectWithValue(res.message || 'Làm mới token thất bại');
    } catch (err: any) {
      return rejectWithValue(err?.message || 'Phiên đăng nhập đã hết hạn');
    }
  }
);

export const accountSlice = createSlice({
  name: 'account',
  initialState,
  reducers: {
    setCredentials: (
      state,
      action: PayloadAction<{
        accessToken: string;
        refreshToken: string;
        user: UserSnapshot;
        permissions?: string[];
      }>
    ) => {
      state.accessToken = action.payload.accessToken;
      state.refreshToken = action.payload.refreshToken;
      state.user = action.payload.user;
      state.permissions = action.payload.permissions || [];
      state.isAuthenticated = true;
    },
    logout: (state) => {
      state.accessToken = null;
      state.refreshToken = null;
      state.user = null;
      state.permissions = [];
      state.isAuthenticated = false;
      state.error = null;
    },
  },
  extraReducers: (builder) => {
    // Login
    builder.addCase(loginThunk.pending, (state) => {
      state.loading = true;
      state.error = null;
    });
    builder.addCase(loginThunk.fulfilled, (state, action) => {
      state.loading = false;
      state.accessToken = action.payload.accessToken;
      state.refreshToken = action.payload.refreshToken;
      state.user = {
        id: action.payload.user.id,
        name: action.payload.user.fullName,
        code: action.payload.user.email,
        email: action.payload.user.email,
        role: action.payload.user.role,
        avatarUrl: action.payload.user.avatar,
      };
      state.isAuthenticated = true;
    });
    builder.addCase(loginThunk.rejected, (state, action) => {
      state.loading = false;
      state.error = action.payload as string;
    });

    // Refresh token
    builder.addCase(refreshTokenThunk.fulfilled, (state, action) => {
      state.accessToken = action.payload.accessToken;
      state.refreshToken = action.payload.refreshToken;
    });
    builder.addCase(refreshTokenThunk.rejected, (state) => {
      state.accessToken = null;
      state.refreshToken = null;
      state.user = null;
      state.isAuthenticated = false;
    });
  },
});

export const { setCredentials, logout } = accountSlice.actions;
export const ACTION_ACCOUNT = {
  ...accountSlice.actions,
  login: loginThunk,
  refreshToken: refreshTokenThunk,
};
export default accountSlice.reducer;
