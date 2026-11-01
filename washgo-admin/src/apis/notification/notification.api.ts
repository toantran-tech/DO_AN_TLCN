import { axiosRequest } from '../../common/configs';
import { ResListResponse, BaseResponse } from '../../common/interfaces/api.interface';

export interface NotificationItem {
  id: string;
  type: number;
  title: string;
  content: string;
  linkType?: 'order' | 'incident' | 'rating' | null;
  linkId?: string | null;
  isRead: boolean;
  readAt?: string | null;
  createdOn: number;
}

export const NOTIFICATION_HUB_URL = `${(import.meta.env.VITE_BE_URL || 'http://localhost:5000/').replace(/\/$/, '')}/hubs/notifications`;

export const getNotifications = async (page = 1, take = 20): Promise<ResListResponse<NotificationItem>> => {
  return await axiosRequest.get('notification', { params: { page, take } });
};

export const getUnreadCount = async (): Promise<BaseResponse<number>> => {
  return await axiosRequest.get('notification/unread-count');
};

export const markNotificationRead = async (id: string): Promise<BaseResponse<NotificationItem>> => {
  return await axiosRequest.post(`notification/${id}/read`);
};

export const markAllNotificationsRead = async (): Promise<BaseResponse<number>> => {
  return await axiosRequest.post('notification/read-all');
};

export const deleteNotification = async (id: string): Promise<BaseResponse<boolean>> => {
  return await axiosRequest.delete(`notification/${id}`);
};

export const deleteAllNotifications = async (): Promise<BaseResponse<number>> => {
  return await axiosRequest.delete('notification');
};
