import { PageOptionsDto } from '../../common/interfaces/api.interface';

export interface ServiceAreaItem {
  id: string;
  code: string;
  name: string;
  description?: string;
  address?: string;
  district: string;
  city: string;
  latitude?: number;
  longitude?: number;
  radiusKm: number;
  isActive: boolean;
  createdOn: number;
}

export interface ServiceAreaFilterParams extends PageOptionsDto {
  isActive?: boolean;
  city?: string;
  district?: string;
}

export interface CreateServiceAreaDto {
  code: string;
  name: string;
  description?: string;
  address?: string;
  district: string;
  city: string;
  latitude?: number;
  longitude?: number;
  radiusKm: number;
  isActive?: boolean;
}

export interface UpdateServiceAreaDto extends CreateServiceAreaDto {
  id: string;
}
