import { PageOptionsDto } from '../../common/interfaces/api.interface';

export enum DiscountType {
  Percentage = 1,
  FixedAmount = 2,
}

export enum PromotionStatus {
  Inactive = 0,
  Upcoming = 1,
  Active = 2,
  Expired = 3,
  Exhausted = 4,
}

export interface PromotionItem {
  id: string;
  code: string;
  description?: string;
  discountType: DiscountType;
  discountValue: number;
  minOrderAmount: number;
  maxDiscountAmount?: number | null;
  startAt: string;
  endAt: string;
  usageLimit?: number | null;
  usageLimitPerCustomer?: number | null;
  usedCount: number;
  isActive: boolean;
  status: PromotionStatus;
  createdOn: number;
}

export interface PromotionFilterParams extends PageOptionsDto {
  status?: PromotionStatus;
  discountType?: DiscountType;
}

export interface CreatePromotionDto {
  code: string;
  description?: string;
  discountType: DiscountType;
  discountValue: number;
  minOrderAmount: number;
  maxDiscountAmount?: number | null;
  startAt: string;
  endAt: string;
  usageLimit?: number | null;
  usageLimitPerCustomer?: number | null;
  isActive: boolean;
}

export interface UpdatePromotionDto extends CreatePromotionDto {
  id: string;
}

export interface PromotionDailyUsage {
  date: string;
  usedCount: number;
  discountAmount: number;
  revenue: number;
}

export interface PromotionStatistics {
  promotionId: string;
  code: string;
  usedCount: number;
  totalDiscountAmount: number;
  totalOrderAmount: number;
  revenue: number;
  paidRevenue: number;
  daily: PromotionDailyUsage[];
}
