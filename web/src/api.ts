// Plain fetch wrapper. Every error from the API is ProblemDetails with a "code".

export type Role = "Admin" | "Member";

export type User = { id: string; name: string; email: string; role: Role };
export type PickerUser = { id: string; name: string; role: Role };
export type Paged<T> = { items: T[]; page: number; pageSize: number; totalCount: number };

export type Location = { id: string; name: string; timeZone: string };
export type ResourceType = { id: string; name: string };
export type Resource = {
  id: string;
  locationId: string;
  locationName: string;
  timeZone: string;
  resourceTypeId: string;
  resourceTypeName: string;
  name: string;
  capacity: number | null;
  active: boolean;
};

// Start and end are UTC ISO strings; show them in the booking's timeZone.
export type Booking = {
  id: string;
  resourceId: string;
  resourceName: string;
  locationName: string;
  timeZone: string;
  ownerId: string;
  ownerName: string;
  start: string;
  end: string;
  status: string;
  attendees: number | null;
  version: number;
  createdAt: string;
};
export type ResourceBooking = { id: string; start: string; end: string; status: string; ownerName: string };

export type Violation = { code: string; message: string };
// A suggestion that comes with a rejection: this resource at this time is free and passes the rules.
export type Alternative = { resourceId: string; resourceName: string; start: string; end: string };

export class ApiError extends Error {
  status: number;
  code: string;
  errors: Record<string, string[]>;
  // Booking rules the request broke (422 RULE_VIOLATION), all at once.
  violations: Violation[];
  // Nearest valid alternatives (on SLOT_TAKEN and RULE_VIOLATION).
  alternatives: Alternative[];

  constructor(status: number, code: string, detail: string, errors: Record<string, string[]>, violations: Violation[] = [], alternatives: Alternative[] = []) {
    super(detail);
    this.status = status;
    this.code = code;
    this.errors = errors;
    this.violations = violations;
    this.alternatives = alternatives;
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

  let problem: { code?: string; detail?: string; errors?: Record<string, string[]>; violations?: Violation[]; alternatives?: Alternative[] } = {};
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
    problem.violations ?? [],
    problem.alternatives ?? [],
  );
}

// Field errors as plain sentences, e.g. "Enter a valid email address, such as name@example.com."
// The server writes each message so it makes sense without the field name.
export function describeError(error: unknown): string[] {
  if (error instanceof ApiError) {
    if (error.violations.length > 0) {
      return [error.message, ...error.violations.map((v) => v.message)];
    }
    const messages = Object.values(error.errors).flat();
    return messages.length > 0 ? [...new Set(messages)] : [error.message];
  }
  return ["Something went wrong. Try again."];
}
