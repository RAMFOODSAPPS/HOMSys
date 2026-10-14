import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { SaveTasafDto, TasafDecision, TasafDto, TasafFormOptions, TasafListItem, TasafProductLookup, TasafSkuPrice } from '../models/tasaf.model';
import { ApiResponse } from '../models/auth.model';

export interface TasafCustomerSuggestion { custKey: string; cusName: string; }
/** A chain's store from the customer master (matched by the chain's name keywords). */
export interface TasafChainCustomer {
  custKey: string; cusName: string; branch: string; address: string;
  /** "custKey — name", added by the form for the selected chips. */
  label?: string;
}
export interface TasafProductSuggestion { cProdNo: string; prodDesc: string; packSize: string; pieces: number; }
/** Participating SKU dropdown option (active products of the picked product group / category). */
export interface TasafProductOption {
  cProdNo: string; prodDesc: string; packSize: string;
  category: string; categoryName: string | null; lspPerCase: number | null;
  /** "4800 — RAM WHOLE KERNEL CORN 425G", added by the form. */
  label?: string;
}

@Injectable({ providedIn: 'root' })
export class TasafService {
  private readonly base = `${environment.apiUrl}/tasafs`;

  constructor(private http: HttpClient) {}

  getAll()            { return this.http.get<ApiResponse<TasafListItem[]>>(this.base); }
  getById(id: number) { return this.http.get<ApiResponse<TasafDto>>(`${this.base}/${id}`); }
  /** Print / PDF: the TASAF as an HTML document (server TasafEmailRenderer) + its company logo (data: URI or null). */
  document(id: number) { return this.http.get<ApiResponse<{ html: string; logo: string | null }>>(`${this.base}/${id}/document`); }
  create(dto: SaveTasafDto)             { return this.http.post<ApiResponse<TasafDto>>(this.base, dto); }
  update(id: number, dto: SaveTasafDto) { return this.http.put<ApiResponse<TasafDto>>(`${this.base}/${id}`, dto); }
  delete(id: number)  { return this.http.delete<ApiResponse<null>>(`${this.base}/${id}`); }
  submit(id: number)  { return this.http.post<ApiResponse<TasafDto>>(`${this.base}/${id}/submit`, {}); }
  decide(id: number, decision: TasafDecision, comment: string) {
    return this.http.post<ApiResponse<TasafDto>>(`${this.base}/${id}/decision`, { decision, comment });
  }
  /** Admin: back to the proponent as Returned. */
  revert(id: number, comment: string) { return this.http.post<ApiResponse<TasafDto>>(`${this.base}/${id}/revert`, { comment }); }
  /** Proponent or Admin: withdraw — kept as Cancelled. */
  cancel(id: number, comment: string) { return this.http.post<ApiResponse<TasafDto>>(`${this.base}/${id}/cancel`, { comment }); }

  /** filtered = false when the chain has no keywords (use the free search instead). */
  /** Cust Key dropdown: stores of the chain (name keywords) and areas (branches). filtered = false → search all. */
  customerOptions(chainId: number | null, areaIds: number[], siteIds: number[] = []) {
    const params: Record<string, string | string[]> = { areaIds: areaIds.map(String), siteIds: siteIds.map(String) };
    if (chainId) params['chainId'] = String(chainId);
    return this.http.get<ApiResponse<{ filtered: boolean; customers: TasafChainCustomer[] }>>(`${this.base}/customer-options`, { params });
  }
  /** LSP / case: each SKU's price (with VAT) for the picked stores as of the start date, grouped by price. */
  productPrices(cProdNos: string[], custKeys: string[], asOf: string | null) {
    return this.http.post<ApiResponse<TasafSkuPrice[]>>(`${this.base}/product-prices`, { cProdNos, custKeys, asOf });
  }
  chainStoreCounts(areaIds: number[], siteIds: number[]) {
    const params = { areaIds: areaIds.map(String), siteIds: siteIds.map(String) };
    return this.http.get<ApiResponse<Record<number, number> | null>>(`${this.base}/chain-store-counts`, { params });
  }
  getOptions() { return this.http.get<ApiResponse<TasafFormOptions>>(`${this.base}/options`); }
  searchCustomers(term: string) {
    return this.http.get<ApiResponse<TasafCustomerSuggestion[]>>(`${this.base}/search/customer`, { params: { term } });
  }
  productOptions(scope: { productGroupId: number | null; categoryId: number | null; productGroup2Id: number | null; category2Id: number | null }) {
    const params: Record<string, string> = {};
    for (const [k, v] of Object.entries(scope)) if (v != null) params[k] = String(v);
    return this.http.get<ApiResponse<TasafProductOption[]>>(`${this.base}/product-options`, { params });
  }
  searchProducts(term: string) {
    return this.http.get<ApiResponse<TasafProductSuggestion[]>>(`${this.base}/search/product`, { params: { term } });
  }
  lookupProduct(cProdNo: string) {
    return this.http.get<ApiResponse<TasafProductLookup>>(`${this.base}/lookup/product/${encodeURIComponent(cProdNo)}`);
  }
}
