import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { PolicyCoverageDto, PolicySearchResultDto } from '../../shared/models/policy.models';

@Injectable({ providedIn: 'root' })
export class PoliciesService {
  private readonly baseUrl = `${API_BASE_URL}/policies`;

  constructor(private readonly http: HttpClient) {}

  search(searchTerm: string): Observable<PolicySearchResultDto[]> {
    const params = new HttpParams().set('searchTerm', searchTerm);
    return this.http.get<PolicySearchResultDto[]>(`${this.baseUrl}/search`, { params });
  }

  getCoverage(policyId: string): Observable<PolicyCoverageDto[]> {
    return this.http.get<PolicyCoverageDto[]>(`${this.baseUrl}/${policyId}/coverage`);
  }
}
