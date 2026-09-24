import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { ERROR_HANDLED_BY_CALLER, isAggregateCapRejection } from '../interceptors/error-handling-context';
import { Result } from '../../shared/models/common.models';
import {
  AdjustReserveRequest,
  OpenReserveRequest,
  RejectReserveRequest,
  ReserveComponentDto,
  ReserveHistoryActionRequest
} from '../../shared/models/reserve.models';

@Injectable({ providedIn: 'root' })
export class ReservesService {
  private urlFor(claimId: string): string {
    return `${API_BASE_URL}/claims/${claimId}/reserves`;
  }

  constructor(private readonly http: HttpClient) {}

  /** When the caller offers a manager override, the $10M-cap 422 is its to handle — no global snackbar. */
  private capContext(options: { callerHandlesAggregateCap?: boolean }): HttpContext | undefined {
    return options.callerHandlesAggregateCap ? new HttpContext().set(ERROR_HANDLED_BY_CALLER, isAggregateCapRejection) : undefined;
  }

  /** `callerHandlesAggregateCap`: see approve() — the caller offers a manager override for the $10M cap. */
  open(claimId: string, request: OpenReserveRequest, options: { callerHandlesAggregateCap?: boolean } = {}): Observable<Result<ReserveComponentDto>> {
    return this.http.post<Result<ReserveComponentDto>>(this.urlFor(claimId), request, { context: this.capContext(options) });
  }

  list(claimId: string): Observable<ReserveComponentDto[]> {
    return this.http.get<ReserveComponentDto[]>(this.urlFor(claimId));
  }

  adjust(
    claimId: string,
    reserveId: string,
    request: AdjustReserveRequest,
    options: { callerHandlesAggregateCap?: boolean } = {}
  ): Observable<Result<ReserveComponentDto>> {
    return this.http.put<Result<ReserveComponentDto>>(`${this.urlFor(claimId)}/${reserveId}`, request, { context: this.capContext(options) });
  }

  /**
   * `callerHandlesAggregateCap`: the caller will offer a manager override when the approval would
   * breach the $10M cap (BR-R-07), so that one error should not also raise the global snackbar.
   */
  approve(
    claimId: string,
    reserveId: string,
    request: ReserveHistoryActionRequest,
    options: { callerHandlesAggregateCap?: boolean } = {}
  ): Observable<ReserveComponentDto> {
    return this.http.post<ReserveComponentDto>(`${this.urlFor(claimId)}/${reserveId}/approve`, request, { context: this.capContext(options) });
  }

  reject(claimId: string, reserveId: string, request: RejectReserveRequest): Observable<ReserveComponentDto> {
    return this.http.post<ReserveComponentDto>(`${this.urlFor(claimId)}/${reserveId}/reject`, request);
  }
}
