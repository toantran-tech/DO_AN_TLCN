import { axiosRequest } from '../../common/configs';
import { ResListResponse, BaseResponse } from '../../common/interfaces/api.interface';
import {
  RatingItem,
  RatingFilterParams,
  IncidentItem,
  IncidentFilterParams,
  ResolveIncidentDto,
} from './feedback.interface';

const BE_URL = (import.meta.env.VITE_BE_URL || 'http://localhost:5000/').replace(/\/$/, '');

/** Ảnh upload được lưu ở backend dạng /uploads/...; ghép với địa chỉ backend để hiển thị. */
export const toImageUrl = (path: string) => (path.startsWith('http') ? path : `${BE_URL}${path}`);

export const filterRatings = async (params: RatingFilterParams): Promise<ResListResponse<RatingItem>> => {
  return await axiosRequest.post('rating/filter', params);
};

export const setRatingVisibility = async (
  id: string,
  isHidden: boolean,
  reason?: string
): Promise<BaseResponse<RatingItem>> => {
  return await axiosRequest.patch(`rating/${id}/visibility`, { isHidden, reason });
};

export const filterIncidents = async (params: IncidentFilterParams): Promise<ResListResponse<IncidentItem>> => {
  return await axiosRequest.post('incident/filter', params);
};

export const resolveIncident = async (id: string, data: ResolveIncidentDto): Promise<BaseResponse<IncidentItem>> => {
  return await axiosRequest.post(`incident/${id}/resolve`, data);
};

export const rejectIncident = async (id: string, reason: string): Promise<BaseResponse<IncidentItem>> => {
  return await axiosRequest.post(`incident/${id}/reject`, { reason });
};

export const confirmCompensation = async (id: string, reference: string): Promise<BaseResponse<IncidentItem>> => {
  return await axiosRequest.post(`incident/${id}/confirm-compensation`, { reference });
};
