import { Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface ExportPricelistRequest {
  custKeys: string[];
  effectivityDate: string;
  srpMarkupPercent: number;
}

export interface ExportZonePricelistRequest {
  branch: string;
  zones: string[];
  effectivityDate: string;
  srpMarkupPercent: number;
}

export interface ExportGroupedPricelistRequest {
  branch: string;
  effectivityDate: string;
  srpMarkupPercent: number;
  baselineCustKey?: string;
}

export interface PricelistCustomerColumn {
  custKey: string;
  cusName: string;
}

export interface PricelistCustomerValue {
  casePriceWithVat: number | null;
  unitPriceWithVat: number | null;
  srp: number | null;
}

export interface PricelistRow {
  cProdNo: string;
  prodDesc: string;
  packSize: string;
  pieces: number;
  caseBarcode: string;
  barcode: string;
  byCustKey: Record<string, PricelistCustomerValue>;
}

export interface PricelistCategoryGroup {
  header: string | null;
  rows: PricelistRow[];
}

export interface PricelistPreviewResult {
  effectivityDate: string;
  srpMarkupPercent: number;
  customers: PricelistCustomerColumn[];
  groups: PricelistCategoryGroup[];
}

/** Branch picker option — value is what the other endpoints expect back
 * (raw pricing-folder code or Site name); label is a friendly display name. */
export interface BranchOptionDto {
  value: string;
  label: string;
}

export interface ZonePricelistColumn {
  zone: string;
}

/** Zone picker option: code + description + a representative-customer label,
 * e.g. "1200 - South Manila - Price - Juan Dela Cruz & 42 more". */
export interface ZoneOptionDto {
  zone: string;
  description: string | null;
  label: string;
}

export interface ZonePricelistRow {
  cProdNo: string;
  prodDesc: string;
  packSize: string;
  pieces: number;
  caseBarcode: string;
  barcode: string;
  byZone: Record<string, PricelistCustomerValue>;
}

export interface ZonePricelistCategoryGroup {
  header: string | null;
  rows: ZonePricelistRow[];
}

export interface ZonePricelistPreviewResult {
  branch: string;
  effectivityDate: string;
  srpMarkupPercent: number;
  zones: ZonePricelistColumn[];
  groups: ZonePricelistCategoryGroup[];
}

@Injectable({ providedIn: 'root' })
export class PricelistService {
  private readonly base = `${environment.apiUrl}/pricelist`;

  constructor(private http: HttpClient) {}

  getBranches() {
    return this.http.get<{ success: boolean; data: BranchOptionDto[] }>(`${this.base}/branches`);
  }

  // --- Per Account ---

  preview(request: ExportPricelistRequest) {
    return this.http.post<{ success: boolean; data: PricelistPreviewResult }>(`${this.base}/preview`, request);
  }

  export(request: ExportPricelistRequest) {
    return this.http.post(`${this.base}/export`, request, {
      observe: 'response',
      responseType: 'blob'
    });
  }

  // --- Per Zone ---

  getZonesForBranch(branch: string) {
    return this.http.get<{ success: boolean; data: ZoneOptionDto[] }>(`${this.base}/zone-branches/${branch}/zones`);
  }

  zonePreview(request: ExportZonePricelistRequest) {
    return this.http.post<{ success: boolean; data: ZonePricelistPreviewResult }>(`${this.base}/zone-preview`, request);
  }

  zoneExport(request: ExportZonePricelistRequest) {
    return this.http.post(`${this.base}/zone-export`, request, {
      observe: 'response',
      responseType: 'blob'
    });
  }

  // --- Grouped by Price ---

  groupedPreview(request: ExportGroupedPricelistRequest) {
    return this.http.post<{ success: boolean; data: PricelistPreviewResult; message?: string }>(`${this.base}/grouped-preview`, request);
  }

  groupedExport(request: ExportGroupedPricelistRequest) {
    return this.http.post(`${this.base}/grouped-export`, request, {
      observe: 'response',
      responseType: 'blob'
    });
  }

  /** Triggers a browser download from an export() response, using the server's Content-Disposition filename. */
  download(response: HttpResponse<Blob>, fallbackName: string): void {
    const disposition = response.headers.get('Content-Disposition') ?? '';
    const match = /filename="?([^"]+)"?/.exec(disposition);
    const fileName = match?.[1] ?? fallbackName;

    const url = window.URL.createObjectURL(response.body!);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    window.URL.revokeObjectURL(url);
  }
}
