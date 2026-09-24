import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';
import {
  AnalyticsMeta, QueryResult, ReportSpec, SaveItemRequest, SavedItem, ValueOption
} from '../models/analytics.model';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/analytics`;
  private meta$?: Observable<AnalyticsMeta>;

  /** Dataset catalog + caller context. Cached for the session (the catalog is code-defined). */
  meta(): Observable<AnalyticsMeta> {
    this.meta$ ??= this.http.get<ApiResponse<AnalyticsMeta>>(`${this.base}/meta`).pipe(
      map(r => r.data!),
      shareReplay(1)
    );
    return this.meta$;
  }

  query(spec: ReportSpec): Observable<QueryResult> {
    return this.http.post<ApiResponse<QueryResult>>(`${this.base}/query`, spec).pipe(map(r => r.data!));
  }

  values(dataset: string, field: string, q = '', take = 100): Observable<ValueOption[]> {
    const params = new HttpParams().set('dataset', dataset).set('field', field).set('q', q).set('take', take);
    return this.http.get<ApiResponse<ValueOption[]>>(`${this.base}/values`, { params }).pipe(map(r => r.data ?? []));
  }

  export(title: string, items: { title: string; spec: ReportSpec }[]) {
    return this.http.post(`${this.base}/export`, { title, items }, { observe: 'response', responseType: 'blob' });
  }

  items(): Observable<SavedItem[]> {
    return this.http.get<ApiResponse<SavedItem[]>>(`${this.base}/items`).pipe(map(r => r.data ?? []));
  }

  item(id: number): Observable<SavedItem> {
    return this.http.get<ApiResponse<SavedItem>>(`${this.base}/items/${id}`).pipe(map(r => r.data!));
  }

  create(req: SaveItemRequest): Observable<SavedItem> {
    return this.http.post<ApiResponse<SavedItem>>(`${this.base}/items`, req).pipe(map(r => r.data!));
  }

  update(id: number, req: SaveItemRequest): Observable<SavedItem> {
    return this.http.put<ApiResponse<SavedItem>>(`${this.base}/items/${id}`, req).pipe(map(r => r.data!));
  }

  delete(id: number) {
    return this.http.delete<ApiResponse<unknown>>(`${this.base}/items/${id}`);
  }
}
