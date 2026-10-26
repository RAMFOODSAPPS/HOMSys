import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';
import { CollectionSiteDto, RemittanceDto, RemittanceListItemDto, SaveRemittanceDto } from '../models/collection.model';

/** Remittance Advice (NKA): encoded by the Custodian Verifier, verified / returned as a whole by the Finance Verifier. */
@Injectable({ providedIn: 'root' })
export class RemittanceService {
  private readonly base = `${environment.apiUrl}/collections/remittances`;

  constructor(private http: HttpClient) {}

  list(status?: string | null) {
    let p = new HttpParams();
    if (status) p = p.set('status', status);
    return this.http.get<ApiResponse<RemittanceListItemDto[]>>(this.base, { params: p });
  }
  headOffice() { return this.http.get<ApiResponse<CollectionSiteDto | null>>(`${this.base}/head-office`); }
  get(id: number) { return this.http.get<ApiResponse<RemittanceDto>>(`${this.base}/${id}`); }
  create(dto: SaveRemittanceDto) { return this.http.post<ApiResponse<RemittanceDto>>(this.base, dto); }
  update(id: number, dto: SaveRemittanceDto) { return this.http.put<ApiResponse<RemittanceDto>>(`${this.base}/${id}`, dto); }

  /** New CR from its RA Excel (crId null), or a corrected Excel for an existing CR. */
  uploadCr(id: number, crId: number | null, file: File, receiptNo: string, dcrNo: string, receiptDate: string | null) {
    const fd = new FormData();
    fd.append('file', file);
    fd.append('receiptNo', receiptNo);
    fd.append('dcrNo', dcrNo);
    if (receiptDate) fd.append('receiptDate', receiptDate);
    return this.http.post<ApiResponse<RemittanceDto>>(crId ? `${this.base}/${id}/crs/${crId}/excel` : `${this.base}/${id}/crs`, fd);
  }
  updateCr(id: number, crId: number, dto: { receiptNo: string; dcrNo: string; receiptDate: string | null }) {
    return this.http.put<ApiResponse<RemittanceDto>>(`${this.base}/${id}/crs/${crId}`, dto);
  }
  removeCr(id: number, crId: number) { return this.http.delete<ApiResponse<RemittanceDto>>(`${this.base}/${id}/crs/${crId}`); }
  rematch(id: number) { return this.http.post<ApiResponse<RemittanceDto>>(`${this.base}/${id}/rematch`, {}); }
  submit(id: number) { return this.http.post<ApiResponse<RemittanceDto>>(`${this.base}/${id}/submit`, {}); }
  verify(id: number) { return this.http.post<ApiResponse<RemittanceDto>>(`${this.base}/${id}/verify`, {}); }
  returnRa(id: number, reason: string) { return this.http.post<ApiResponse<RemittanceDto>>(`${this.base}/${id}/return`, { reason }); }
  voidRa(id: number, reason: string) { return this.http.post<ApiResponse<RemittanceDto>>(`${this.base}/${id}/void`, { reason }); }
}
