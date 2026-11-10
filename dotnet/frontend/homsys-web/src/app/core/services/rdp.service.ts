import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';

export interface RdpDistributor {
  id: number; custKey: number; name: string; rdm: string; area: string; territory: string;
  siteId: number | null; siteName: string | null; isActive: boolean;
  lineCount: number; firstPeriod: string | null; lastPeriod: string | null; fileNames: string;
}
export interface SaveRdpDistributor {
  custKey: number; name: string; rdm: string; area: string; territory: string; siteId: number | null; isActive: boolean;
  /** What the RDP's own files call it, ";"-separated. */
  fileNames: string;
}
export interface RdpSiteOption { id: number; name: string; code: string; }

export interface RdpChannelMap { id: number; label: string; channel: string; lineCount: number; }
export interface RdpUnmappedLabel { label: string; lineCount: number; distributorCount: number; }
export interface RdpChannels { channels: string[]; maps: RdpChannelMap[]; unmapped: RdpUnmappedLabel[]; }

export interface RdpPreviewDistributor {
  distributorId: number; custKey: number; name: string; periods: string[];
  lines: number; netAmount: number; accounts: number; existingLines: number;
}
export interface RdpCodeCount { code: string; description: string; lines: number; }
export interface RdpUploadPreview {
  fileName: string; lines: number; blankRowsSkipped: number; netAmount: number; accounts: number;
  distributors: RdpPreviewDistributor[]; errors: string[]; errorCount: number;
  unknownSkus: RdpCodeCount[]; unmappedChannels: RdpCodeCount[];
  noChannelLines: number; noAccountCodeLines: number; noCasesLines: number; replacesLines: number;
  /** Rows skipped or repaired (subtotals, other principals, RDPs not in the list). */
  notes: string[];
}
export interface RdpUpload {
  id: number; fileName: string; distributors: string; periods: string; lineCount: number;
  netAmount: number; replacedLines: number; currentLines: number; uploadedAt: string; uploadedBy: string;
}

/** One imported line, in the template's column order. */
export interface RdpLine {
  id: number; custKey: number; distributorName: string; period: string; invoiceDate: string | null; invoiceNo: string;
  txnType: string; accountCode: string; accountName: string; channelLabel: string; province: string; city: string;
  salesman: string; skuCode: string; itemDesc: string; quantity: number; uom: string; netAmount: number;
}
export interface RdpLines { total: number; quantity: number; netAmount: number; rows: RdpLine[]; }
export interface RdpLinesQuery {
  uploadId?: number | null; distributorId?: number | null; period?: string | null; search?: string; skip: number; take: number;
}

export type UbaMetric = 'accounts' | 'sales' | 'cases';
export interface UbaFilter {
  from: string | null; to: string | null; areas: string[]; rdms: string[]; distributorIds: number[]; metric: UbaMetric;
  /** Channel types (as the dashboard shows them), from clicking the channel table. */
  channels?: string[];
}
export interface UbaOption { value: string; label: string; }
export interface UbaFilters { areas: string[]; rdms: string[]; distributors: RdpDistributor[]; periods: string[]; dimensions: UbaOption[]; }
export interface UbaKpi { value: number; prior: number | null; changePct: number | null; }
export interface UbaSplitRow {
  level: number; keys: string[]; label: string; area: string | null; rdm: string | null;
  months: (number | null)[]; range: number; share: number; changePct: number | null;
}
export interface UbaSplit {
  from: string; to: string; metric: UbaMetric; groupBy: string[]; periods: string[];
  rows: UbaSplitRow[]; total: UbaSplitRow; note: string | null;
}
export interface UbaSummary {
  from: string; to: string; distributors: number; likeForLikeDistributors: number;
  buyingAccounts: UbaKpi; netSales: UbaKpi; cases: UbaKpi;
  avgAccountsPerMonth: number; avgLinesPerAccount: number; skusSold: number;
  trend: { period: string; value: number; inRange: boolean }[]; byArea: UbaSplitRow[];
}

/** RDP (RAM Distributor Partner) sales files and the UBA dashboard â€” `api/rdp`. */
@Injectable({ providedIn: 'root' })
export class RdpService {
  private readonly base = `${environment.apiUrl}/rdp`;

  constructor(private http: HttpClient) {}

  // UBA dashboard
  filters() { return this.http.get<ApiResponse<UbaFilters>>(`${this.base}/uba/filters`); }
  summary(f: UbaFilter) { return this.http.post<ApiResponse<UbaSummary>>(`${this.base}/uba/summary`, f); }
  split(f: UbaFilter, groupBy: string[]) { return this.http.post<ApiResponse<UbaSplit>>(`${this.base}/uba/split`, { filter: f, groupBy }); }

  // uploads
  private form(distributorId: number | null, file: File) {
    const fd = new FormData();
    if (distributorId) fd.append('distributorId', String(distributorId));
    fd.append('file', file, file.name);
    return fd;
  }
  /** Step 1: what the file would import and replace â€” nothing is saved. */
  preview(distributorId: number | null, file: File) {
    return this.http.post<ApiResponse<RdpUploadPreview>>(`${this.base}/uploads/preview`, this.form(distributorId, file));
  }
  /** Step 2: import; replaces each distributor's months that the file contains. */
  upload(distributorId: number | null, file: File) {
    return this.http.post<ApiResponse<RdpUpload>>(`${this.base}/uploads`, this.form(distributorId, file));
  }
  uploads() { return this.http.get<ApiResponse<RdpUpload[]>>(`${this.base}/uploads`); }
  deleteUpload(id: number) { return this.http.delete<ApiResponse<boolean>>(`${this.base}/uploads/${id}`); }
  /** Imported lines in the template's columns, paged (file order). */
  lines(q: RdpLinesQuery) {
    const params: Record<string, string> = { skip: String(q.skip), take: String(q.take) };
    if (q.uploadId) params['uploadId'] = String(q.uploadId);
    if (q.distributorId) params['distributorId'] = String(q.distributorId);
    if (q.period) params['period'] = q.period;
    if (q.search?.trim()) params['search'] = q.search.trim();
    return this.http.get<ApiResponse<RdpLines>>(`${this.base}/lines`, { params });
  }

  // RDP list
  distributors() { return this.http.get<ApiResponse<RdpDistributor[]>>(`${this.base}/distributors`); }
  saveDistributor(id: number | null, dto: SaveRdpDistributor) {
    return id
      ? this.http.put<ApiResponse<RdpDistributor>>(`${this.base}/distributors/${id}`, dto)
      : this.http.post<ApiResponse<RdpDistributor>>(`${this.base}/distributors`, dto);
  }
  sites() { return this.http.get<ApiResponse<RdpSiteOption[]>>(`${this.base}/sites`); }

  // channel mapping
  channels() { return this.http.get<ApiResponse<RdpChannels>>(`${this.base}/channels`); }
  saveChannel(label: string, channel: string) { return this.http.put<ApiResponse<RdpChannelMap>>(`${this.base}/channels`, { label, channel }); }
  deleteChannel(id: number) { return this.http.delete<ApiResponse<boolean>>(`${this.base}/channels/${id}`); }
}
