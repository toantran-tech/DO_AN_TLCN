import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { PageOptionsDto } from '../common/interfaces/api.interface';

export const useCustomSearchParams = <T extends PageOptionsDto>(defaultParams: Partial<T>) => {
  const [searchParams, setSearchParams] = useSearchParams();

  const mergedParams = useMemo(() => {
    const params: Record<string, any> = { ...defaultParams };

    searchParams.forEach((value, key) => {
      if (key === 'page' || key === 'take' || key === 'fromDate' || key === 'toDate') {
        const num = Number(value);
        if (!isNaN(num)) params[key] = num;
      } else if (key === 'columnFilters' || key === 'sortBy') {
        try {
          params[key] = JSON.parse(value);
        } catch {
          // ignore json parse error
        }
      } else {
        params[key] = value;
      }
    });

    return params as T;
  }, [searchParams, defaultParams]);

  const setParams = useCallback(
    (newParams: Partial<T>) => {
      const updated: Record<string, any> = { ...mergedParams, ...newParams };
      const currentSearchParams = new URLSearchParams();

      Object.entries(updated).forEach(([key, val]) => {
        if (val === undefined || val === null || val === '') return;
        if (typeof val === 'object') {
          if (Array.isArray(val) && val.length === 0) return;
          currentSearchParams.set(key, JSON.stringify(val));
        } else {
          currentSearchParams.set(key, String(val));
        }
      });

      setSearchParams(currentSearchParams, { replace: true });
    },
    [mergedParams, setSearchParams]
  );

  const resetParams = useCallback(() => {
    const currentSearchParams = new URLSearchParams();
    Object.entries(defaultParams).forEach(([key, val]) => {
      if (val !== undefined && val !== null && val !== '') {
        currentSearchParams.set(key, typeof val === 'object' ? JSON.stringify(val) : String(val));
      }
    });
    setSearchParams(currentSearchParams, { replace: true });
  }, [defaultParams, setSearchParams]);

  return { mergedParams, setParams, resetParams };
};
