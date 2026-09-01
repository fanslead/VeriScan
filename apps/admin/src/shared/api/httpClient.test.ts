import axios, { AxiosError, type AxiosAdapter } from 'axios';
import { describe, expect, it } from 'vitest';
import { ApiHttpError } from './errors';
import { RealApiClient } from './httpClient';

describe('RealApiClient', () => {
  it('为管理请求自动附加内存中的 Bearer Token', async () => {
    const requests: Array<{ authorization?: string }> = [];
    const adapter: AxiosAdapter = async (config) => {
      requests.push({ authorization: config.headers.Authorization as string | undefined });
      return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config };
    };
    const client = new RealApiClient({
      axiosInstance: axios.create({ adapter }),
      getAccessToken: () => 'access-token-in-memory',
    });

    await client.get('/overview');

    expect(requests).toEqual([{ authorization: 'Bearer access-token-in-memory' }]);
  });

  it('401 只触发一次统一重新登录入口并返回 Problem Details 错误', async () => {
    let unauthorizedCalls = 0;
    const adapter: AxiosAdapter = async (config) => {
      const response = {
        data: {
          code: 'unauthorized',
          title: 'Unauthorized',
          detail: '登录状态已失效。',
          status: 401,
        },
        status: 401,
        statusText: 'Unauthorized',
        headers: {},
        config,
      };
      throw new AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, undefined, response);
    };
    const client = new RealApiClient({
      axiosInstance: axios.create({ adapter }),
      onUnauthorized: () => {
        unauthorizedCalls += 1;
      },
    });

    const results = await Promise.allSettled([
      client.get('/applications'),
      client.get('/overview'),
    ]);
    expect(results.every((result) => result.status === 'rejected')).toBe(true);
    expect(
      results.every(
        (result) => result.status === 'rejected' && result.reason instanceof ApiHttpError,
      ),
    ).toBe(true);
    expect(unauthorizedCalls).toBe(1);
  });
});
