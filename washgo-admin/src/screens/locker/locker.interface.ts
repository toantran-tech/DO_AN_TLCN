import { LockerItem } from '../../apis/locker/locker.interface';

export interface LockerScreenState {
  list: LockerItem[];
  total: number;
  loading: boolean;
  selectedLocker: LockerItem | null;
}
