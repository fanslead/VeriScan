import axios from 'axios';
import { describe, expect, it } from 'vitest';
import { ApiHttpError, mapApiError } from './errors';

describe('Problem Details 统一错误映射', () => {
  it('保留业务错误码、请求编号和重试判断', () => {
    const error = new axios.AxiosError('bad request', 'ERR_BAD_REQUEST', undefined, undefined, {
      status: 422,
      statusText: 'Unprocessable Entity',
      headers: { 'x-request-id': 'req-123' },
      config: { headers: new axios.AxiosHeaders() },
      data: {
        type: 'https://veriscan.invalid/problems/invalid_scope',
        title: 'Request validation failed',
        detail: 'API Key 授权范围无效。',
        status: 422,
        code: 'invalid_scope',
      },
    });
    const mapped = mapApiError(error);
    expect(mapped).toBeInstanceOf(ApiHttpError);
    expect(mapped.shape).toMatchObject({
      code: 'invalid_scope',
      message: 'API Key 授权范围无效。',
      status: 422,
      retryable: false,
    });
  });

  it('将无响应错误标记为可恢复', () => {
    const mapped = mapApiError(new Error('network down'));
    expect(mapped.shape).toMatchObject({ code: 'request_failed', retryable: true });
  });
});
