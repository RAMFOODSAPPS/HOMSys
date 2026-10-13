import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  SaveTasafLookupDto, TasafApprovalMatrix, TasafApprovalRuleCell, TasafApprovalRules, TasafApproverCell, TasafLookupDto, TasafMasterMeta, TasafUserOption
} from '../models/tasaf-master.model';
import { ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class TasafMasterService {
  private readonly base = `${environment.apiUrl}/tasaf-masters`;

  constructor(private http: HttpClient) {}

  getMeta()  { return this.http.get<ApiResponse<TasafMasterMeta[]>>(`${this.base}/meta`); }
  getUsers() { return this.http.get<ApiResponse<TasafUserOption[]>>(`${this.base}/users`); }
  getRules() { return this.http.get<ApiResponse<TasafApprovalRules>>(`${this.base}/approval-rules`); }
  saveRules(cells: TasafApprovalRuleCell[]) {
    return this.http.put<ApiResponse<TasafApprovalRules>>(`${this.base}/approval-rules`, cells);
  }
  getMatrix() { return this.http.get<ApiResponse<TasafApprovalMatrix>>(`${this.base}/approval-matrix`); }
  saveMatrix(cells: TasafApproverCell[]) {
    return this.http.put<ApiResponse<TasafApprovalMatrix>>(`${this.base}/approval-matrix`, cells);
  }

  getAll(key: string) { return this.http.get<ApiResponse<TasafLookupDto[]>>(`${this.base}/${key}`); }
  create(key: string, dto: SaveTasafLookupDto) { return this.http.post<ApiResponse<TasafLookupDto>>(`${this.base}/${key}`, dto); }
  update(key: string, id: number, dto: SaveTasafLookupDto) { return this.http.put<ApiResponse<TasafLookupDto>>(`${this.base}/${key}/${id}`, dto); }
  delete(key: string, id: number) { return this.http.delete<ApiResponse<null>>(`${this.base}/${key}/${id}`); }
}
