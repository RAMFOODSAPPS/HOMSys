import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';
import {
  BankAccountOptionDto, CollectionDto, CollectionHistoryDto, InvoiceHistoryDto, CollectionListItemDto, CollectionSiteDto, DepositInfoDto, InvoiceSyncRequestStatusDto,
  LodLetterDto, OpenInvoiceDto, PdcItemDto, SaveCollectionDto, SettlementCandidateDto
} from '../models/collection.model';

@Injectable({ providedIn: 'root' })
export class CollectionService {
  private readonly base = `${environment.apiUrl}/collections`;

  constructor(private http: HttpClient) {}

  mySites() { return this.http.get<ApiResponse<CollectionSiteDto[]>>(`${this.base}/my-sites`); }

  list(siteId?: number | null, status?: string | null) {
    let p = new HttpParams();
    if (siteId) p = p.set('siteId', siteId);
    if (status) p = p.set('status', status);
    return this.http.get<ApiResponse<CollectionListItemDto[]>>(this.base, { params: p });
  }

  get(id: number) { return this.http.get<ApiResponse<CollectionDto>>(`${this.base}/${id}`); }
  /** One posted invoice's payment history (RFCs + every collection applied to it). */
  invoiceHistory(postedInvoiceId: number) {
    return this.http.get<ApiResponse<InvoiceHistoryDto>>(`${this.base}/invoices/${postedInvoiceId}/history`);
  }

  /** Already-synced posted invoices with open balances, by customer and/or invoice numbers. */
  invoices(siteId: number, opts: { custKey?: string; docNos?: number[]; openOnly?: boolean }) {
    let p = new HttpParams().set('siteId', siteId);
    if (opts.custKey) p = p.set('custKey', opts.custKey);
    if (opts.docNos?.length) p = p.set('docNos', opts.docNos.join(','));
    if (opts.openOnly) p = p.set('openOnly', true);
    return this.http.get<ApiResponse<OpenInvoiceDto[]>>(`${this.base}/invoices`, { params: p });
  }

  create(dto: SaveCollectionDto) { return this.http.post<ApiResponse<CollectionDto>>(this.base, dto); }
  update(id: number, dto: SaveCollectionDto) { return this.http.put<ApiResponse<CollectionDto>>(`${this.base}/${id}`, dto); }
  submit(id: number) { return this.http.post<ApiResponse<CollectionDto>>(`${this.base}/${id}/submit`, {}); }
  void(id: number, reason: string) { return this.http.post<ApiResponse<CollectionDto>>(`${this.base}/${id}/void`, { reason }); }
  recordDeposit(id: number, dep: DepositInfoDto) { return this.http.post<ApiResponse<CollectionDto>>(`${this.base}/${id}/deposit`, dep); }

  // Finance Verifier
  verify(id: number) { return this.http.post<ApiResponse<CollectionDto>>(`${this.base}/${id}/verify`, {}); }
  returnToBranch(id: number, reason: string) { return this.http.post<ApiResponse<CollectionDto>>(`${this.base}/${id}/return`, { reason }); }
  retryHoms(id: number) { return this.http.post<ApiResponse<CollectionDto>>(`${this.base}/${id}/homs-posting/retry`, {}); }
  confirmMatch(id: number) { return this.http.post<ApiResponse<CollectionDto>>(`${this.base}/${id}/bank-match/confirm`, {}); }
  rejectMatch(id: number, reason: string | null) { return this.http.post<ApiResponse<CollectionDto>>(`${this.base}/${id}/bank-match/reject`, { reason }); }

  // PDC module
  pdcList(siteId: number | null, nearDays: number) {
    let p = new HttpParams().set('nearDays', nearDays);
    if (siteId) p = p.set('siteId', siteId);
    return this.http.get<ApiResponse<PdcItemDto[]>>(`${environment.apiUrl}/pdc`, { params: p });
  }
  tagPdc(id: number, outcome: 'Succeeded' | 'Bounced' | 'None', date: string | null, reason: string | null) {
    return this.http.post<ApiResponse<CollectionDto>>(`${environment.apiUrl}/pdc/${id}/tag`, { outcome, date, reason });
  }
  /** CNC Verifier: the bank credit is this matured PDC — Deposited, and its JV is queued for HOMS. */
  confirmPdcMatch(id: number) {
    return this.http.post<ApiResponse<CollectionDto>>(`${environment.apiUrl}/pdc/${id}/confirm-match`, {});
  }
  rejectPdcMatch(id: number, reason: string) {
    return this.http.post<ApiResponse<CollectionDto>>(`${environment.apiUrl}/pdc/${id}/reject-match`, { reason });
  }
  recordPdcDeposit(id: number, dep: { bankAccountId: number | null; slipRefNo: string; depositDate: string }) {
    return this.http.post<ApiResponse<CollectionDto>>(`${environment.apiUrl}/pdc/${id}/deposit`, dep);
  }
  pdcSites() {
    return this.http.get<ApiResponse<CollectionSiteDto[]>>(`${environment.apiUrl}/pdc/sites`);
  }
  pdcDepositAccounts(siteId: number) {
    return this.http.get<ApiResponse<BankAccountOptionDto[]>>(`${environment.apiUrl}/pdc/deposit-accounts`, { params: { siteId } });
  }

  // Bounced PDC follow-up (pdc-tag): Letter of Dishonor -> settled by a new CR, or endorsed for legal action
  lodSettings() { return this.http.get<ApiResponse<{ settleDays: number }>>(`${environment.apiUrl}/pdc/lod-settings`); }
  issueLod(id: number, date: string, settleDays: number) {
    return this.http.post<ApiResponse<CollectionDto>>(`${environment.apiUrl}/pdc/${id}/lod`, { date, settleDays });
  }
  lodLetter(id: number) { return this.http.get<ApiResponse<LodLetterDto>>(`${environment.apiUrl}/pdc/${id}/lod`); }
  lodReceived(id: number, date: string, file: File) {
    const form = new FormData();
    form.append('date', date);
    form.append('file', file, file.name);
    return this.http.post<ApiResponse<CollectionDto>>(`${environment.apiUrl}/pdc/${id}/lod/received`, form);
  }
  lodCopy(id: number) { return this.http.get(`${environment.apiUrl}/pdc/${id}/lod/copy`, { responseType: 'blob' }); }
  settlementCandidates(id: number) {
    return this.http.get<ApiResponse<SettlementCandidateDto[]>>(`${environment.apiUrl}/pdc/${id}/settlement-candidates`);
  }
  settleBounced(id: number, collectionId: number) {
    return this.http.post<ApiResponse<CollectionDto>>(`${environment.apiUrl}/pdc/${id}/settle`, { collectionId });
  }
  unsettleBounced(id: number, reason: string) {
    return this.http.post<ApiResponse<CollectionDto>>(`${environment.apiUrl}/pdc/${id}/unsettle`, { reason });
  }
  endorseLegal(id: number, date: string, remarks: string | null, endorsedTo: string) {
    return this.http.post<ApiResponse<CollectionDto>>(`${environment.apiUrl}/pdc/${id}/legal`, { date, remarks, endorsedTo });
  }
  /** A PDC's audit trail (works for CNC Verifiers, who have no collections access). */
  pdcHistory(id: number) {
    return this.http.get<ApiResponse<CollectionHistoryDto[]>>(`${environment.apiUrl}/pdc/${id}/history`);
  }

  addAttachment(id: number, file: File) {
    const fd = new FormData();
    fd.append('file', file, file.name);
    return this.http.post<ApiResponse<CollectionDto>>(`${this.base}/${id}/attachments`, fd);
  }
  deleteAttachment(id: number, attachmentId: number) {
    return this.http.delete<ApiResponse<CollectionDto>>(`${this.base}/${id}/attachments/${attachmentId}`);
  }
  /** Blob of the stored file (fetched with the auth header, then shown via an object URL). */
  attachment(id: number, attachmentId: number) {
    return this.http.get(`${this.base}/${id}/attachments/${attachmentId}`, { responseType: 'blob' });
  }

  /** Branch "Fetch invoices": queued for LegacyMasterWatcher at HO (~1 min). */
  requestInvoices(siteId: number, items: { invoiceNo: number; invoiceDate: string }[]) {
    return this.http.post<ApiResponse<InvoiceSyncRequestStatusDto>>(`${this.base}/invoice-sync`, { siteId, items });
  }
  invoiceRequest(id: number) {
    return this.http.get<ApiResponse<InvoiceSyncRequestStatusDto>>(`${this.base}/invoice-sync/${id}`);
  }

  // Admin: which branches a user may encode collections for.
  userBranches(userId: number) { return this.http.get<ApiResponse<number[]>>(`${environment.apiUrl}/users/${userId}/branches`); }
  setUserBranches(userId: number, siteIds: number[]) {
    return this.http.put<ApiResponse<unknown>>(`${environment.apiUrl}/users/${userId}/branches`, siteIds);
  }
}
