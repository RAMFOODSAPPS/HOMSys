/** TPAR page picker row: an Approved (or Completed) TASAF and how much of it its TPARs have used. */
export interface TparTasaf {
  id: number;
  refNo: string | null;
  title: string;
  status: string;
  salesGroupName: string | null;
  programTypeName: string | null;
  proponentName: string;
  startDate: string | null;
  endDate: string | null;
  approvedAt: string | null;
  /** The TASAF's total cost — the most its TPARs may add up to. */
  approvedAmount: number;
  /** Every TPAR but Cancelled ones (they hold their amount from Draft on). */
  tparTotal: number;
  /** Approved TPARs only — reaching the approved amount completes the TASAF. */
  approvedTotal: number;
  balance: number;
  tparCount: number;
  /** Branches a TPAR on this TASAF can be for. */
  branches: TparBranch[];
}

/** One TPAR (Trade Promo Analysis Report) entered against a TASAF. */
export interface Tpar {
  id: number;
  tasafId: number;
  tasafRefNo: string | null;
  tasafTitle: string;
  tasafStatus: string;
  tparNo: string;
  amount: number;
  siteId: number | null;
  branchName: string;
  remarks: string;
  /** Draft, Submitted, Approved, Cancelled. */
  status: string;
  createdByName: string;
  createdAt: string;
  updatedAt: string | null;
  updatedBy: string | null;
  submittedAt: string | null;
  approvedAt: string | null;
  approvedBy: string | null;
  cancelledAt: string | null;
  cancelledBy: string | null;
  // Whoever entered it, or an Admin, while the TASAF is Approved:
  /** Save changes (Draft only). */
  canEdit: boolean;
  canSubmit: boolean;
  canApprove: boolean;
  canCancel: boolean;
  /** Submitted / Approved → back to Draft (reason required) so it can be changed. */
  canRevert: boolean;
}

export interface SaveTpar {
  tasafId: number;
  siteId: number | null;
  tparNo: string;
  amount: number;
  remarks: string;
}

/** A branch (Site) a TPAR can be for. */
export interface TparBranch { siteId: number; code: string; name: string; }

export const TPAR_STATUS_SEVERITY: Record<string, 'secondary' | 'info' | 'warn' | 'success' | 'danger'> = {
  'Draft':     'secondary',
  'Submitted': 'warn',
  'Approved':  'success',
  'Cancelled': 'danger',
};
