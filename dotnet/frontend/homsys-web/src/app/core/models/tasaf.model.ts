export interface TasafBranch {
  siteId?: number | null;
  name: string;
}

export interface TasafProduct {
  cProdNo: string;
  sku: string;
  p3MoAvgCs: number | null;
  splyCs: number | null;
  promotedVolCs: number;
  incrVolCs: number;
  lspPerCase: number;
  promotedValue: number;
}

export interface TasafCost {
  costTypeId: number | null;
  costTypeName?: string | null;
  amount: number;
  remarks: string;
}

/** Create/update body. Totals and promoted value are recalculated by the server. */
export interface SaveTasafDto {
  title: string;
  startMonth: number | null;
  startYear: number | null;
  endMonth: number | null;
  endYear: number | null;
  salesGroupId: number | null;
  areaId: number | null;
  chainId: number | null;
  custKey: string | null;
  programTypeId: number | null;
  programTypeOther: string;
  objectiveId: number | null;
  objectiveOther: string;
  targetMarketId: number | null;
  targetMarketOther: string;
  channelId: number | null;
  productGroupId: number | null;
  productGroup2Id: number | null;
  categoryId: number | null;
  category2Id: number | null;
  background: string;
  concessions: string;
  mechanics: string;
  promotedValue: number | null;
  prePromoSales: number | null;
  incrementalSales: number | null;
  payInvDeductionAp: number;
  payInvDeductionSalesman: number;
  payCheckAp: number;
  payCheckSalesman: number;
  payFreeGoodsAp: number;
  payFreeGoodsSalesman: number;
  payOffInvoiceAp: number;
  payOffInvoiceSalesman: number;
  branches: TasafBranch[];
  products: TasafProduct[];
  costs: TasafCost[];
}

export interface TasafDto extends SaveTasafDto {
  id: number;
  refNo: string | null;
  status: string;
  customerName: string | null;
  salesGroupName?: string | null;
  chainName?: string | null;
  totalCost: number;
  promotedVsPrePromo: number | null;
  incrementalVsPrePromo: number | null;
  cts: number | null;
  paymentTotal: number;
  paymentVariance: number;
  proponentUserId: number;
  proponentName: string;
  canEdit: boolean;
  canDelete: boolean;
  /** Admin: send an Approved / Rejected / For Approval TASAF back to the proponent. */
  canRevert: boolean;
  /** Proponent or Admin: withdraw (any status but Approved / Cancelled). */
  canCancel: boolean;
  /** 'approve' when the current step is the caller's. */
  myAction: 'approve' | null;
  submittedAt: string | null;
  decidedAt: string | null;
  currentStepSeq: number | null;
  /** Approval matrix used at submit, e.g. "Promo / Near Ex Moveout". */
  approvalTypeName: string;
  currentRoleCode: string | null;
  steps: TasafStep[];
  actions: TasafAction[];
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

/** One approver in the chain. Status: Pending | Approved | Returned | Rejected | Skipped (vacant). */
export interface TasafStep {
  seq: number;
  roleCode: string;
  roleName: string;
  userName: string;
  status: string;
  actedAt: string | null;
  comment: string;
}

export interface TasafAction {
  action: string;
  fromStatus: string;
  toStatus: string;
  byName: string;
  role: string;
  comment: string;
  at: string;
}

export type TasafDecision = 'approve' | 'return' | 'reject';
/** Everything the comment dialog can do: approver decisions + Admin revert + cancel. */
export type TasafDialogAction = TasafDecision | 'revert' | 'cancel';

export interface TasafListItem {
  id: number;
  refNo: string | null;
  status: string;
  title: string;
  salesGroupName: string | null;
  chainName: string | null;
  customerName: string | null;
  programTypeName: string | null;
  startMonth: number | null;
  startYear: number | null;
  endMonth: number | null;
  endYear: number | null;
  totalCost: number;
  proponentName: string;
  currentRoleCode: string | null;
  currentApproverName: string | null;
  stepCount: number;
  stepsDone: number;
  isMine: boolean;
  awaitingMe: boolean;
  submittedAt: string | null;
  createdAt: string;
  updatedAt?: string;
}

export const TASAF_STATUS_SEVERITY: Record<string, 'secondary' | 'info' | 'warn' | 'success' | 'danger' | 'contrast'> = {
  'Draft':        'secondary',
  'For Approval': 'warn',
  'Returned':     'contrast',
  'Approved':     'success',
  'Rejected':     'danger',
  'Cancelled':    'secondary',
};

export interface TasafOption {
  id: number;
  code: string;
  name: string;
  parentId?: number | null;
  /** Program types: Max CTS %. */
  amount?: number | null;
}

export interface TasafFormOptions {
  /** Keyed by master key: 'sales-groups', 'chains', 'areas', 'program-types', ... */
  lists: Record<string, TasafOption[]>;
  branches: TasafBranch[];
}

export interface TasafProductLookup {
  cProdNo: string;
  prodDesc: string;
  packSize: string;
  pieces: number;
  lspPerCase: number | null;
}

export { utc } from '../utils/utc-date';

export const MONTHS =['JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN', 'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC'];

export function formatPeriod(m: number | null, y: number | null): string {
  if (!m && !y) return '';
  return [m ? MONTHS[m - 1] : '', y ?? ''].join(' ').trim();
}
