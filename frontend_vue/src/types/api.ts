// ============================================================
// API / HTTP Types - mirrors App.Application.v1.RestApiErrorResponse
// ============================================================

/** Standard error response from the ASP.NET Core API */
export interface RestApiErrorResponse {
  status: number
  error: string
  traceId?: string
}

/** Generic paginated result wrapper (for future paged endpoints) */
export interface PaginatedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

/** Application-level error for use in UI */
export class ApiError extends Error {
  public readonly statusCode: number
  public readonly apiMessage: string

  constructor(statusCode: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.statusCode = statusCode
    this.apiMessage = message
  }

  get isUnauthorized(): boolean {
    return this.statusCode === 401
  }

  get isForbidden(): boolean {
    return this.statusCode === 403
  }

  get isNotFound(): boolean {
    return this.statusCode === 404
  }

  get isValidationError(): boolean {
    return this.statusCode === 400
  }

  get isServerError(): boolean {
    return this.statusCode >= 500
  }
}
