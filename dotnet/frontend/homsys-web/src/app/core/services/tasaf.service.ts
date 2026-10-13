import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { SaveTasafDto, TasafDecision, TasafDto, TasafFormOptions, TasafListItem, TasafProductLookup } from '../models/tasaf.model';
import { ApiResponse } from '../models/auth.model';

export interface TasafCustomerSuggestion { custKey: string; cusName: string; }
/** A chain's store from the customer master (matched by the chain's name keywords). */
export interface TasafChainCustomer { custKey: string; cusName: string; branch: string; address: string; }
export interface TasafProductSuggestion { cProdNo: string; prodDesc: string; packSize: string; pieces: number; }

@Injectable({ providedIn: 'root' })
export class TasafService {
  private readonly base = `${environment.apiUrl}/tasafs`;

  constructor(private http: HttpClient) {}

  getAll()            { return this.http.get<ApiResponse<TasafListItem[]>>(this.base); }
  getById(id: number) { return this.http.get<ApiResponse<TasafDto>>(`${this.base}/${id}`); }
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
  chainCustomers(chainId: number) {
    return this.http.get<ApiResponse<{ filtered: boolean; customers: TasafChainCustomer[] }>>(`${this.base}/chain-customers/${chainId}`);
  }
  getOptions() { return this.http.get<ApiResponse<TasafFormOptions>>(`${this.base}/options`); }
  searchCustomers(term: string) {
    return this.http.get<ApiResponse<TasafCustomerSuggestion[]>>(`${this.base}/search/customer`, { params: { term } });
  }
  searchProducts(term: string) {
    return this.http.get<ApiResponse<TasafProductSuggestion[]>>(`${this.base}/search/product`, { params: { term } });
  }
  lookupProduct(cProdNo: string) {
    return this.http.get<ApiResponse<TasafProductLookup>>(`${this.base}/lookup/product/${encodeURIComponent(cProdNo)}`);
  }
}
