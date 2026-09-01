import axios, { isAxiosError } from 'axios';
import type { ApiErrorShape } from './types';

export interface ProblemDetailsPayload {
  type?: unknown;
  title?: unknown;
  status?: unknown;
  detail?: unknown;
  instance?: unknown;
  code?: unknown;
  requestId?: unknown;
  errors?: unknown;
}

const asObject = (value: unknown): Record<string, unknown> =>
  typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};

const stringValue = (value: unknown, fallback = '') =>
  typeof value === 'string' && value.trim() ? value : fallback;

export class ApiHttpError extends Error {
  readonly shape: ApiErrorShape;

  constructor(shape: ApiErrorShape) {
    super(shape.message);
    this.name = 'ApiHttpError';
    this.shape = shape;
  }

  get status(): number | undefined {
    return this.shape.status;
  }

  get code(): string {
    return this.shape.code;
  }

  get requestId(): string | undefined {
    return this.shape.requestId;
  }

  get retryable(): boolean {
    return this.shape.retryable ?? false;
  }
}

export function mapApiError(error: unknown): ApiHttpError {
  if (error instanceof ApiHttpError) return error;

  if (isAxiosError(error)) {
    const response = error.response;
    const payload = asObject(response?.data) as ProblemDetailsPayload;
    const status = response?.status;
    const headerRequestId = response?.headers?.['x-request-id'];
    const requestId =
      stringValue(payload.requestId) ||
      (typeof headerRequestId === 'string' ? headerRequestId : undefined);
    const typeCode = stringValue(payload.type).split('/').filter(Boolean).at(-1);
    const code = stringValue(
      payload.code,
      typeCode || (status ? `http_${status}` : 'network_error'),
    );
    const message = stringValue(payload.detail) || stringValue(payload.title) || '请求暂时无法完成';
    return new ApiHttpError({
      code,
      message,
      requestId,
      status,
      retryable: !status || status >= 500 || status === 408 || status === 429,
    });
  }

  const object = asObject(error);
  const shape = asObject(object.shape);
  if (shape.code || shape.message) {
    return new ApiHttpError({
      code: stringValue(shape.code, 'request_failed'),
      message: stringValue(shape.message, '请求暂时无法完成'),
      requestId: stringValue(shape.requestId) || undefined,
      retryable: shape.retryable === true,
      status: typeof shape.status === 'number' ? shape.status : undefined,
    });
  }

  if (axios.isCancel(error)) {
    return new ApiHttpError({ code: 'request_cancelled', message: '请求已取消', retryable: false });
  }

  return new ApiHttpError({
    code: 'request_failed',
    message: error instanceof Error ? error.message : '请求暂时无法完成',
    retryable: true,
  });
}
