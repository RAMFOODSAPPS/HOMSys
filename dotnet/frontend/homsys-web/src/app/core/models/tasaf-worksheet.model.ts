/**
 * TASAF worksheets — the "LF Breakdown" and "Near Ex" sheets of TASA Form_MT version template 2026.xlsx,
 * filled out by the proponent and attached to a TASAF. Computed columns follow the workbook formulas
 * (server: TasafWorksheetService.Compute; mirrored here for the live preview).
 */
export type TasafWorksheetType = 'ListingFee' | 'NearEx';
/** AllSkus: TOTAL = LF × Doors ("package rate for all SKUs"); AllDoors: TOTAL = LF × SKUs ("package rate for all doors"). */
export type TasafListingFeeBasis = 'AllSkus' | 'AllDoors';

export interface TasafListingFeeLine {
  salesGroup: string;
  channel: string;
  account: string;
  category: string;
  lfPerSku: number | null;
  skuCount: number | null;
  doorCount: number | null;
  basis: TasafListingFeeBasis;
  total: number;
  projectedAnnualSales: number | null;
  /** Fraction (0.33 = 33%). */
  cts: number | null;
  remarks: string;
}

export interface TasafNearExLine {
  salesGroup: string;
  account: string;
  category: string;
  sku: string;
  lspPerCase: number | null;
  lspPerPc: number | null;
  pcsPerCase: number | null;
  /** 'yyyy-MM-dd' (1st of the month). */
  expiry: string | null;
  invCases: number | null;
  invPieces: number | null;
  totalPcs: number;
  totalValue: number;
  moveoutScheme: string;
  /** Fraction (0.5 = 50%). */
  discount: number | null;
  moveoutCost: number;
}

export interface SaveTasafWorksheet {
  type: TasafWorksheetType;
  title: string;
  listingFeeLines: TasafListingFeeLine[];
  nearExLines: TasafNearExLine[];
}

export interface TasafWorksheet extends SaveTasafWorksheet {
  id: number;
  typeLabel: string;
  proponentUserId: number;
  proponentName: string;
  tasafId: number | null;
  tasafRefNo: string | null;
  tasafTitle: string | null;
  tasafStatus: string | null;
  /** Listing Fee: Σ TOTAL; Near Ex: Σ Moveout Cost. */
  total: number;
  /** Attached to a TASAF past Draft / Returned — read-only, can't detach. */
  locked: boolean;
  canEdit: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface TasafWorksheetListItem {
  id: number;
  type: TasafWorksheetType;
  typeLabel: string;
  title: string;
  proponentName: string;
  tasafId: number | null;
  tasafRefNo: string | null;
  tasafTitle: string | null;
  tasafStatus: string | null;
  lineCount: number;
  total: number;
  isMine: boolean;
  locked: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export const WORKSHEET_TYPES: { value: TasafWorksheetType; label: string; sheet: string }[] = [
  { value: 'ListingFee', label: 'Listing Fee',     sheet: 'LF Breakdown' },
  { value: 'NearEx',     label: 'Near Ex Moveout', sheet: 'Near Ex' },
];

export const LISTING_FEE_BASES: { value: TasafListingFeeBasis; label: string }[] = [
  { value: 'AllSkus',  label: 'Package for all SKUs (LF × Doors)' },
  { value: 'AllDoors', label: 'Package for all doors (LF × SKUs)' },
];

export function blankListingFeeLine(): TasafListingFeeLine {
  return { salesGroup: '', channel: '', account: '', category: '', lfPerSku: null, skuCount: null, doorCount: null,
           basis: 'AllSkus', total: 0, projectedAnnualSales: null, cts: null, remarks: '' };
}

export function blankNearExLine(): TasafNearExLine {
  return { salesGroup: '', account: '', category: '', sku: '', lspPerCase: null, lspPerPc: null, pcsPerCase: null,
           expiry: null, invCases: null, invPieces: null, totalPcs: 0, totalValue: 0, moveoutScheme: '', discount: null, moveoutCost: 0 };
}

const round2 = (v: number) => Math.round(v * 100) / 100;

/** LF Breakdown: TOTAL = LF/SKU × Doors (AllSkus) or × SKUs (AllDoors); CTS = TOTAL ÷ Projected Annual Sales. */
export function computeListingFee(l: TasafListingFeeLine) {
  l.total = round2((l.lfPerSku ?? 0) * ((l.basis === 'AllDoors' ? l.skuCount : l.doorCount) ?? 0));
  l.cts = l.projectedAnnualSales && l.projectedAnnualSales > 0 ? l.total / l.projectedAnnualSales : null;
}

/** Near Ex: Total in Pcs = Cases × Pcs/Cs + Pieces; Total Value = Cases × LSP/Case + Pieces × LSP/Pc; Moveout Cost = Value × Discount. */
export function computeNearEx(l: TasafNearExLine) {
  const cases = l.invCases ?? 0, pieces = l.invPieces ?? 0;
  l.totalPcs = cases * (l.pcsPerCase ?? 0) + pieces;
  l.totalValue = round2(cases * (l.lspPerCase ?? 0) + pieces * (l.lspPerPc ?? 0));
  l.moveoutCost = round2(l.totalValue * (l.discount ?? 0));
}
