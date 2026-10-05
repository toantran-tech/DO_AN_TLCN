import { WashServiceItem } from '../../apis/wash-service/wash-service.interface';

export interface WashServiceScreenState {
  list: WashServiceItem[];
  total: number;
  loading: boolean;
}
