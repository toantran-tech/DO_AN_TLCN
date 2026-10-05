import { ReactNode } from 'react';
import { SortOption } from '../../common/interfaces/api.interface';

export interface Column<T = any> {
  id: string;
  label: string;
  minWidth?: number;
  width?: number | string;
  align?: 'left' | 'center' | 'right';
  sortable?: boolean;
  filterable?: boolean;
  dataType?: 'text' | 'select' | 'date' | 'number';
  render?: (row: T, index: number) => ReactNode;
}

export interface TableLayeredProps<T = any> {
  rows: T[];
  columns: Column<T>[];
  loading?: boolean;
  total?: number;
  page?: number;
  take?: number;
  onChangePage?: (page: number) => void;
  onChangeTake?: (take: number) => void;
  sortBy?: SortOption[];
  onSort?: (field: string) => void;
  renderCollapse?: (row: T) => ReactNode;
  rowKey?: (row: T) => string;
  selectedIds?: string[];
  onSelectRows?: (ids: string[]) => void;
  emptyText?: string;
  onFilterClick?: (columnId: string, event: React.MouseEvent<HTMLElement>) => void;
  filteredColumns?: string[];
}
