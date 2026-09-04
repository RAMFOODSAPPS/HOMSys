import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface ExportPricelistRequest {
  custKeys: string[];
  effectivityDate: string;
  srpMarkupPercent: number;
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

@Injectable({ providedIn: 'root' })
export class PricelistService {
  private readonly base = `${environment.apiUrl}/pricelist`;

  constructor(private http: HttpClient) {}

  preview(request: ExportPricelistRequest) {
    return this.http.post<{ success: boolean; data: PricelistPreviewResult }>(`${this.base}/preview`, request);
  }

  export(request: ExportPricelistRequest) {
    return this.http.post(`${this.base}/export`, request, {
      observe: 'response',
      responseType: 'blob'
    });
  }

  /** Triggers a browser download from the export() response, using the server's Content-Disposition filename. */
  download(response: import('@angular/common/http').HttpResponse<Blob>, fallbackName: string): void {
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
