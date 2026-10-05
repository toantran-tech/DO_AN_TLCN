import { PageOptionsDto } from '../interfaces/api.interface';

export const buildColumnDistinctRequestBody = (columnId: string, screenParams: PageOptionsDto) => {
  const { columnFilters, ...rest } = screenParams;
  // Lọc bỏ filter của chính cột hiện tại để lấy được toàn bộ distinct values tiềm năng
  const otherColumnFilters = (columnFilters || []).filter((f) => f.field !== columnId);

  return {
    ...rest,
    columnFilters: otherColumnFilters,
    take: 200,
    page: 1,
  };
};
