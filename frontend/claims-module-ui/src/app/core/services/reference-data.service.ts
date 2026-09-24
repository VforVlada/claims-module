import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { CauseOfLossCodeDto, ClaimStatusDto } from '../../shared/models/reference-data.models';

@Injectable({ providedIn: 'root' })
export class ReferenceDataService {
  private readonly baseUrl = `${API_BASE_URL}/reference`;

  constructor(private readonly http: HttpClient) {}

  listCauseOfLossCodes(perilCategory?: string): Observable<CauseOfLossCodeDto[]> {
    const params = perilCategory ? new HttpParams().set('perilCategory', perilCategory) : undefined;
    return this.http
      .get<CauseOfLossCodeDto[]>(`${this.baseUrl}/cause-of-loss-codes`, { params })
      // Alphabetical by description, the text every cause-of-loss dropdown shows.
      .pipe(map((codes) => [...codes].sort((a, b) => a.description.localeCompare(b.description))));
  }

  listClaimStatuses(): Observable<ClaimStatusDto[]> {
    return this.http.get<ClaimStatusDto[]>(`${this.baseUrl}/claim-statuses`);
  }
}
