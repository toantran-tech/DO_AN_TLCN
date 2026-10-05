export interface BaseEntity {
  id: string;
  createdOn: number;
  modifiedOn: number;
  createdBy?: string;
  modifiedBy?: string;
  isDeleted?: boolean;
}

export interface SortOption {
  field: string;
  isDescending: boolean;
}

export interface ColumnFilterConditionDto {
  operator?: string;
  value?: string;
  valueTo?: string;
}

export interface ColumnFilterDto {
  field: string;
  dataType?: string;
  selectedValues?: string[];
  condition?: ColumnFilterConditionDto;
}

export interface PageOptionsDto {
  page: number;
  take: number;
  search?: string;
  keyword?: string;
  sortBy?: SortOption[];
  columnFilters?: ColumnFilterDto[];
  fromDate?: number;
  toDate?: number;
  filters?: Record<string, string>;
  [key: string]: any;
}

export interface BaseResponse<T = any> {
  data: T;
  meta?: any;
  succeeded: boolean;
  message?: string;
  errors?: Record<string, string[]>;
  statusCode: number;
}

export interface ResListResponse<T> {
  list: T[];
  statusCode: number;
  currentPage: number;
  totalPages: number;
  total: number;
  pageSize: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
  succeeded: boolean;
  message?: string;
}

export interface UserSnapshot {
  id: string;
  name: string;
  code: string;
  email?: string;
  role?: string;
  avatarUrl?: string;
}

export interface ColumnFilterDistinctValueDto {
  value: any;
  label?: string;
  count: number;
}
