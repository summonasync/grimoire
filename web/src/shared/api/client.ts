export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export function jsonRequest(method: "POST" | "PUT" | "PATCH", body: unknown): RequestInit {
  return { method, body: JSON.stringify(body), headers: { "Content-Type": "application/json" } };
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init);
  if (!response.ok) {
    throw await toApiError(response);
  }
  return (await response.json()) as T;
}

async function toApiError(response: Response): Promise<ApiError> {
  const payload: unknown = await response.json().catch(() => null);
  const error = extractError(payload);
  return new ApiError(
    response.status,
    error?.code ?? `http_${response.status}`,
    error?.message ?? `Request failed with status ${response.status}.`,
  );
}

function extractError(payload: unknown): { code: string; message: string } | null {
  if (typeof payload !== "object" || payload === null) {
    return null;
  }
  const error = (payload as { error?: unknown }).error;
  if (typeof error !== "object" || error === null) {
    return null;
  }
  const { code, message } = error as { code?: unknown; message?: unknown };
  return {
    code: typeof code === "string" ? code : "",
    message: typeof message === "string" ? message : "",
  };
}
