export enum WashOrderStatus {
  Pending = 1,
  Deposited = 2,
  Collected = 3,
  Washing = 4,
  Drying = 5,
  Ironing = 6,
  Folding = 7,
  AwaitingPayment = 8,
  Paid = 9,
  ReadyToReturn = 10,
  Returned = 11,
  Completed = 12,
  Cancelled = 99,
}

export const WashOrderStatusLabels: Record<WashOrderStatus, { label: string; color: 'default' | 'primary' | 'secondary' | 'error' | 'info' | 'success' | 'warning' }> = {
  [WashOrderStatus.Pending]: { label: 'Chờ gửi đồ', color: 'info' },
  [WashOrderStatus.Deposited]: { label: 'Đã gửi vào tủ', color: 'primary' },
  [WashOrderStatus.Collected]: { label: 'Shipper đã lấy', color: 'warning' },
  [WashOrderStatus.Washing]: { label: 'Đang giặt', color: 'warning' },
  [WashOrderStatus.Drying]: { label: 'Đang sấy', color: 'warning' },
  [WashOrderStatus.Ironing]: { label: 'Đang ủi', color: 'warning' },
  [WashOrderStatus.Folding]: { label: 'Đang gấp', color: 'warning' },
  [WashOrderStatus.AwaitingPayment]: { label: 'Chờ thanh toán', color: 'warning' },
  [WashOrderStatus.Paid]: { label: 'Đã thanh toán', color: 'success' },
  [WashOrderStatus.ReadyToReturn]: { label: 'Chờ trả về tủ', color: 'info' },
  [WashOrderStatus.Returned]: { label: 'Đã trả về tủ', color: 'primary' },
  [WashOrderStatus.Completed]: { label: 'Hoàn tất', color: 'success' },
  [WashOrderStatus.Cancelled]: { label: 'Đã hủy', color: 'error' },
};

export enum PaymentStatus {
  Unpaid = 1,
  Paid = 2,
  Refunded = 3,
}

export const PaymentStatusLabels: Record<PaymentStatus, { label: string; color: 'default' | 'success' | 'warning' | 'error' }> = {
  [PaymentStatus.Unpaid]: { label: 'Chưa thanh toán', color: 'warning' },
  [PaymentStatus.Paid]: { label: 'Đã thanh toán', color: 'success' },
  [PaymentStatus.Refunded]: { label: 'Đã hoàn tiền', color: 'error' },
};

export enum PaymentMethod {
  Cash = 1,
  VnPay = 2,
  Momo = 3,
  ZaloPay = 4,
}