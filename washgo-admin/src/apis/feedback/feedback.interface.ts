import { PageOptionsDto } from '../../common/interfaces/api.interface';

export enum IncidentType {
  Damaged = 1,
  Lost = 2,
  Other = 3,
}

export enum IncidentSeverity {
  Light = 1,
  Medium = 2,
  Severe = 3,
}

export enum IncidentStatus {
  Pending = 1,
  Resolved = 2,
  Rejected = 3,
}

export enum CompensationLevel {
  None = 0,
  Light = 1,
  Medium = 2,
  Severe = 3,
  Lost = 4,
}

export enum CompensationStatus {
  None = 0,
  Pending = 1,
  Paid = 2,
}

export enum ResponsibleParty {
  Merchant = 1,
  Shipper = 2,
  Platform = 3,
  Customer = 4,
}

export interface RatingItem {
  id: string;
  orderId: string;
  orderCode: string;
  customerId: string;
  customerName: string;
  merchantId?: string;
  merchantName?: string;
  overallStars: number;
  qualityStars?: number | null;
  speedStars?: number | null;
  attitudeStars?: number | null;
  packagingStars?: number | null;
  comment?: string;
  imageUrls: string[];
  isHidden: boolean;
  hiddenReason?: string;
  hiddenAt?: string;
  createdOn: number;
}

export interface RatingFilterParams extends PageOptionsDto {
  stars?: number;
  merchantId?: string;
  isHidden?: boolean;
}

export interface IncidentItem {
  id: string;
  orderId: string;
  orderCode: string;
  customerId: string;
  customerName: string;
  merchantId?: string;
  merchantName?: string;
  type: IncidentType;
  severity: IncidentSeverity;
  description: string;
  imageUrls: string[];
  status: IncidentStatus;
  resolutionNote?: string;
  responsibleParty?: ResponsibleParty | null;
  compensationLevel: CompensationLevel;
  itemValue: number;
  compensationAmount: number;
  compensationStatus: CompensationStatus;
  compensationReference?: string;
  compensationPaidAt?: string;
  reviewedAt?: string;
  createdOn: number;
}

export interface IncidentFilterParams extends PageOptionsDto {
  status?: IncidentStatus;
  severity?: IncidentSeverity;
  type?: IncidentType;
  merchantId?: string;
}

export interface ResolveIncidentDto {
  resolutionNote: string;
  responsibleParty?: ResponsibleParty | null;
  compensationLevel: CompensationLevel;
  itemValue: number;
}
