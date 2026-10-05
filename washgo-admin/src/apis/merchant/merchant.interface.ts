import { PageOptionsDto } from '../../common/interfaces/api.interface';

export enum MerchantStatus {
  Active = 1,
  Inactive = 2,
  Suspended = 3,
}

export interface MerchantItem {
  id: string;
  businessName: string;
  taxCode?: string;
  address: string;
  district: string;
  city: string;
  latitude: number;
  longitude: number;
  phone: string;
  openingHours?: string;
  status: MerchantStatus;
  rejectionReason?: string;
  rating: number;
  totalOrders: number;
  totalRevenue: number;
  depositAmount: number;
  commissionRate: number;
  serviceRadiusKm: number;
  currentWorkload: number;
  maxCapacity: number;
  createdOn: number;
  serviceAreaIds: string[];
}

export interface MerchantFilterParams extends PageOptionsDto {
  status?: MerchantStatus;
  city?: string;
  district?: string;
}

export interface CreateMerchantDto {
  userId: string;
  businessName: string;
  taxCode?: string;
  address: string;
  district: string;
  city: string;
  latitude: number;
  longitude: number;
  phone: string;
  openingHours?: string;
  depositAmount?: number;
  commissionRate?: number;
  serviceRadiusKm?: number;
  maxCapacity?: number;
  serviceAreaIds?: string[];
}

export interface UpdateMerchantDto extends CreateMerchantDto {
  id: string;
  status: MerchantStatus;
}
