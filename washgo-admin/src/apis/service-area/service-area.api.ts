import { axiosRequest } from '../../common/configs';
import { ResListResponse, BaseResponse } from '../../common/interfaces/api.interface';
import {
  ServiceAreaItem,
  ServiceAreaFilterParams,
  CreateServiceAreaDto,
  UpdateServiceAreaDto,
} from './service-area.interface';

export const filterServiceAreas = async (
  params: ServiceAreaFilterParams
): Promise<ResListResponse<ServiceAreaItem>> => {
  return await axiosRequest.post('service-area/filter', params);
};

export const getServiceAreaDetail = async (id: string): Promise<BaseResponse<ServiceAreaItem>> => {
  return await axiosRequest.get(`service-area/${id}`);
};

export const createServiceArea = async (
  data: CreateServiceAreaDto
): Promise<BaseResponse<ServiceAreaItem>> => {
  return await axiosRequest.post('service-area', data);
};

export const updateServiceArea = async (
  data: UpdateServiceAreaDto
): Promise<BaseResponse<ServiceAreaItem>> => {
  return await axiosRequest.put('service-area', data);
};

export const toggleServiceAreaActive = async (id: string): Promise<BaseResponse<boolean>> => {
  return await axiosRequest.patch(`service-area/${id}/toggle-active`);
};
