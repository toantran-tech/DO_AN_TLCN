import { axiosRequest } from '../../common/configs';
import { BaseResponse } from '../../common/interfaces/api.interface';
import { LoginParams, AuthResponseData, AuthUserData } from './auth.interface';

export const loginApi = async (data: LoginParams): Promise<BaseResponse<AuthResponseData>> => {
  return await axiosRequest.post('auth/login', data);
};

export const refreshTokenApi = async (data: { refreshToken: string }): Promise<BaseResponse<AuthResponseData>> => {
  return await axiosRequest.post('auth/refresh-token', data);
};

export const logoutApi = async (data: { refreshToken: string }): Promise<BaseResponse<boolean>> => {
  return await axiosRequest.post('auth/logout', data);
};

export const getMeApi = async (): Promise<BaseResponse<AuthUserData>> => {
  return await axiosRequest.get('auth/me');
};
