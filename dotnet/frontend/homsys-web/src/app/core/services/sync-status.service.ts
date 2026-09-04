import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { SyncStatusDto } from '../models/sync-status.model';
import { ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class SyncStatusService {
  private readonly base = `${environment.apiUrl}/sync-status`;

  constructor(private http: HttpClient) {}

  getStatus() { return this.http.get<ApiResponse<SyncStatusDto>>(this.base); }
  triggerReferenceSync() { return this.http.post<ApiResponse<string>>(`${this.base}/reference-sync`, {}); }
  triggerPricingSync() { return this.http.post<ApiResponse<string>>(`${this.base}/pricing-sync`, {}); }
}
