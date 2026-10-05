import { axiosRequest } from '../../common/configs';
import { ResListResponse, BaseResponse } from '../../common/interfaces/api.interface';
import { WashOrderRow, WashOrderDetail, WashOrderFilterParams } from './wash-order.interface';
import { createColumnDistinctFetcherForPrefix } from '../../common/utils/column-distinct-fetcher.util';
import { buildColumnDistinctRequestBody } from '../../common/utils/column-filter-api-body.util';

export const filterWashOrders = async (
  params: WashOrderFilterParams
): Promise<ResListResponse<WashOrderRow>> => {
  return await axiosRequest.post('wash-order/filter', params);
};

export const getWashOrderDetail = async (id: string): Promise<BaseResponse<WashOrderDetail>> => {
  return await axiosRequest.get(`wash-order/${id}`);
};

export const updateWashOrderStatus = async (body: {
  orderId: string;
  newStatus: number;
  note?: string;
}): Promise<BaseResponse<WashOrderDetail>> => {
  return await axiosRequest.put('wash-order/status', body);
};

export const assignShipperToOrder = async (body: {
  orderId: string;
  shipperId: string;
  shipperName: string;
}): Promise<BaseResponse<WashOrderDetail>> => {
  return await axiosRequest.put('wash-order/assign-shipper', body);
};

export const fetchWashOrderColumnDistinct = createColumnDistinctFetcherForPrefix(
  'wash-order',
  buildColumnDistinctRequestBody,
  axiosRequest.post
);
