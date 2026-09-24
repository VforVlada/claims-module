export interface PagedList<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ValidationIssue {
  code: string;
  message: string;
  field?: string | null;
}

export interface Result<T> {
  value: T;
  warnings: ValidationIssue[];
}

/** RFC 7807 ProblemDetails as emitted by the API (validation → 400, invalid transition → 409, business rule → 422). */
export interface ApiErrorResponse {
  type: string;
  title: string;
  status: number;
  detail?: string | null;
  instance?: string | null;
  traceId?: string | null;
  /** Keyed by command property name, e.g. "LossDate", "Parties", "Parties[0].Name". */
  errors?: Record<string, string[]>;
  allowedNextStatuses?: (number | string)[];
}
