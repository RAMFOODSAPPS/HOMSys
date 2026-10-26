/** Collections (branch payments applied to EOD-mirrored posted invoices). Dates are 'yyyy-MM-dd'. */

export type ReceiptType = 'CR' | 'PR' | 'AR';
/** Accepted: CR = BankTransfer (bank transfer / direct deposit), PR/AR = PostDatedCheck (user 2026-10-07).
 *  Cash and Check stay in the type only for rows saved before that. */
export type PaymentMode = 'Cash' | 'Check' | 'PostDatedCheck' | 'BankTransfer';
/** RfcBoRtv: returns / bad orders / RTV a chain deducts on its Remittance Advice (RA CRs only). */
export type DeductionType = 'Ewt2307' | 'Discount' | 'Penalty' | 'Other' | 'RfcBoRtv';

export interface CollectionSiteDto { id: number; code: string; name: string; }

export interface OpenInvoiceDto {
  id: number; docNo: number; docDate: string; custKey: string; custName: string; term: number; poNum: string | null;
  total: number; rfcTotal: number; paid: number; balance: number;
}

/** bankAccountId = company account deposited into; bank is its label (set by the server). */
export interface DepositInfoDto { bankAccountId: number | null; bank?: string | null; slipRefNo: string; depositDate: string; }

export const BANK_CODES = ['BDO', 'BPI', 'MBTC', 'PBCOM', 'CBC', 'SBC', 'LBP'];

export interface BankAccountDto {
  id: number; bankCode: string; accountNo: string; accountName: string; label: string; isActive: boolean;
  siteIds: number[]; asOfDate: string | null; lastUploadAt: string | null;
  /** Running balance after the latest statement line (null if the bank's export has none). */
  balance: number | null; lineCount: number; unmatchedCount: number;
  /** HOMS cash-in-bank GL account (BANKS4W); synced = maintained from BANKS4W (name / GL read-only; the number can be corrected). */
  homsAcctNo: number | null; bankAbbr: string | null; synced: boolean;
  /** BANKS4W's number (what HOMS writes in DEPBANK); differs from accountNo when it was corrected in HOMSys. */
  homsBankAc: string | null;
  /** No deposits and no statement uploads: it can be deleted. */
  canDelete: boolean;
}
/** Import preview: read from the file, nothing saved. Opening/Closing from the bank's running balances. */
export interface BankUploadPreviewDto {
  accountLabel: string; fileName: string; fromDate: string; toDate: string; lineCount: number; newLineCount: number;
  creditCount: number; credits: number; debitCount: number; debits: number;
  hasBalance: boolean; balancesAddUp: boolean; order: string | null; opening: number | null; closing: number | null; currentBalance: number | null;
}
export interface BankAccountOptionDto { id: number; label: string; accountName: string; }
export interface BankAsOfDto { asOfDate: string | null; lastUploadAt: string | null; accounts: BankAccountDto[]; }
export interface BankStatementUploadDto {
  id: number; bankAccountId: number; accountLabel: string; fileName: string; asOfDate: string | null;
  lineCount: number; newLineCount: number; uploadedAt: string; uploadedBy: string;
}
export interface BankStatementLineDto {
  id: number; bankAccountId: number; accountLabel: string; txnDate: string; valueDate: string | null; txnTime: string | null;
  description: string; reference: string | null; bankBranch: string | null; checkNo: string | null;
  debit: number; credit: number; balance: number | null;
  matchedDepositBatchId: number | null; matchedSlip: string | null; matchedSiteCode: string | null;
  matchMethod: string | null; matchedAt: string | null; matchedBy: string | null;
  /** false on a matched line = automatic match awaiting the Finance Verifier. */
  matchConfirmed: boolean;
}
export interface DepositCandidateDto {
  id: number; siteCode: string; bank: string; slipRefNo: string; depositDate: string; totalAmount: number;
  collectionCount: number; amountMatches: boolean;
}

export interface SaveCollectionDto {
  siteId: number;
  receiptType: ReceiptType;
  receiptNo: string;
  receiptDate: string;
  custKey: string;
  paymentMode: PaymentMode;
  amount: number;
  bank: string | null;
  checkNo: string | null;
  checkDate: string | null;
  deposit: DepositInfoDto | null;
  remarks: string | null;
  invoices: { postedInvoiceId: number; amountApplied: number }[];
  deductions: { postedInvoiceId: number; type: DeductionType; amount: number; reference: string | null }[];
  /** Chain account paying several customers' invoices on this one receipt (e.g. SM). */
  multiCustomer?: boolean;
}

export interface CollectionListItemDto {
  id: number; siteId: number; siteCode: string; receiptType: ReceiptType; receiptNo: string; receiptDate: string;
  custKey: string; custName: string; paymentMode: PaymentMode; amount: number; unappliedAmount: number; invoiceCount: number;
  paymentStatus: string; verificationStatus: string; pdcState: string | null; checkDate: string | null;
  depositSlipRef: string | null; createdBy: string; createdAt: string; submittedAt: string | null; postedInBms: boolean;
  /** Found = matched to a bank credit, awaiting the verifier; Approved = verifier approved it. */
  bankMatch: 'Found' | 'Approved' | null;
  /** Writing into the HOMS ARCHIVES files. */
  homs: 'Queued' | 'Posted' | 'Held' | 'Failed' | null;
  /** Part of a Remittance Advice: submitted / verified / returned with it. */
  remittanceId: number | null;
}

/** The BMS posted OR (OH03/OD03 mirror) a collection was found in. */
export interface BmsPostingDto {
  docNo: number; docDate: string | null; posted: string | null; received: number; ewt: number; arApplied: number;
  bounced: boolean; linkMethod: string;
}

export interface CollectionInvoiceDto {
  postedInvoiceId: number; docNo: number; docDate: string; custKey: string; custName: string;
  total: number; rfcTotal: number; amountApplied: number; balance: number; availableBalance: number;
}

export interface CollectionDto {
  id: number; collectionKey: string; siteId: number; siteCode: string; siteName: string;
  receiptType: ReceiptType; receiptNo: string; receiptDate: string; custKey: string; custName: string;
  paymentMode: PaymentMode; amount: number; unappliedAmount: number;
  bank: string | null; checkNo: string | null; checkDate: string | null; deposit: DepositInfoDto | null; depositMatched: boolean;
  paymentStatus: string; verificationStatus: string; pdcState: string | null; pdcOutcome: string | null;
  pdcOutcomeDate: string | null; pdcOutcomeReason: string | null; pdcOutcomeBy: string | null;
  returnReason: string | null; voidReason: string | null; remarks: string | null;
  createdBy: string; createdAt: string; submittedAt: string | null; verifiedAt: string | null; verifiedBy: string | null;
  canEdit: boolean; canSubmit: boolean; canVoid: boolean; canRecordDeposit: boolean; canVerify: boolean;
  invoices: CollectionInvoiceDto[];
  deductions: { id: number; postedInvoiceId: number; docNo: number; type: DeductionType; amount: number; reference: string | null }[];
  attachments: { id: number; fileName: string; contentType: string; size: number; uploadedAt: string; uploadedBy: string }[];
  history: CollectionHistoryDto[];
  bms: BmsPostingDto | null;
  multiCustomer: boolean;
  /** The bank credit the slip is matched to; confirmed = approved by a verifier (depositMatched is true only then). */
  bankLine: MatchedLineDto | null;
  canConfirmMatch: boolean;
  /** What a draft still lacks before it can be submitted (empty = complete). */
  missing: string[];
  /** Documents written (or queued) into the HOMS ARCHIVES files: OR (CR), RPDC (PDC), JV (matured PDC). */
  homsPostings: HomsPostingDto[];
  canRetryHoms: boolean;
  /** RA CR: its Remittance Advice, HOMS desk (32 Luzon / 31 VisMin) and typed DCR no. */
  remittanceId: number | null; homsDeptCode: number | null; dcrNo: number | null;
}

// ── Remittance Advice (NKA chains) ──────────────────────────────────────────

export interface SaveRemittanceDto {
  chainName: string | null; paymentMode: 'Check' | 'BankTransfer'; bank: string | null; checkNo: string | null; checkDate: string | null;
  checkAmount: number; deposit: DepositInfoDto | null; remarks: string | null;
}

export interface RemittanceListItemDto {
  id: number; chainName: string; paymentMode: PaymentMode; checkNo: string | null; checkDate: string | null; checkAmount: number;
  crCount: number; crTotal: number; receiptNos: string; openFlags: number; verificationStatus: string;
  depositSlipRef: string | null; bankMatch: 'Found' | 'Approved' | null; homs: 'Queued' | 'Posted' | 'Held' | 'Failed' | null;
  createdBy: string; createdAt: string; submittedAt: string | null;
}

/** Every flag means the line doesn't match its invoice and blocks submit: correct the RA Excel and upload it again.
 *  RfcDiff (the chain's RFC/BO/RTV vs RFCs posted in BMS) also clears once BMS posts the RFC — HOMSys re-reads it from HO. */
export type RemittanceFlag = 'Fetching' | 'NotFound' | 'GrossDiff' | 'VatExcluded' | 'CustDiff' | 'DateDiff' | 'RfcDiff' | 'OverBalance' | 'NetMismatch' | 'Duplicate';
export const FLAG_LABELS: Record<RemittanceFlag, string> = {
  Fetching: 'Fetching from HO', NotFound: 'Not found in BMS', GrossDiff: 'Gross differs', VatExcluded: 'VAT excluded?', CustDiff: 'Customer differs',
  DateDiff: 'Date differs', RfcDiff: 'RFC differs', OverBalance: 'Over balance', NetMismatch: 'Net ≠ gross − deductions', Duplicate: 'Listed twice',
};

export interface RemittanceLineDto {
  id: number; rowNo: number; docNo: number; whseNo: number; siteCode: string | null; custKey: string; docDate: string | null; birSeries: string | null;
  gross: number; cewt: number; rfcBoRtv: number; other: number; net: number;
  deductions: { type: DeductionType; amount: number; remark: string | null }[];
  postedInvoiceId: number | null; systemTotal: number | null; systemRfc: number | null;
  systemCustKey: string | null; systemCustName: string | null; systemDocDate: string | null;
  flags: RemittanceFlag[]; applied: boolean;
  /** What this CR may still apply: total + posted RFCs − other live receipts; appliedElsewhere names those receipts. */
  openBalance: number | null; appliedElsewhere: string | null;
  /** RFCs posted in BMS against the invoice (rfcNo 0 = number not synced yet). */
  rfcs: { rfcNo: number; rfcDate: string | null; amount: number }[] | null;
}

export interface CollectionAttachmentDto { id: number; fileName: string; contentType: string; size: number; uploadedAt: string; uploadedBy: string; }

export interface RemittanceCrDto {
  id: number; receiptNo: string; dcrNo: number | null; receiptDate: string; desk: number | null; deskLabel: string | null; amount: number;
  applied: number; deductions: number; unapplied: number; verificationStatus: string; paymentStatus: string;
  excelFile: CollectionAttachmentDto | null; attachments: CollectionAttachmentDto[]; lines: RemittanceLineDto[];
  missing: string[]; homsPostings: HomsPostingDto[];
}

export interface RemittanceDto {
  id: number; siteId: number; siteCode: string; chainName: string; paymentMode: 'Check' | 'BankTransfer';
  bank: string | null; checkNo: string | null; checkDate: string | null; checkAmount: number;
  deposit: DepositInfoDto | null; bankLine: MatchedLineDto | null; verificationStatus: string;
  returnReason: string | null; voidReason: string | null; remarks: string | null;
  createdBy: string; createdAt: string; submittedAt: string | null; verifiedAt: string | null; verifiedBy: string | null;
  crTotal: number; crs: RemittanceCrDto[]; history: CollectionHistoryDto[]; missing: string[];
  canEdit: boolean; canSubmit: boolean; canVerify: boolean; canVoid: boolean;
}

export interface HomsPostingDto {
  id: number; kind: 'OR' | 'RPDC' | 'JV'; status: 'Pending' | 'Processing' | 'Done' | 'Held' | 'Failed' | 'Cancelled';
  docNo: number | null; file: string | null; message: string | null; createdAt: string; completedAt: string | null;
}

export interface MatchedLineDto {
  id: number; account: string; txnDate: string; credit: number; reference: string | null; description: string;
  method: string | null; confirmed: boolean; confirmedBy: string | null;
}

export interface InvoiceSyncRequestStatusDto {
  id: number; siteId: number; status: 'Pending' | 'Processing' | 'Done' | 'Failed';
  requestedAt: string; completedAt: string | null; message: string | null;
  items: { invoiceNo: number; invoiceDate: string; found: boolean | null }[];
}

/** PDC module row. state: Immature | Matured | Deposited | Succeeded | Bounced. */
export interface PdcItemDto {
  id: number; siteId: number; siteCode: string; receiptType: ReceiptType; receiptNo: string; receiptDate: string;
  custKey: string; custName: string; bank: string | null; checkNo: string | null; checkDate: string;
  daysToMaturity: number; amount: number; state: string; nearMaturity: boolean; verificationStatus: string;
  depositBank: string | null; depositSlipRef: string | null; depositDate: string | null;
  outcome: string | null; outcomeDate: string | null; outcomeReason: string | null; outcomeBy: string | null; canTag: boolean;
  bmsDocNo: number | null; bmsBounced: boolean;
  /** The bank credit the deposit is matched to; confirmed = the CNC Verifier confirmed it (JV posted). */
  bankLine: MatchedLineDto | null; depositBy: string | null;
  canConfirmMatch: boolean; canRecordDeposit: boolean;
  /** Bounced PDCs only: Letter of Dishonor -> settled by a new CR, or endorsed for legal action. */
  dishonor: PdcDishonorDto | null;
}

/** One posted invoice's payment history; counts = false for voided receipts and bounced PDCs. */
export interface InvoiceHistoryDto {
  id: number; docNo: number; docDate: string; siteCode: string; custKey: string; custName: string; poNum: string | null; term: number;
  total: number; rfcTotal: number; rfcs: number[]; payments: InvoicePaymentDto[]; paid: number; openBalance: number;
}
export interface InvoicePaymentDto {
  collectionId: number; siteCode: string; receiptType: string; receiptNo: string; receiptDate: string; paymentMode: string;
  checkNo: string | null; checkDate: string | null; verificationStatus: string; pdcState: string | null; createdBy: string;
  applied: number; deductions: number; deductionLines: { type: string; amount: number; reference: string | null }[];
  counts: boolean; notCountedReason: string | null;
  /** Deposited = matched to the bank statement. */
  paymentStatus: string;
}

export interface CollectionHistoryDto {
  action: string; fromStatus: string | null; toStatus: string | null; reason: string | null; at: string; by: string;
}

export type DishonorState = 'For LOD' | 'LOD Issued' | 'Awaiting Settlement' | 'Overdue' | 'For Legal Action' | 'Settled';

export interface PdcDishonorDto {
  state: DishonorState; canFollowUp: boolean;
  lodIssuedDate: string | null; lodIssuedBy: string | null; lodSettleDays: number | null;
  lodReceivedDate: string | null; lodCopyAttachmentId: number | null; lodDueDate: string | null;
  settledByCollectionId: number | null; settledByReceipt: string | null; settledReceiptDate: string | null; settledBy: string | null;
  legalEndorsedDate: string | null; legalEndorsedBy: string | null; legalRemarks: string | null;
  /** In-house legal | External counsel. */
  legalEndorsedTo: string | null;
}

export interface SettlementCandidateDto {
  id: number; siteCode: string; receiptType: string; receiptNo: string; receiptDate: string; paymentMode: string; amount: number;
}

export interface LodLetterDto {
  companyName: string; companyAddress: string; companyPhone: string; siteName: string; siteAddress: string;
  custKey: string; custName: string; custAddress1: string | null; custAddress2: string | null;
  receiptType: string; receiptNo: string; bank: string | null; checkNo: string | null; checkDate: string; amount: number;
  depositDate: string | null; bouncedDate: string; bounceReason: string | null;
  issuedDate: string; issuedBy: string; settleDays: number;
}

export const RECEIPT_TYPES: { label: string; value: ReceiptType }[] = [
  { label: 'CR — Collection Receipt', value: 'CR' },
  { label: 'PR — Provisional Receipt (PDC)', value: 'PR' },
  { label: 'AR — Acknowledgement Receipt (PDC)', value: 'AR' },
];

export const PAYMENT_MODES: { label: string; value: PaymentMode }[] = [
  { label: 'Bank transfer / direct deposit', value: 'BankTransfer' },
  { label: 'Cheque', value: 'Check' },
  { label: 'Post-dated check', value: 'PostDatedCheck' },
];

export const DEDUCTION_TYPES: { label: string; value: DeductionType }[] = [
  { label: 'EWT (2307)', value: 'Ewt2307' },
  { label: 'Discount', value: 'Discount' },
  { label: 'Penalty', value: 'Penalty' },
  { label: 'Other', value: 'Other' },
  { label: 'RFC/BO/RTV', value: 'RfcBoRtv' },
];

/** Tag colour per status (verification, payment or PDC state). */
export function statusSeverity(s: string | null | undefined): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
  switch (s) {
    case 'Verified': case 'Deposited': case 'Succeeded': case 'Settled': return 'success';
    case 'ForVerification': case 'Matured': case 'Pending': case 'Collected': case 'LOD Issued': case 'Awaiting Settlement': return 'info';
    case 'Returned': case 'Immature': case 'For LOD': return 'warn';
    case 'Voided': case 'Bounced': case 'Overdue': case 'For Legal Action': return 'danger';
    default: return 'secondary';
  }
}

/** 'ForVerification' -> 'For Verification'. */
export function statusLabel(s: string | null | undefined): string {
  return (s ?? '').replace(/([a-z])([A-Z])/g, '$1 $2');
}

/** Local calendar day as yyyy-MM-dd (what the API's DateOnly fields hold). */
export function isoDay(d: Date | null | undefined): string | null {
  return d ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` : null;
}

/** Date filter for the collection pages: value (yyyy-MM-dd…) within [from, to]; an open end is unbounded. */
export function inDateRange(value: string | null | undefined, from: string | null, to: string | null): boolean {
  if (!from && !to) return true;
  const d = (value ?? '').slice(0, 10);
  return !!d && (!from || d >= from) && (!to || d <= to);
}