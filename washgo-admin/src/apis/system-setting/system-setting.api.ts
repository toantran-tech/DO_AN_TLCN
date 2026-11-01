import { axiosRequest } from '../../common/configs';
import { BaseResponse } from '../../common/interfaces/api.interface';

export interface SystemSetting {
  depositWindowMinutes: number;
  merchantCollectSlaHours: number;
  merchantReturnSlaHours: number;
  freePickupHours: number;
  defaultCommissionRate: number;
  overdueFeeDay1: number;
  overdueFeeDay2: number;
  overdueFeeDay3: number;
  storageFeePerDay: number;
  storageTransferAfterHours: number;
  cancelLatePendingPercent: number;
  cancelDepositedPercent: number;
  cancelCollectedPercent: number;
  compensationLightPercent: number;
  compensationMediumPercent: number;
  compensationSeverePercent: number;
  compensationLostPercent: number;
  updatedAt?: string | null;
}

export type SlaSettings = Pick<SystemSetting,
  'depositWindowMinutes' | 'merchantCollectSlaHours' | 'merchantReturnSlaHours' | 'freePickupHours'>;

export type PenaltySettings = Pick<SystemSetting,
  'overdueFeeDay1' | 'overdueFeeDay2' | 'overdueFeeDay3' | 'storageFeePerDay' |
  'cancelLatePendingPercent' | 'cancelDepositedPercent' | 'cancelCollectedPercent' |
  'compensationLightPercent' | 'compensationMediumPercent' | 'compensationSeverePercent' | 'compensationLostPercent'>;

export const getSystemSetting = async (): Promise<BaseResponse<SystemSetting>> => {
  return await axiosRequest.get('system-setting');
};

export const updateSlaSettings = async (data: SlaSettings): Promise<BaseResponse<SystemSetting>> => {
  return await axiosRequest.put('system-setting/sla', data);
};

export const updateCommissionSettings = async (defaultCommissionRate: number): Promise<BaseResponse<SystemSetting>> => {
  return await axiosRequest.put('system-setting/commission', { defaultCommissionRate });
};

export const updatePenaltySettings = async (data: PenaltySettings): Promise<BaseResponse<SystemSetting>> => {
  return await axiosRequest.put('system-setting/penalty', data);
};
