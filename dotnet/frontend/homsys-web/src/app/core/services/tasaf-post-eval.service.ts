import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';
import { TasafStep } from '../models/tasaf.model';

/** Post Evaluation list row: a Completed / Closed TASAF and its post evaluation (status null = not started). */
export interface PostEvalListItem {
  tasafId: number;
  refNo: string | null;
  title: string;
  tasafStatus: string;
  salesGroupName: string | null;
  programTypeName: string | null;
  proponentName: string;
  startDate: string | null;
  endDate: string | null;
  kind: 'Promo' | 'Activity';
  postEvalId: number | null;
  status: string | null;
  plannedCost: number;
  actualCost: number | null;
  overBudget: boolean | null;
  currentRoleCode: string | null;
  currentApproverName: string | null;
  awaitingMe: boolean;
  isMine: boolean;
  submittedAt: string | null;
  decidedAt: string | null;
}

export interface PostEvalLine {
  lineNo: number;
  cProdNo: string;
  sku: string;
  lspPerCase: number;
  prePromoCs: number | null;
  plannedCs: number;
  actualCs: number | null;
  postPromoCs: number | null;
}

export interface PostEval {
  id: number;
  tasafId: number;
  kind: 'Promo' | 'Activity';
  /** New (not saved), Draft, For Approval, Returned, Approved. */
  status: string;
  refNo: string | null;
  title: string;
  tasafStatus: string;
  salesGroupName: string | null;
  areaNames: string | null;
  chainNames: string | null;
  custKeys: string | null;
  custKeyCount: number;
  programTypeName: string | null;
  objectiveName: string | null;
  targetMarketName: string | null;
  channelNames: string | null;
  productCategoryNames: string | null;
  segmentNames: string | null;
  mechanics: string;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  proponentName: string;
  tparApprovedTotal: number;
  actualStartDate: string | null;
  actualEndDate: string | null;
  plannedDoors: number | null;
  actualDoors: number | null;
  explanation: string;
  conclusion: string;
  plannedCost: number;
  actualCost: number | null;
  overBudget: boolean | null;
  lines: PostEvalLine[];
  approvalTypeName: string;
  steps: TasafStep[];
  currentRoleCode: string | null;
  currentApproverName: string | null;
  submittedAt: string | null;
  decidedAt: string | null;
  createdByName: string;
  createdAt: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
  canEdit: boolean;
  canSubmit: boolean;
  awaitingMe: boolean;
  canAdminDecide: boolean;
  canRevert: boolean;
  attachments: PostEvalAttachment[];
  canAttach: boolean;
}

export interface PostEvalAttachment {
  id: number;
  fileName: string;
  contentType: string;
  size: number;
  uploadedAt: string;
  uploadedBy: string;
}

export interface SavePostEval {
  actualStartDate: string | null;
  actualEndDate: string | null;
  actualDoors: number | null;
  explanation: string;
  conclusion: string;
  actualCost: number | null;
  lines: { lineNo: number; actualCs: number | null; postPromoCs: number | null }[];
}

export const POST_EVAL_STATUS_SEVERITY: Record<string, 'secondary' | 'info' | 'warn' | 'success' | 'danger' | 'contrast'> = {
  'New':          'info',
  'Draft':        'secondary',
  'For Approval': 'warn',
  'Returned':     'contrast',
  'Approved':     'success',
};

/** Post Evaluation of Completed TASAFs (permission `tasaf`); its approval closes the TASAF. Keyed by the TASAF id. */
@Injectable({ providedIn: 'root' })
export class TasafPostEvalService {
  private base = `${environment.apiUrl}/tasaf-post-evals`;
  constructor(private http: HttpClient) {}

  list() { return this.http.get<ApiResponse<PostEvalListItem[]>>(this.base); }
  get(tasafId: number) { return this.http.get<ApiResponse<PostEval>>(`${this.base}/${tasafId}`); }
  save(tasafId: number, dto: SavePostEval) { return this.http.put<ApiResponse<PostEval>>(`${this.base}/${tasafId}`, dto); }
  submit(tasafId: number, dto: SavePostEval) { return this.http.post<ApiResponse<PostEval>>(`${this.base}/${tasafId}/submit`, dto); }
  decide(tasafId: number, decision: 'approve' | 'return', comment: string) {
    return this.http.post<ApiResponse<PostEval>>(`${this.base}/${tasafId}/decide`, { decision, comment });
  }
  revert(tasafId: number, comment: string) { return this.http.post<ApiResponse<PostEval>>(`${this.base}/${tasafId}/revert`, { comment }); }
  addAttachment(tasafId: number, file: File) {
    const fd = new FormData(); fd.append('file', file, file.name);
    return this.http.post<ApiResponse<PostEval>>(`${this.base}/${tasafId}/attachments`, fd);
  }
  deleteAttachment(tasafId: number, id: number) { return this.http.delete<ApiResponse<PostEval>>(`${this.base}/${tasafId}/attachments/${id}`); }
  attachment(tasafId: number, id: number) { return this.http.get(`${this.base}/${tasafId}/attachments/${id}`, { responseType: 'blob' }); }
}
