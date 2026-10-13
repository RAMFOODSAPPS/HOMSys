import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';

export interface ProductCatalogItem {
  id: number;
  prodNo: number;
  cProdNo: string;
  prodDesc: string;
  packSize: string;
  pieces: number;
  um: string;
  /** List price per case, ex-VAT (prod4win.NEWPRICE). */
  newPrice: number | null;
  priceFrom: string | null;
  oldPrice1: number | null;
  srp: number | null;
  taxRate: number;
  category: string;
  groupDesc: string | null;
  subCat: string | null;
  groupNo: number;
  seqNo: number;
  brand: string;
  sBrand: string;
  barcode: string;
  caseBarcode: string;
  priceList: boolean;
  phOut: boolean;
  /** Active | Phased Out | Delisted */
  status: string;
  importedAt: string;
}

export interface PriceHistoryRow {
  effective: string | null;
  /** Price with VAT as of Effective (PRCHST.NPAFVAT). */
  npAfVat: number;
}

/** Read-only product master — synced from BMS, never edited in HOMSys. */
@Injectable({ providedIn: 'root' })
export class ProductCatalogService {
  private readonly base = `${environment.apiUrl}/products`;

  constructor(private http: HttpClient) {}

  getAll() { return this.http.get<ApiResponse<ProductCatalogItem[]>>(this.base); }
  getPriceHistory(prodNo: number) {
    return this.http.get<ApiResponse<PriceHistoryRow[]>>(`${this.base}/${prodNo}/price-history`);
  }
}
