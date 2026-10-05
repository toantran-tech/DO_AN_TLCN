import { axiosRequest } from '../../common/configs';
import { ResListResponse, BaseResponse } from '../../common/interfaces/api.interface';
import { LockerItem, LockerFilterParams } from './locker.interface';
import { createColumnDistinctFetcherForPrefix } from '../../common/utils/column-distinct-fetcher.util';
import { buildColumnDistinctRequestBody } from '../../common/utils/column-filter-api-body.util';

export const filterLockers = async (
  params: LockerFilterParams
): Promise<ResListResponse<LockerItem>> => {
  return await axiosRequest.post('locker/filter', params);
};

export const getLockerDetail = async (id: string): Promise<BaseResponse<LockerItem>> => {
  return await axiosRequest.get(`locker/${id}`);
};

export const updateLockerBoxStatus = async (body: {
  lockerId: string;
  boxId: string;
  newStatus: number;
}): Promise<BaseResponse<boolean>> => {
  return await axiosRequest.put('locker/box-status', body);
};

export const fetchLockerColumnDistinct = createColumnDistinctFetcherForPrefix(
  'locker',
  buildColumnDistinctRequestBody,
  axiosRequest.post
);
