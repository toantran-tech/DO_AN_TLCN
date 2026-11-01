import { axiosRequest } from '../../common/configs';
import { ResListResponse, BaseResponse } from '../../common/interfaces/api.interface';
import {
  PromotionItem,
  PromotionFilterParams,
  CreatePromotionDto,
  UpdatePromotionDto,
  PromotionStatistics,
} from './promotion.interface';

export const filterPromotions = async (
  params: PromotionFilterParams
): Promise<ResListResponse<PromotionItem>> => {
  return await axiosRequest.post('promotion/filter', params);
};

export const createPromotion = async (
  data: CreatePromotionDto
): Promise<BaseResponse<PromotionItem>> => {
  return await axiosRequest.post('promotion', data);
};

export const updatePromotion = async (
  data: UpdatePromotionDto
): Promise<BaseResponse<PromotionItem>> => {
  return await axiosRequest.put('promotion', data);
};

export const deletePromotion = async (id: string): Promise<BaseResponse<boolean>> => {
  return await axiosRequest.delete(`promotion/${id}`);
};

export const togglePromotionActive = async (id: string): Promise<BaseResponse<boolean>> => {
  return await axiosRequest.patch(`promotion/${id}/toggle-active`);
};

export const getPromotionStatistics = async (
  id: string
): Promise<BaseResponse<PromotionStatistics>> => {
  return await axiosRequest.get(`promotion/${id}/statistics`);
};

export const exportPromotionStatistics = async (id: string): Promise<Blob> => {
  const response = await axiosRequest.get(`promotion/${id}/statistics/export`, { responseType: 'blob' });
  return (response as unknown as { data: Blob }).data;
};
