import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';
import {
  BankAccountDto, BankAccountOptionDto, BankAsOfDto, BankStatementLineDto, BankStatementUploadDto, BankUploadPreviewDto, DepositCandidateDto
} from '../models/collection.model';

@Injectable({ providedIn: 'root' })
export class BankStatementService {
  private readonly base = `${environment.apiUrl}/bank-statements`;

  constructor(private http: HttpClient) {}

  asOf() { return this.http.get<ApiResponse<BankAsOfDto>>(`${this.base}/as-of`); }
  depositAccounts(siteId: number) {
    return this.http.get<ApiResponse<BankAccountOptionDto[]>>(`${this.base}/deposit-accounts`, { params: { siteId } });
  }

  accounts() { return this.http.get<ApiResponse<BankAccountDto[]>>(`${this.base}/accounts`); }
  saveAccount(id: number | null, dto: { bankCode: string; accountNo: string; accountName: string; isActive: boolean; siteIds: number[];
                                        homsAcctNo: number | null; bankAbbr: string }) {
    return id
      ? this.http.put<ApiResponse<BankAccountDto>>(`${this.base}/accounts/${id}`, dto)
      : this.http.post<ApiResponse<BankAccountDto>>(`${this.base}/accounts`, dto);
  }
  deleteAccount(id: number) { return this.http.delete<ApiResponse<number>>(`${this.base}/accounts/${id}`); }

  /** Step 1: what the file would import — nothing is saved. */
  preview(bankAccountId: number, file: File) {
    const fd = new FormData();
    fd.append('bankAccountId', String(bankAccountId));
    fd.append('file', file, file.name);
    return this.http.post<ApiResponse<BankUploadPreviewDto>>(`${this.base}/upload/preview`, fd);
  }
  /** Step 2: import after the user confirmed the previewed closing balance. */
  upload(bankAccountId: number, file: File, confirmedBalance: number | null) {
    const fd = new FormData();
    fd.append('bankAccountId', String(bankAccountId));
    fd.append('file', file, file.name);
    fd.append('confirmed', 'true');
    if (confirmedBalance !== null) fd.append('confirmedBalance', String(confirmedBalance));
    return this.http.post<ApiResponse<{ upload: BankStatementUploadDto; credits: number; matched: number }>>(`${this.base}/upload`, fd);
  }
  uploads(bankAccountId: number | null) {
    let p = new HttpParams();
    if (bankAccountId) p = p.set('bankAccountId', bankAccountId);
    return this.http.get<ApiResponse<BankStatementUploadDto[]>>(`${this.base}/uploads`, { params: p });
  }

  /** filter: all | unmatched | matched */
  lines(bankAccountId: number | null, from: string | null, to: string | null, filter: string) {
    let p = new HttpParams().set('filter', filter);
    if (bankAccountId) p = p.set('bankAccountId', bankAccountId);
    if (from) p = p.set('from', from);
    if (to) p = p.set('to', to);
    return this.http.get<ApiResponse<BankStatementLineDto[]>>(`${this.base}/lines`, { params: p });
  }
  candidates(lineId: number) { return this.http.get<ApiResponse<DepositCandidateDto[]>>(`${this.base}/lines/${lineId}/candidates`); }
  match(lineId: number, depositBatchId: number) {
    return this.http.post<ApiResponse<BankStatementLineDto>>(`${this.base}/lines/${lineId}/match`, { depositBatchId });
  }
  unmatch(lineId: number) { return this.http.post<ApiResponse<BankStatementLineDto>>(`${this.base}/lines/${lineId}/unmatch`, {}); }
}
