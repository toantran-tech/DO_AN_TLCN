import { WashOrderRow, WashOrderFilterParams } from '../../apis/wash-order/wash-order.interface';

export interface WashOrderScreenState {
  list: WashOrderRow[];
  total: number;
  loading: boolean;
  selectedOrderId: string | null;
  detailDialogOpen: boolean;
}

export type WashOrderScreenParams = WashOrderFilterParams;
