import { axiosRequest } from '../../common/configs';
import { BaseResponse, PageOptionsDto, ResListResponse } from '../../common/interfaces/api.interface';

export enum VehicleType {
  Motorbike = 1,
  Car = 2,
  Bicycle = 3,
}

export enum ShipperStatus {
  Available = 1,
  Busy = 2,
  Offline = 3,
}

export interface ShipperItem {
  id: string;
  userId: string;
  merchantId: string;
  fullName: string;
  phone?: string;
  email: string;
  vehicleType: VehicleType;
  vehiclePlate?: string;
  status: ShipperStatus;
  isActive: boolean;
  totalDeliveries: number;
  activeTaskCount: number;
  rating: number;
  identityNumber?: string;
  identityImageUrls: string[];
  createdAt: string;
}

export interface ShipperFilterParams extends PageOptionsDto {
  status?: ShipperStatus;
  isActive?: boolean;
  merchantId?: string;
}

export interface ShipperFormDto {
  merchantId?: string;
  fullName: string;
  phone: string;
  email: string;
  vehicleType: VehicleType;
  vehiclePlate?: string;
  identityNumber?: string;
  identityImageUrls?: string[];
  isActive?: boolean;
}

export const filterShippers = async (params: ShipperFilterParams): Promise<ResListResponse<ShipperItem>> => {
  return await axiosRequest.post('shipper/filter', params);
};

export const createShipper = async (data: ShipperFormDto): Promise<BaseResponse<ShipperItem>> => {
  return await axiosRequest.post('shipper', data);
};

export const updateShipper = async (id: string, data: ShipperFormDto): Promise<BaseResponse<ShipperItem>> => {
  return await axiosRequest.put(`shipper/${id}`, data);
};

/** Upload một ảnh (JPG/PNG/WEBP, tối đa 5MB); trả về đường dẫn /uploads/... */
export const uploadImage = async (file: File): Promise<BaseResponse<{ url: string }>> => {
  const form = new FormData();
  form.append('file', file);
  return await axiosRequest.post('files/images', form, { headers: { 'Content-Type': 'multipart/form-data' } });
};
