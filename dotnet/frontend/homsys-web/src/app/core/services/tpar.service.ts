import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';
import { SaveTpar, Tpar, TparTasaf } from '../models/tpar.model';

/** TPAR page: TPARs entered against Approved TASAFs (permission `tpar`). */
@Injectable({ providedIn: 'root' })
export class TparService {
  private base = `${environment.apiUrl}/tpars`;

  constructor(private http: HttpClient) {}

  /** Approved TASAFs (plus Completed ones when asked) with their TPAR total and balance. */
  tasafs(includeCompleted: boolean) {
    return this.http.get<ApiResponse<TparTasaf[]>>(`${this.base}/tasafs`, { params: { includeCompleted } });
  }
  list(tasafId?: number | null) {
    return this.http.get<ApiResponse<Tpar[]>>(this.base, { params: tasafId ? { tasafId } : {} });
  }
  get(id: number) { return this.http.get<ApiResponse<Tpar>>(`${this.base}/${id}`); }
  create(dto: SaveTpar)             { return this.http.post<ApiResponse<Tpar>>(this.base, dto); }
  update(id: number, dto: SaveTpar) { return this.http.put<ApiResponse<Tpar>>(`${this.base}/${id}`, dto); }
  submit(id: number)  { return this.http.post<ApiResponse<Tpar>>(`${this.base}/${id}/submit`, {}); }
  approve(id: number) { return this.http.post<ApiResponse<Tpar>>(`${this.base}/${id}/approve`, {}); }
  /** Submitted / Approved → Draft (reason required) so it can be changed. */
  revert(id: number, comment: string) { return this.http.post<ApiResponse<Tpar>>(`${this.base}/${id}/revert`, { comment }); }
  /** Kept as Cancelled: frees its amount and its TPAR No. */
  cancel(id: number)  { return this.http.post<ApiResponse<Tpar>>(`${this.base}/${id}/cancel`, {}); }
}
