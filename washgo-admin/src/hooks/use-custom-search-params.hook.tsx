import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { PageOptionsDto } from '../common/interfaces/api.interface';

export const useCustomSearchParams = <T extends PageOptionsDto>(defaultParams: Partial<T>) => {
  const [searchParams, setSearchParams] = useSearchParams();

  // Stabilize defaultParams across renders even if passed as object literal
  const defaultParamsString = JSON.stringify(defaultParams);
  const stableDefaultParams = useMemo(() => defaultParams, [defaultParamsString]);

  const searchParamsString = searchParams.toString();

  const mergedParams = useMemo(() => {
    const params: Record<string, any> = { ...stableDefaultParams };

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
  }, [searchParamsString, stableDefaultParams]);

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

      if (currentSearchParams.toString() !== searchParams.toString()) {
        setSearchParams(currentSearchParams, { replace: true });
      }
    },
    [mergedParams, searchParams, setSearchParams]
  );

  const resetParams = useCallback(() => {
    const currentSearchParams = new URLSearchParams();
    Object.entries(stableDefaultParams).forEach(([key, val]) => {
      if (val !== undefined && val !== null && val !== '') {
        currentSearchParams.set(key, typeof val === 'object' ? JSON.stringify(val) : String(val));
      }
    });
    if (currentSearchParams.toString() !== searchParams.toString()) {
      setSearchParams(currentSearchParams, { replace: true });
    }
  }, [stableDefaultParams, searchParams, setSearchParams]);

  return { mergedParams, setParams, resetParams };
};
