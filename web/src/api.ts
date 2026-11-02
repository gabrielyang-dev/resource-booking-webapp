// Plain fetch wrapper. Every error from the API is ProblemDetails with a "code".

export type Role = "Admin" | "Member";

export type User = { id: string; name: string; email: string; role: Role };
export type PickerUser = { id: string; name: string; role: Role };
export type Paged<T> = { items: T[]; page: number; pageSize: number; totalCount: number };

export class ApiError extends Error {
  status: number;
  code: string;
  errors: Record<string, string[]>;

  constructor(status: number, code: string, detail: string, errors: Record<string, string[]>) {
    super(detail);
    this.status = status;
    this.code = code;
    this.errors = errors;
  }
}

// App sets this so that any 401 (cookie gone or stale) sends the user back to the picker.
let onNotSignedIn: () => void = () => {};
export function setOnNotSignedIn(handler: () => void) {
  onNotSignedIn = handler;
}

export async function api<T>(method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(path, {
    method,
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (response.ok) {
    return response.status === 204 ? (undefined as T) : await response.json();
  }

  let problem: { code?: string; detail?: string; errors?: Record<string, string[]> } = {};
  try {
    problem = await response.json();
  } catch {
    // Not JSON: fall through to the generic message.
  }
  if (response.status === 401) {
    onNotSignedIn();
  }
  throw new ApiError(
    response.status,
    problem.code ?? "UNKNOWN",
    problem.detail ?? "Something went wrong. Try again.",
    problem.errors ?? {},
  );
}

// Field errors as one readable line each, e.g. "email: Enter a valid email address."
export function describeError(error: unknown): string[] {
  if (error instanceof ApiError) {
    const fields = Object.entries(error.errors).flatMap(([field, messages]) =>
      messages.map((message) => `${field}: ${message}`),
    );
    return fields.length > 0 ? fields : [error.message];
  }
  return ["Something went wrong. Try again."];
}
