import { HttpClient, HttpEvent, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { PagedList, Result } from '../../shared/models/common.models';
import {
  AddPartyRequest,
  ClaimAuditLogDto,
  ClaimDetailDto,
  ClaimDocumentDto,
  ClaimListFilter,
  ClaimListItemDto,
  ClaimPartyDto,
  CreateClaimRequest
} from '../../shared/models/claim.models';
import { ClaimStatus, DocumentType } from '../../shared/models/enums';

@Injectable({ providedIn: 'root' })
export class ClaimsService {
  private readonly baseUrl = `${API_BASE_URL}/claims`;

  constructor(private readonly http: HttpClient) {}

  create(request: CreateClaimRequest): Observable<Result<ClaimDetailDto>> {
    return this.http.post<Result<ClaimDetailDto>>(this.baseUrl, request);
  }

  list(filter: ClaimListFilter): Observable<PagedList<ClaimListItemDto>> {
    let params = new HttpParams().set('pageNumber', filter.pageNumber).set('pageSize', filter.pageSize);

    for (const status of filter.statuses ?? []) {
      params = params.append('statuses', status);
    }
    if (filter.fromDate) params = params.set('fromDate', filter.fromDate);
    if (filter.toDate) params = params.set('toDate', filter.toDate);
    if (filter.assignedHandler) params = params.set('assignedHandler', filter.assignedHandler);
    if (filter.causeOfLossCodeId) params = params.set('causeOfLossCodeId', filter.causeOfLossCodeId);

    return this.http.get<PagedList<ClaimListItemDto>>(this.baseUrl, { params });
  }

  getById(id: string): Observable<ClaimDetailDto> {
    return this.http.get<ClaimDetailDto>(`${this.baseUrl}/${id}`);
  }

  transitionStatus(id: string, newStatus: ClaimStatus): Observable<ClaimDetailDto> {
    return this.http.put<ClaimDetailDto>(`${this.baseUrl}/${id}/status`, { newStatus });
  }

  addParty(id: string, request: AddPartyRequest): Observable<ClaimPartyDto> {
    return this.http.post<ClaimPartyDto>(`${this.baseUrl}/${id}/parties`, request);
  }

  getAuditLog(id: string, pageNumber = 1, pageSize = 20): Observable<PagedList<ClaimAuditLogDto>> {
    const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    return this.http.get<PagedList<ClaimAuditLogDto>>(`${this.baseUrl}/${id}/audit`, { params });
  }

  getDocuments(id: string): Observable<ClaimDocumentDto[]> {
    return this.http.get<ClaimDocumentDto[]>(`${this.baseUrl}/${id}/documents`);
  }

  /** Multipart upload; `documentType` is sent as the optional "documentType" form field (the API defaults to Other). */
  uploadDocument(id: string, file: File, documentType?: DocumentType): Observable<HttpEvent<ClaimDocumentDto>> {
    const formData = new FormData();
    formData.append('file', file);
    if (documentType !== undefined && documentType !== null) {
      formData.append('documentType', String(documentType));
    }
    return this.http.post<ClaimDocumentDto>(`${this.baseUrl}/${id}/documents`, formData, {
      reportProgress: true,
      observe: 'events'
    });
  }
}
