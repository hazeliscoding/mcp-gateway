import { HttpErrorResponse } from '@angular/common/http';

/** RFC 7807 problem document, as produced by the gateway's OperationResult mapping. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
}

/**
 * Turns an HTTP failure into a human-readable message. Handles the gateway's
 * ProblemDetails bodies and the OAuth token endpoint's `{ error }` shape, falling
 * back to the status text.
 */
export function describeError(error: unknown, fallback = 'Something went wrong.'): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error;
    if (body && typeof body === 'object') {
      const problem = body as ProblemDetails & { error?: string; error_description?: string };
      return (
        problem.detail ??
        problem.error_description ??
        problem.error ??
        problem.title ??
        error.message ??
        fallback
      );
    }
    if (typeof body === 'string' && body.length > 0) {
      return body;
    }
    return error.message || fallback;
  }
  return fallback;
}
