import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';

export interface CustomerMaster {
  id: number;
  branch: string;
  custKey: string;
  cusName: string;
  addrLn1: string;
  addrLn2: string;
  delAddrLn1: string;
  delAddrLn2: string;
  delArea: string;
  whseNo: number;
  serveWh: number;
  delWhse: number;
  salesman: number;
  csMan: string;
  term: number;
  termDays: number;
  cZone: string;
  /** cust4win.NSLGRP — sales group (GT / MT / LKA). */
  nSlGrp: string;
  /** cust4win.NREGION — region. */
  nRegion: string;
  /** HOMSys classification (by CustKey; blank when none): Region / Sales Group / Servicing Branch / Customer-Chain-Group. */
  region: string;
  salesGroup: string;
  servicingBranch: string;
  chainGroup: string;
  tin: string;
  vatId: string;
  subd: string;
  tpc: boolean;
  offshore: boolean;
  exBranch: boolean;
  blockInv: boolean;
  inactive: boolean;
  firstOrder: string | null;
  importedAt: string;
}

export interface CustomerPage { total: number; rows: CustomerMaster[]; }
export interface CustomerFilterOptions { regions: string[]; salesGroups: string[]; servicingBranches: string[]; chainGroups: string[]; }
export interface CustomerBranchCount { branch: string; active: number; total: number; }

export interface CustomerQuery {
  term?: string;
  branch?: string | null;
  status?: 'active' | 'inactive' | 'all';
  region?: string | null;
  salesGroup?: string | null;
  servicingBranch?: string | null;
  chainGroup?: string | null;
  /** CZone, starts with. */
  zone?: string | null;
  terms?: 'cash' | 'credit' | null;
  invBlocked?: boolean | null;
  /** 'yyyy-MM-dd'. */
  firstOrderFrom?: string | null;
  firstOrderTo?: string | null;
  page: number;
  pageSize: number;
  sortField?: string;
  sortDesc?: boolean;
}

/** Read-only customer master — synced from each branch's BMS, never edited in HOMSys. */
@Injectable({ providedIn: 'root' })
export class CustomerMasterService {
  private readonly base = `${environment.apiUrl}/customers`;

  constructor(private http: HttpClient) {}

  getPage(q: CustomerQuery) {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.term) params = params.set('term', q.term);
    if (q.branch) params = params.set('branch', q.branch);
    if (q.status) params = params.set('status', q.status);
    for (const k of ['region', 'salesGroup', 'servicingBranch', 'chainGroup', 'zone', 'terms', 'firstOrderFrom', 'firstOrderTo'] as const)
      if (q[k]) params = params.set(k, q[k]!);
    if (q.invBlocked != null) params = params.set('invBlocked', q.invBlocked);
    if (q.sortField) params = params.set('sortField', q.sortField).set('sortDesc', !!q.sortDesc);
    return this.http.get<ApiResponse<CustomerPage>>(this.base, { params });
  }

  getFilterOptions() { return this.http.get<ApiResponse<CustomerFilterOptions>>(`${this.base}/filter-options`); }

  getBranches() { return this.http.get<ApiResponse<CustomerBranchCount[]>>(`${this.base}/branches`); }
}
