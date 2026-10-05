export interface LoginParams {
  email: string;
  password: string;
}

export interface AuthUserData {
  id: string;
  email: string;
  fullName: string;
  phone?: string;
  role: string;
  avatar?: string;
}

export interface AuthResponseData {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUserData;
}
