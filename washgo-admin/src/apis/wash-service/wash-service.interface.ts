import { BaseEntity, PageOptionsDto } from '../../common/interfaces/api.interface';

export enum WashServiceType {
  StandardWash = 1,
  DryClean = 2,
  IronOnly = 3,
  BlanketWash = 4,
  ShoeCare = 5,
}

export const WashServiceTypeLabels: Record<WashServiceType, string> = {
  [WashServiceType.StandardWash]: 'Giặt sấy tiêu chuẩn',
  [WashServiceType.DryClean]: 'Giặt hấp / Giặt khô cao cấp',
  [WashServiceType.IronOnly]: 'Ủi / Là hơi chuyên dụng',
  [WashServiceType.BlanketWash]: 'Giặt chăn drap mền gối',
  [WashServiceType.ShoeCare]: 'Vệ sinh & Spa giày',
};

export interface WashServiceItem extends BaseEntity {
  code: string;
  name: string;
  description?: string;
  unitPrice: number;
  serviceType: WashServiceType;
  unit: string;
  estimatedDurationHours: number;
  isActive: boolean;
}

export interface WashServiceFilterParams extends PageOptionsDto {
  serviceType?: WashServiceType;
  isActive?: boolean;
}
