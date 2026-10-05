export function unwrapBaseResponse<T>(result: unknown): T {
  if (typeof result !== 'object' || result === null) return result as T;
  const res = result as Record<string, unknown>;
  return (res.data ?? res.Data ?? res.list ?? res.List ?? result) as T;
}

export function getApiErrorMessage(error: unknown, fallback = 'Đã xảy ra lỗi không xác định!'): string {
  if (typeof error !== 'object' || error === null) return fallback;
  const body = error as { errors?: Record<string, string[]>; message?: string; Message?: string };

  // Ưu tiên hiển thị message validation tiếng Việt chi tiết từ FluentValidation / ModelState
  if (body.errors && typeof body.errors === 'object') {
    const list = Object.values(body.errors).flat().filter(Boolean);
    if (list.length > 0) return list.join('; ');
  }
  return body.message?.trim() || body.Message?.trim() || fallback;
}
