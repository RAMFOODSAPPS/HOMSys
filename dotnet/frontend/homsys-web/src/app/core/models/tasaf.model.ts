/** A participating store from the customer master. */
export interface TasafCustomerRef {
  custKey: string;
  cusName: string;
}

export interface TasafBranch {
  siteId?: number | null;
  name: string;
  /** Site code (form options only), matched against an area's branchCodes. */
  code?: string | null;
}

export interface TasafProduct {
  cProdNo: string;
  sku: string;
  p3MoAvgCs: number | null;
  splyCs: number | null;
  promotedVolCs: number;
  incrVolCs: number;
  lspPerCase: number;
  /** Server-computed price of the picked stores (read-only); null = none picked / mixed prices. */
  lspComputed?: number | null;
  promotedValue: number;
}

/** LSP / case of one SKU for the picked stores, grouped by price (POST tasafs/product-prices). */
export interface TasafSkuPrice {
  cProdNo: string;
  hasPrice: boolean;
  groups: { price: number; custKeys: string[] }[];
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
  /** Program duration, 'yyyy-MM-dd' (inclusive). */
  startDate: string | null;
  endDate: string | null;
  salesGroupId: number | null;
  /** Areas covered — multi-select. */
  areaIds: number[];
  chainId: number | null;
  /** Participating stores (Cust Keys) — any number. */
  customers: TasafCustomerRef[];
  programTypeId: number | null;
  programTypeOther: string;
  objectiveId: number | null;
  objectiveOther: string;
  targetMarketId: number | null;
  targetMarketOther: string;
  /** Channels — multi-select. */
  channelIds: number[];
  /** Product scope (Book2.xlsx): Categories and Sub-Categories multi-select; Segments follow the Sub-Categories. */
  productCategoryIds: number[];
  /** Product master category codes ("2a"); none for a picked category = the whole category. */
  subCategoryCodes: string[];
  background: string;
  concessions: string;
  mechanics: string;
  /** Why CTS is over the program type's limit — required to submit when it is. */
  ctsJustification: string;
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
  salesGroupName?: string | null;
  chainName?: string | null;
  /** Display names of the picked lookups (comma-separated for the multi-selects). */
  areaNames?: string | null;
  programTypeName?: string | null;
  objectiveName?: string | null;
  targetMarketName?: string | null;
  channelNames?: string | null;
  productCategoryNames?: string | null;
  subCategoryNames?: string | null;
  /** Segments of the picked sub-categories by name, "Green Peas, Garbanzos" (automatic). */
  segmentNames?: string | null;
  totalCost: number;
  promotedVsPrePromo: number | null;
  incrementalVsPrePromo: number | null;
  cts: number | null;
  /** The program type's Max CTS % (e.g. 8); null = no limit (8% is the guide for Account Support / Convention). */
  ctsLimitPercent?: number | null;
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
  /** Program duration, 'yyyy-MM-dd' (inclusive). */
  startDate: string | null;
  endDate: string | null;
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
  /** Areas: comma-separated Site codes (blank = all branches). */
  branchCodes?: string | null;
}

export interface TasafFormOptions {
  /** Keyed by master key: 'sales-groups', 'chains', 'areas', 'program-types', ... */
  lists: Record<string, TasafOption[]>;
  branches: TasafBranch[];
  /** Sub-Categories from the product master (Products page): code, sub-category, segment, Product Category id. */
  subCategories: TasafSubCategoryOption[];
  /** The user's own sales groups (TASAF Setup › Sales Groups › Users); empty = every group. */
  mySalesGroupIds?: number[];
}

export interface TasafSubCategoryOption {
  code: string;
  name: string;
  segment: string;
  productCategoryId: number;
}

export interface TasafProductLookup {
  cProdNo: string;
  prodDesc: string;
  packSize: string;
  pieces: number;
  lspPerCase: number | null;
}

export { utc } from '../utils/utc-date';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** 'yyyy-MM-dd' → local Date (no timezone shift). */
export function parseIsoDate(s: string | null | undefined): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s ?? '');
  return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null;
}

/** Local Date → 'yyyy-MM-dd' (not toISOString, which would shift to UTC). */
export function toIsoDate(d: Date | null | undefined): string | null {
  if (!d) return null;
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** 'yyyy-MM-dd' → "Jan 5, 2026". */
export function formatDay(s: string | null | undefined): string {
  const d = parseIsoDate(s);
  return d ? `${MONTHS[d.getMonth()]} ${d.getDate()}, ${d.getFullYear()}` : '';
}

/** "Jan 5 – Mar 31, 2026" style range for lists. */
export function formatPeriod(start: string | null, end: string | null): string {
  const s = formatDay(start), e = formatDay(end);
  return s && e && s !== e ? `${s} – ${e}` : s || e;
}

/**
 * Program duration, start and end inclusive: "92 days (3 months)", "45 days (1 month, 14 days)".
 * Null when either date is missing or end < start.
 */
export function programDuration(start: Date | null, end: Date | null): string | null {
  if (!start || !end || end < start) return null;
  const days = Math.round((Date.UTC(end.getFullYear(), end.getMonth(), end.getDate())
    - Date.UTC(start.getFullYear(), start.getMonth(), start.getDate())) / 86_400_000) + 1;
  // Whole months from the start, then the remaining days (end is inclusive → compare with end + 1).
  const stop = new Date(end.getFullYear(), end.getMonth(), end.getDate() + 1);
  let months = 0;
  while (new Date(start.getFullYear(), start.getMonth() + months + 1, start.getDate()) <= stop) months++;
  const anchor = new Date(start.getFullYear(), start.getMonth() + months, start.getDate());
  const rest = Math.round((stop.getTime() - anchor.getTime()) / 86_400_000);
  const plural = (n: number, w: string) => `${n} ${w}${n === 1 ? '' : 's'}`;
  const parts = [months ? plural(months, 'month') : '', rest ? plural(rest, 'day') : ''].filter(Boolean);
  return months ? `${plural(days, 'day')} (${parts.join(', ')})` : plural(days, 'day');
}
