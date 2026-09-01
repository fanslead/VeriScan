import axios, {
  type AxiosInstance,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios';
import { mapApiError } from './errors';

export interface ApiClient {
  get<T>(path: string, config?: AxiosRequestConfig): Promise<T>;
  post<T>(path: string, body?: unknown, config?: AxiosRequestConfig): Promise<T>;
  patch<T>(path: string, body?: unknown, config?: AxiosRequestConfig): Promise<T>;
  delete<T>(path: string, config?: AxiosRequestConfig): Promise<T>;
}

export interface RealApiClientOptions {
  baseURL?: string;
  getAccessToken?: () => string | null | Promise<string | null>;
  onUnauthorized?: () => void | Promise<void>;
  axiosInstance?: AxiosInstance;
}

type AuthRequestConfig = InternalAxiosRequestConfig & {
  authRedirectHandled?: boolean;
};

export class RealApiClient implements ApiClient {
  private readonly client: AxiosInstance;
  private unauthorizedInFlight = false;

  constructor(options: RealApiClientOptions = {}) {
    this.client =
      options.axiosInstance ??
      axios.create({
        baseURL: options.baseURL ?? '/api/admin/v1',
        timeout: 15_000,
        headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      });
    this.client.interceptors.request.use(async (config) => {
      const token = await options.getAccessToken?.();
      if (token) config.headers.Authorization = `Bearer ${token}`;
      return config;
    });
    this.client.interceptors.response.use(
      (response) => response,
      (error: unknown) => {
        const request = axios.isAxiosError(error)
          ? (error.config as AuthRequestConfig | undefined)
          : undefined;
        if (
          axios.isAxiosError(error) &&
          error.response?.status === 401 &&
          request?.authRedirectHandled !== true &&
          !this.unauthorizedInFlight
        ) {
          this.unauthorizedInFlight = true;
          let redirect: void | Promise<void>;
          try {
            redirect = options.onUnauthorized?.();
          } catch {
            redirect = undefined;
          }
          void Promise.resolve(redirect).finally(() => {
            this.unauthorizedInFlight = false;
          });
        }
        return Promise.reject(mapApiError(error));
      },
    );
  }

  async get<T>(path: string, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.get<T>(path, config);
    return response.data;
  }

  async post<T>(path: string, body?: unknown, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.post<T>(path, body, config);
    return response.data;
  }

  async patch<T>(path: string, body?: unknown, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.patch<T>(path, body, config);
    return response.data;
  }

  async delete<T>(path: string, config?: AxiosRequestConfig): Promise<T> {
    const response = await this.client.delete<T>(path, config);
    return response.data;
  }
}

export class AxiosApiClient extends RealApiClient {
  constructor(baseURL = '/api/admin/v1') {
    super({ baseURL });
  }
}
