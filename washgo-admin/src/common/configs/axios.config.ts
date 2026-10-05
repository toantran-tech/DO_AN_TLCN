import axios, { AxiosInstance } from 'axios';

export const HttpStatusSpecial = {
  REFRESH_TOKEN: 777,
};

let refreshingPromise: Promise<any> | null = null;

export const createAxiosRequest = (baseURL: string): AxiosInstance => {
  const instance = axios.create({
    baseURL,
    timeout: 30000,
    headers: { 'Content-Type': 'application/json' },
    withCredentials: true,
  });

  instance.interceptors.request.use(async (config) => {
    const { store } = await import('../../redux/store.redux');
    const { accessToken, userUnitPositionId, deviceId } = store.getState().account;

    if (accessToken) {
      config.headers.Authorization = `Bearer ${accessToken}`;
    }
    if (userUnitPositionId) {
      config.headers['userUnitPositionId'] = userUnitPositionId;
    }
    if (deviceId) {
      config.headers['deviceId'] = deviceId;
    }
    return config;
  });

  instance.interceptors.response.use(
    (response) => (response.config.responseType === 'blob' ? response : response.data),
    async (error) => {
      const { store } = await import('../../redux/store.redux');
      const originalConfig = error.config;

      // Xử lý mã hết hạn token nội bộ (HTTP Status 777)
      if (error?.response?.status === HttpStatusSpecial.REFRESH_TOKEN && !originalConfig._retry) {
        originalConfig._retry = true;
        try {
          if (!refreshingPromise) {
            const { ACTION_ACCOUNT } = await import('../../redux');
            const refreshToken = store.getState().account.refreshToken;
            refreshingPromise = store.dispatch(ACTION_ACCOUNT.refreshToken({ refreshToken })).unwrap();
          }

          const data = await refreshingPromise;
          if (data?.accessToken) {
            originalConfig.headers.Authorization = `Bearer ${data.accessToken}`;
            return axios.request(originalConfig);
          }
        } catch (refreshErr) {
          const { logout } = await import('../../redux/account/account.slice');
          store.dispatch(logout());
          return Promise.reject(refreshErr);
        } finally {
          // Bắt buộc reset về null để tránh deadlock khi có lỗi mạng
          refreshingPromise = null;
        }
      }

      return Promise.reject(error.response?.data || error);
    }
  );

  return instance;
};

export const axiosRequest = createAxiosRequest(import.meta.env.VITE_BE_URL || 'http://localhost:5000/');
export const axiosRequestSSO = createAxiosRequest(import.meta.env.VITE_SSO_BE_URL || 'http://localhost:5001/');
