import axios, { AxiosError, type AxiosInstance } from 'axios'

/**
 * A normalized API failure. Every axios error is converted into this shape so components never inspect
 * transport details. `detail` is safe to show to the player (the backend writes player-facing messages).
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly detail: string,
    readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(detail || title)
    this.name = 'ApiError'
  }

  get isUnauthorized(): boolean {
    return this.status === 401
  }

  get isRateLimited(): boolean {
    return this.status === 429
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

type UnauthorizedListener = () => void
const unauthorizedListeners = new Set<UnauthorizedListener>()

/** Lets the auth store react to expired sessions without the HTTP layer importing the store (no cycles). */
export function onUnauthorized(listener: UnauthorizedListener): () => void {
  unauthorizedListeners.add(listener)
  return () => unauthorizedListeners.delete(listener)
}

/** Camel-cases ASP.NET validation keys ("CaptainName" -> "captainName") to match form field names. */
function normalizeFieldErrors(errors: Record<string, string[]> | undefined): Record<string, string[]> {
  if (!errors) return {}
  return Object.fromEntries(
    Object.entries(errors).map(([key, value]) => [key.charAt(0).toLowerCase() + key.slice(1), value]),
  )
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error
  if (error instanceof AxiosError) {
    const status = error.response?.status ?? 0
    const problem = (error.response?.data ?? {}) as ProblemDetails
    if (status === 0) {
      return new ApiError(0, 'Connection lost', 'The ship lost contact with the station. Check your connection.')
    }
    if (status === 429) {
      return new ApiError(429, 'Easy there, captain', 'Too many attempts. Wait a minute and try again.')
    }
    return new ApiError(
      status,
      problem.title ?? error.message,
      problem.detail ?? problem.title ?? 'Something went wrong.',
      normalizeFieldErrors(problem.errors),
    )
  }
  return new ApiError(0, 'Unexpected error', error instanceof Error ? error.message : String(error))
}

/** Shared axios instance. Same-origin cookies carry the session; there are no tokens in browser storage. */
export const http: AxiosInstance = axios.create({
  baseURL: '/',
  withCredentials: true,
  timeout: 100_000,
  headers: { Accept: 'application/json' },
})

http.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    const apiError = toApiError(error)
    if (apiError.isUnauthorized) {
      unauthorizedListeners.forEach((listener) => listener())
    }
    return Promise.reject(apiError)
  },
)
