import { BaseEntity, PageOptionsDto } from '../../common/interfaces/api.interface';
import { WashOrderStatus, PaymentStatus, PaymentMethod } from './wash-order.enum';

export interface WashOrderItem {
  id: string;
  serviceId: string;
  serviceName: string;
  quantity: number;
  unitPrice: number;
  subTotal: number;
  note?: string;
}

export interface WashOrderTimelineItem {
  id: string;
  status: WashOrderStatus;
  actorId?: string;
  actorName?: string;
  actorRole?: string;
  timestamp: number;
  note?: string;
}

export interface WashOrderRow extends BaseEntity {
  orderCode: string;
  customerId: string;
  customerName: string;
  customerPhoneNumber: string;
  lockerId: string;
  lockerName: string;
  boxId: string;
  boxNumber: string;
  returnBoxNumber?: string;
  shipperId?: string;
  shipperName?: string;
  merchantId?: string;
  merchantName?: string;
  totalAmount: number;
  discountAmount: number;
  finalAmount: number;
  status: WashOrderStatus;
  paymentStatus: PaymentStatus;
  paymentMethod: PaymentMethod;
  depositPinCode: string;
  pickupPinCode: string;
  qrCodeString: string;
  expectedDeliveryTime: number;
}

export interface WashOrderDetail extends WashOrderRow {
  customerNote?: string;
  items: WashOrderItem[];
  timeline: WashOrderTimelineItem[];
}

export interface WashOrderFilterParams extends PageOptionsDto {
  status?: WashOrderStatus;
  paymentStatus?: PaymentStatus;
  lockerId?: string;
  customerId?: string;
  shipperId?: string;
  merchantId?: string;
}
