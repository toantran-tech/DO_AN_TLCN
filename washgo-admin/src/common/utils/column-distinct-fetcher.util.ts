import { unwrapBaseResponse } from './api-base-response.util';

export const createColumnDistinctFetcherForPrefix = (
  prefix: string,
  buildBody: (col: string, params: any) => any,
  post: (url: string, body: any) => Promise<any>
) => async (columnId: string, screenParams: any) => {
  const body = buildBody(columnId, screenParams);
  const cleanPrefix = prefix.replace(/^\/+|\/+$/g, '');
  const res = await post(`${cleanPrefix}/column-distinct-values?field=${columnId}`, body);
  return unwrapBaseResponse(res);
};
