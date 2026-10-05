import { axiosRequest } from '../../common/configs';
import { ResListResponse, BaseResponse } from '../../common/interfaces/api.interface';
import { WashServiceItem, WashServiceFilterParams } from './wash-service.interface';
import { createColumnDistinctFetcherForPrefix } from '../../common/utils/column-distinct-fetcher.util';
import { buildColumnDistinctRequestBody } from '../../common/utils/column-filter-api-body.util';

export const filterWashServices = async (
  params: WashServiceFilterParams
): Promise<ResListResponse<WashServiceItem>> => {
  return await axiosRequest.post('wash-service/filter', params);
};

export const getWashServiceDetail = async (id: string): Promise<BaseResponse<WashServiceItem>> => {
  return await axiosRequest.get(`wash-service/${id}`);
};

export const createWashService = async (body: Partial<WashServiceItem>): Promise<BaseResponse<WashServiceItem>> => {
  return await axiosRequest.post('wash-service', body);
};

export const updateWashService = async (body: Partial<WashServiceItem>): Promise<BaseResponse<WashServiceItem>> => {
  return await axiosRequest.put('wash-service', body);
};

export const fetchWashServiceColumnDistinct = createColumnDistinctFetcherForPrefix(
  'wash-service',
  buildColumnDistinctRequestBody,
  axiosRequest.post
);
