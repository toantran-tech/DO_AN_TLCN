import { useState, useCallback } from 'react';
import { ColumnFilterDto } from '../common/interfaces/api.interface';

export const useServerColumnFilters = (
  initialFilters: ColumnFilterDto[] = [],
  onChange?: (filters: ColumnFilterDto[]) => void
) => {
  const [columnFilters, setColumnFilters] = useState<ColumnFilterDto[]>(initialFilters);

  const setColumnFilter = useCallback(
    (field: string, selectedValues: string[], dataType = 'text') => {
      setColumnFilters((prev) => {
        const filtered = prev.filter((f) => f.field !== field);
        let updated: ColumnFilterDto[];

        if (selectedValues && selectedValues.length > 0) {
          updated = [...filtered, { field, dataType, selectedValues }];
        } else {
          updated = filtered;
        }

        if (onChange) onChange(updated);
        return updated;
      });
    },
    [onChange]
  );

  const clearFilter = useCallback(
    (field: string) => {
      setColumnFilters((prev) => {
        const updated = prev.filter((f) => f.field !== field);
        if (onChange) onChange(updated);
        return updated;
      });
    },
    [onChange]
  );

  const clearAllFilters = useCallback(() => {
    setColumnFilters([]);
    if (onChange) onChange([]);
  }, [onChange]);

  return {
    columnFilters,
    setColumnFilter,
    clearFilter,
    clearAllFilters,
  };
};
