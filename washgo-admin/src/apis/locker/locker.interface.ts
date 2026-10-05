import { BaseEntity, PageOptionsDto } from '../../common/interfaces/api.interface';
import { LockerStatus, LockerBoxStatus, LockerBoxSize } from './locker.enum';

export interface LockerBoxItem {
  id: string;
  lockerId: string;
  boxNumber: string;
  size: LockerBoxSize;
  status: LockerBoxStatus;
  currentOrderId?: string;
}

export interface LockerItem extends BaseEntity {
  code: string;
  name: string;
  address: string;
  ward?: string;
  district: string;
  city: string;
  latitude: number;
  longitude: number;
  status: LockerStatus;
  totalBoxes: number;
  emptyBoxesCount: number;
  boxes: LockerBoxItem[];
}

export interface LockerFilterParams extends PageOptionsDto {
  status?: LockerStatus;
  city?: string;
  district?: string;
}
