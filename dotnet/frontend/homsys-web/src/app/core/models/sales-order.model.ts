export interface CustomerLookupDto {
  custKey: string;
  cusName: string;
  cKey: string;
  whseNo: number;
  custWhse: number;
  term: number;
  termDays: number;
  salesman: number;
  csMan: string;
  shipToLn1: string;
  shipToLn2: string;
  delArea: string;
  vatId: string;
  tpc: boolean;
  offshore: boolean;
  exBranch: boolean;
  cCode: number;
  /** term === 0 — enables the O.R. fields, as in the legacy form */
  isCash: boolean;
}

export interface ProductLookupDto {
  cProdNo: string;
  prodNo: number;
  prodDesc: string;
  packSize: string;
  pieces: number;
  qtyPerPc: number;
  um: string;
  priceList: boolean;
  taxRate: number;
  supplier: number;
}

/** Typeahead suggestion for the Customer Key field. */
export interface CustomerSuggestionDto {
  custKey: string;
  cusName: string;
}

/** Typeahead suggestion for the Prodno field. */
export interface ProductSuggestionDto {
  cProdNo: string;
  prodDesc: string;
  packSize: string;
  pieces: number;
}

/** Document Classification combo option (docclass.DBF). */
export interface DocClassDto {
  code: string;
  description: string;
}

/**
 * PO duplicate check result.
 *
 * alreadyEncoded is a WARNING, not an error. The legacy form shows an OK-only
 * messagebox and keeps the value, so the UI must not block on this.
 */
export interface PoCheckDto {
  poNum: string;
  alreadyEncoded: boolean;
  previousSoNo?: number | null;
  previousOrderDate?: string | null;
  previousCustKey: string;
  message: string;
}

export interface SalesOrderLineDto {
  id: number;
  lineNo: number;
  cProdNo: string;
  prodNo: number;
  prodDesc: string;
  packSize: string;
  qtyCs: number;
  qtyPc: number;
  pieces: number;
  um: string;
  priceList: boolean;
  taxRate: number;
  freeGoods: boolean;
  /** BMS-owned — last oowkdet.QTYCS/QTYPC read by the oos-status bridge sync,
   *  taken right before allocate() would delete a full-OOS row. Null until
   *  the first sync; 0 means fully out of stock. */
  allocatedQtyCs?: number | null;
  allocatedQtyPc?: number | null;
  stkFlag?: number | null;
  /** BMS-owned — last oowkdet.NETAMT read by the oos-status bridge sync,
   *  same snapshot as allocatedQtyCs. Null until the first sync; 0 means
   *  fully out of stock. */
  invNetAmt?: number | null;
}

export interface SalesOrderDto {
  soId: number;
  /** null until the Python bridge pushes the order into BMS */
  soNo?: number | null;
  custKey: string;
  cusName: string;
  orderDate: string;
  poNum: string;
  poDate?: string | null;
  /** HOMSys-only field — no legacy oowkhdr column. */
  cancelDate?: string | null;
  invRem: string;
  remarks: string;
  shipToLn1: string;
  shipToLn2: string;
  term: number;
  salesman: number;
  csMan: string;
  orNo?: number | null;
  chkDate?: string | null;
  orAmt?: number | null;
  docClass?: string | null;
  /** BMS-owned — set by the Python bridge once the order is invoiced */
  invNo?: number | null;
  invDate?: string | null;
  invAmt?: number | null;
  /** True once pushed to BMS (SoNo assigned); cleared once BMS deallocates it */
  isLocked?: boolean;
  /** True while a post-deallocation edit is waiting to be pushed back into BMS */
  needsResync?: boolean;
  /** True if BMS could not find the live record to apply the last resync onto */
  resyncFailed?: boolean;
  /** Entered / Downloaded / Processed / Deallocated / Invoiced. Display-only. */
  workflowStatus?: string;
  estAmt: number;
  createdAt: string;
  createdBy: string;
  lines: SalesOrderLineDto[];
}

export interface CreateSalesOrderLineDto {
  cProdNo: string;
  qtyCs: number;
  qtyPc: number;
  freeGoods: boolean;
}

export interface CreateSalesOrderDto {
  custKey: string;
  poNum: string;
  poDate?: string | null;
  /** HOMSys-only field — no legacy oowkhdr column. */
  cancelDate?: string | null;
  invRem: string;
  remarks: string;
  orNo?: number | null;
  chkDate?: string | null;
  orAmt?: number | null;
  docClass?: string | null;
  soTymStart?: string | null;
  /** SHA-256 hash of the source import file. Set only for orders that originated from the import wizard. */
  sourceFileHash?: string | null;
  /** Original filename of the import source. Set only for orders that originated from the import wizard. */
  sourceFileName?: string | null;
  lines: CreateSalesOrderLineDto[];
}

/** One Customer Key + PO Number pair to check for an existing Sales Order. */
export interface ImportCheckRowDto {
  custKey: string;
  poNum: string;
}

/**
 * Import file-hash check result. Unlike PoCheckDto, this IS a hard block —
 * the caller must stop the import wizard when alreadyProcessed is true.
 */
export interface FileImportCheckResultDto {
  alreadyProcessed: boolean;
  firstProcessedAt?: string | null;
  firstProcessedBy?: string | null;
}

/** Fallback Customer+PO duplicate check result. Warning only — never blocks. */
export interface RowDuplicateCheckResultDto {
  duplicateRows: ImportCheckRowDto[];
}

/** One PO Number that already exists as a real, saved Sales Order. */
export interface PoImportMatchDto {
  poNum: string;
  custKey: string;
  cusName: string;
  orderDate: string;
  encodedBy: string;
}

/**
 * Result of the early, PO-Number-only import check run right after Next in
 * the import wizard, before column mapping/customer resolution. Unlike
 * RowDuplicateCheckResultDto, this IS a hard block — it exists to catch a
 * batch that's already been saved even when the file's bytes (and hash)
 * changed, e.g. a renamed worksheet tab.
 */
export interface PoImportCheckResultDto {
  matches: PoImportMatchDto[];
}

/** Display-only price quote for the encode grid. Never persisted. */
export interface PriceQuoteDto {
  hasPrice: boolean;
  /** prod4win.NewPrice + zone.add_on + zone2.add_on, ex-VAT, per case */
  pricePerCase?: number | null;
}

/** One staged, not-yet-persisted order parsed from an Excel import, grouped by PO Number. */
export interface ImportedOrderDraft {
  custKey: string;
  poNum: string;
  poDate?: string | null;
  cancelDate?: string | null;
  remarks?: string;
  orNo?: number | null;
  chkDate?: string | null;
  orAmt?: number | null;
  sourceFileHash?: string | null;
  sourceFileName?: string | null;
  lines: { cProdNo: string; qtyCs: number }[];
}

/** Known Customer Identifier -> CustKey mapping, pre-fills the mapping dialog. */
export interface CustomerIdentifierMapDto {
  identifier: string;
  custKey: string;
  cusName: string;
}

/** Grid row while encoding — carries lookup state the DTO doesn't need. */
export interface EncodeLine {
  cProdNo: string;
  prodDesc: string;
  packSize: string;
  um: string;
  pieces: number;
  qtyCs: number;
  qtyPc: number;
  freeGoods: boolean;
  priceList: boolean;
  /** set when the product code was typed but not found */
  notFound: boolean;
  /** Price Per Case, ex-VAT — display-only, never sent on save */
  pricePerCase: number | null;
  /** BMS-owned actual invoiced Qty CS (oowkdet.QTYCS via the oos-status bridge sync) — display-only, view mode of an invoiced order only. */
  allocatedQtyCs: number | null;
  /** BMS-owned actual invoiced LP/VAT (oowkdet.NETAMT via the oos-status bridge sync) — display-only, view mode of an invoiced order only. */
  invNetAmt: number | null;
}
