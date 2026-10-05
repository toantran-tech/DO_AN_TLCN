export enum LockerStatus {
  Active = 1,
  Maintenance = 2,
  Inactive = 3,
}

export const LockerStatusLabels: Record<LockerStatus, { label: string; color: 'success' | 'warning' | 'error' | 'default' }> = {
  [LockerStatus.Active]: { label: 'Đang hoạt động', color: 'success' },
  [LockerStatus.Maintenance]: { label: 'Bảo trì', color: 'warning' },
  [LockerStatus.Inactive]: { label: 'Tạm dừng', color: 'error' },
};

export enum LockerBoxStatus {
  Available = 1,
  Occupied = 2,
  Reserved = 3,
  Maintenance = 4,
}

export const LockerBoxStatusLabels: Record<LockerBoxStatus, { label: string; color: 'success' | 'error' | 'warning' | 'default' }> = {
  [LockerBoxStatus.Available]: { label: 'Trống', color: 'success' },
  [LockerBoxStatus.Occupied]: { label: 'Có đồ', color: 'error' },
  [LockerBoxStatus.Reserved]: { label: 'Đã giữ chỗ', color: 'warning' },
  [LockerBoxStatus.Maintenance]: { label: 'Bảo trì', color: 'default' },
};

export enum LockerBoxSize {
  Small = 1,
  Standard = 2,
  Large = 3,
}