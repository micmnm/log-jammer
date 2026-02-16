const BASE_URL = '/api';

export class ApiRequestError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail: string;

  constructor(status: number, title: string, detail: string) {
    super(detail || title || `API error: ${status}`);
    this.name = 'ApiRequestError';
    this.status = status;
    this.title = title;
    this.detail = detail;
  }
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
    ...options,
  });

  if (!response.ok) {
    let title = response.statusText;
    let detail = '';
    try {
      const body = await response.json();
      title = body.title || title;
      detail = body.detail || '';
    } catch {
      // Response body isn't JSON — use defaults
    }
    throw new ApiRequestError(response.status, title, detail);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, {
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
    }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
};
