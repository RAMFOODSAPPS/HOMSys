import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';

/** One row of the TASAF budget dashboard (chainId and areaId null = a whole sales group). */
export interface TasafDashboardRow {
  salesGroupId: number;
  salesGroup: string;
  chainId: number | null;
  /** Area rows of a group shown by area (LKA) in the sales-group view. */
  areaId: number | null;
  area: string | null;
  chain: string | null;
  /** Budget entered for this row (null = none). */
  budgetEntered: number | null;
  /** Budget used: entered, else (sales group row) its chains' budgets added up. */
  budget: number;
  createdCount: number;
  createdAmount: number;
  approvedCount: number;
  approvedAmount: number;
  completedCount: number;
  completedAmount: number;
  closedCount: number;
  closedAmount: number;
  balance: number;
}

export interface TasafDashboard {
  year: number;
  years: number[];
  canEditBudget: boolean;
  bySalesGroup: TasafDashboardRow[];
  byChain: TasafDashboardRow[];
}

export interface TasafBudgetItem { salesGroupId: number; chainId: number | null; areaId: number | null; amount: number | null; }

/** TASAF Dashboard: approved annual budget vs TASAFs per sales group / account chain (permission `tasaf-dashboard`). */
@Injectable({ providedIn: 'root' })
export class TasafDashboardService {
  private base = `${environment.apiUrl}/tasaf-dashboard`;
  constructor(private http: HttpClient) {}

  get(year?: number | null) {
    return this.http.get<ApiResponse<TasafDashboard>>(this.base, { params: year ? { year } : {} });
  }
  saveBudgets(year: number, items: TasafBudgetItem[]) {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/budgets`, { year, items });
  }
}
