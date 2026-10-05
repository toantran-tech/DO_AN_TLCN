import { axiosRequest } from '../../common/configs';
import { ResListResponse, BaseResponse } from '../../common/interfaces/api.interface';
import {
  MerchantItem,
  MerchantFilterParams,
  CreateMerchantDto,
  UpdateMerchantDto,
  MerchantStatus,
} from './merchant.interface';

export const filterMerchants = async (
  params: MerchantFilterParams
): Promise<ResListResponse<MerchantItem>> => {
  return await axiosRequest.post('merchant/filter', params);
};

export const getMerchantDetail = async (id: string): Promise<BaseResponse<MerchantItem>> => {
  return await axiosRequest.get(`merchant/${id}`);
};

export const createMerchant = async (
  data: CreateMerchantDto
): Promise<BaseResponse<MerchantItem>> => {
  return await axiosRequest.post('merchant', data);
};

export const updateMerchant = async (
  data: UpdateMerchantDto
): Promise<BaseResponse<MerchantItem>> => {
  return await axiosRequest.put('merchant', data);
};

export const updateMerchantStatus = async (body: {
  merchantId: string;
  status: MerchantStatus;
  rejectionReason?: string;
  approvedBy?: string;
}): Promise<BaseResponse<MerchantItem>> => {
  return await axiosRequest.put('merchant/status', body);
};
