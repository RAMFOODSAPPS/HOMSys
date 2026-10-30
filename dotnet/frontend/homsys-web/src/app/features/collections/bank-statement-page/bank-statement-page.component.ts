import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { AuthService } from '../../../core/services/auth.service';
import { BankStatementService } from '../../../core/services/bank-statement.service';
import { CollectionService } from '../../../core/services/collection.service';
import {
  BANK_CODES, BankAccountDto, BankStatementLineDto, BankUploadPreviewDto, CollectionSiteDto, DepositCandidateDto
} from '../../../core/models/collection.model';

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Account', field: 'accountLabel' },
  { header: 'Date', field: 'txnDate' },
  { header: 'Value Date', field: 'valueDate' },
  { header: 'Description', field: 'description' },
  { header: 'Reference', field: 'reference' },
  { header: 'Bank Branch', field: 'bankBranch' },
  { header: 'Check No.', field: 'checkNo' },
  { header: 'Debit', field: 'debit' },
  { header: 'Credit', field: 'credit' },
  { header: 'Matched Slip', field: 'matchedSlip' },
  { header: 'Branch', field: 'matchedSiteCode' },
  { header: 'Match', field: 'matchMethod' },
];

type LineTab = 'all' | 'unmatched' | 'credits' | 'debits';
type SortKey = 'date' | 'debit' | 'credit';

/**
 * Finance Manager: uploads each account's daily statement (bank portal export), sees "as of" per account,
 * and settles what the automatic match left open. Verifiers get the same view read-only.
 */
@Component({
  selector: 'app-bank-statement-page',
  standalone: true,
  providers: [MessageService],
  imports: [SelectModule, MultiSelectModule, ButtonModule, DialogModule, InputTextModule, CheckboxModule,
    ToastModule, TooltipModule, FormsModule, DatePipe, DecimalPipe],
  template: `
    <p-toast position="top-right" />
    <div class="rv fit">
      <main class="rv-main">
        <section class="acct-sec">
          <div class="acct-head">
            <span class="sec-title">Accounts</span>
            <div class="acct-head-r">
              @if (companyName()) { <span class="muted">{{ companyName() }}</span> }
              @if (canManage) { <button type="button" class="btn-sm" (click)="openAccounts()">Bank accounts</button> }
              <button type="button" class="btn-sm" (click)="showBalances.set(!showBalances())">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z"></path><circle cx="12" cy="12" r="3"></circle></svg>
                {{ showBalances() ? 'Hide balances' : 'Show balances' }}</button>
            </div>
          </div>
          <div class="acct-grid">
            @for (a of shownAccounts(); track a.id) {
              <button type="button" class="acct" [class.acct-on]="accountId() === a.id" (click)="selectAccount(accountId() === a.id ? null : a.id)">
                <div class="acct-top">
                  <span class="acct-bank">{{ a.bankCode }}</span>
                  <span class="acct-no mono">{{ a.accountNo }}</span>
                  <span class="badge" [class.none]="!a.lineCount" [class.warn]="a.lineCount && a.unmatchedCount" [class.ok]="a.lineCount && !a.unmatchedCount">
                    {{ !a.lineCount ? 'No lines' : a.unmatchedCount ? a.unmatchedCount + ' unmatched' : 'All matched' }}</span>
                </div>
                <span class="acct-bal mono" [pTooltip]="a.balance === null ? 'No running balance in this bank export' : 'Balance'">{{ a.balance === null ? '—' : showBalances() ? '₱ ' + (a.balance | number:'1.2-2') : '₱ ••••••••' }}</span>
                <span class="acct-meta"><span [class.stale]="isStale(a)">Stmt {{ (a.asOfDate | date:'MM/dd/yyyy') || 'not uploaded yet' }}</span>@if (a.lastUploadAt) { · Imported {{ a.lastUploadAt | date:'MM/dd hh:mm a' }} }</span>
              </button>
            } @empty { <div class="muted">No bank accounts yet.</div> }
          </div>
        </section>

        <section class="card lines-card">
          <div class="lines-bar">
            <div class="lines-filters">
              <select class="sel" [ngModel]="accountId()" (ngModelChange)="selectAccount($event)">
                <option [ngValue]="null">All accounts</option>
                @for (a of shownAccounts(); track a.id) { <option [ngValue]="a.id">{{ a.bankCode }} {{ a.accountNo }}</option> }
              </select>
              <div class="dates" title="Transaction date">
                <input type="date" class="sel" [ngModel]="from()" (ngModelChange)="from.set($event || null); load()" aria-label="Date from" />
                <span class="muted">to</span>
                <input type="date" class="sel" [ngModel]="to()" (ngModelChange)="to.set($event || null); load()" aria-label="Date to" />
              </div>
              <div class="seg">
                @for (t of lineTabs; track t.value) {
                  <button type="button" [class.on]="tab() === t.value" (click)="setTab(t.value)">{{ t.label }}</button>
                }
              </div>
            </div>
            <div class="search">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7"></circle><path d="m20 20-3.5-3.5"></path></svg>
              <input [ngModel]="q()" (ngModelChange)="q.set($event); page.set(0)" placeholder="Search description or reference" />
            </div>
          </div>

          <div class="lines-scroll">
            <table class="tbl lines">
              <thead>
                <tr>
                  <th class="sort" (click)="sortBy('date')">Date {{ arrow('date') }}</th>
                  <th>Account</th><th class="w-desc">Description</th><th class="w-ref">Reference</th><th>Bank branch</th>
                  <th class="num sort" (click)="sortBy('debit')">Debit {{ arrow('debit') }}</th>
                  <th class="num sort" (click)="sortBy('credit')">Credit {{ arrow('credit') }}</th>
                  <th>Deposit slip</th>
                </tr>
              </thead>
              <tbody>
                @for (l of pageRows(); track l.id) {
                  <tr>
                    <td class="nowrap"><div class="mono">{{ l.txnDate | date:'MM/dd/yyyy' }} <span class="muted">{{ l.txnTime }}</span></div>
                      @if (l.valueDate && l.valueDate !== l.txnDate) { <div class="sub">Value {{ l.valueDate | date:'MM/dd' }}</div> }</td>
                    <td class="nowrap"><div class="strong">{{ acctOf(l)?.bankCode }}</div><div class="sub mono">{{ acctOf(l)?.accountNo }}</div></td>
                    <td>{{ l.description }}</td>
                    <td class="ref mono">{{ l.reference }}@if (l.checkNo) { · check {{ l.checkNo }} }</td>
                    <td class="dim">{{ l.bankBranch || '—' }}</td>
                    <td class="num mono nowrap">{{ l.debit ? (l.debit | number:'1.2-2') : '' }}</td>
                    <td class="num mono nowrap">{{ l.credit ? (l.credit | number:'1.2-2') : '' }}</td>
                    <td class="nowrap"><div class="slip">
                      @if (l.credit > 0) {
                        @if (l.matchedSlip) {
                          @if (l.matchConfirmed) {
                            <span class="pill pill-success">{{ l.matchedSiteCode }} · {{ l.matchedSlip }}</span>
                          } @else {
                            <span class="pill pill-info" pTooltip="Automatic match — counts once the Finance Verifier approves it">Pending · {{ l.matchedSiteCode }} · {{ l.matchedSlip }}</span>
                          }
                          @if (canManage) { <a href="" (click)="$event.preventDefault(); unmatch(l)">Unmatch</a> }
                        } @else {
                          <span class="pill pill-warn">{{ l.matchMethod === 'Unmatched' ? 'Unmatched by finance' : 'Unmatched' }}</span>
                          @if (canManage) { <a href="" (click)="$event.preventDefault(); openMatch(l)">Match</a> }
                        }
                      } @else { <span class="nil">—</span> }
                    </div></td>
                  </tr>
                }
              </tbody>
            </table>
            @if (!loading() && !sorted().length) { <div class="empty">No lines match these filters.</div> }
            @if (loading()) { <div class="empty">Loading…</div> }
          </div>

          <div class="pager">
            <span>Showing {{ sorted().length ? (page() * pageSize() + 1) + '–' + min((page() + 1) * pageSize(), sorted().length) + ' of ' + sorted().length : '0' }}</span>
            <div class="pager-r">
              <button type="button" [disabled]="page() === 0" (click)="page.set(page() - 1)">‹</button>
              <button type="button" class="on">{{ page() + 1 }}</button>
              <button type="button" [disabled]="page() >= pageCount() - 1" (click)="page.set(page() + 1)">›</button>
              <select class="sel sm" [ngModel]="pageSize()" (ngModelChange)="pageSize.set(+$event); page.set(0)">
                <option [ngValue]="50">50 / page</option><option [ngValue]="100">100 / page</option><option [ngValue]="200">200 / page</option>
              </select>
            </div>
          </div>
        </section>
      </main>
    </div>

    <p-dialog [header]="preview() ? 'Confirm statement' : 'Import statement'" [(visible)]="importVisible" [modal]="true" [style]="{ width: '480px' }">
      @if (preview(); as p) {
        <div class="pv">
          <div class="pv-row"><span>Account</span><b>{{ p.accountLabel }}</b></div>
          <div class="pv-row"><span>File</span><span>{{ p.fileName }}</span></div>
          <div class="pv-row"><span>Statement date</span><b>{{ p.fromDate | date:'MM/dd/yyyy' }}@if (p.toDate !== p.fromDate) { – {{ p.toDate | date:'MM/dd/yyyy' }} }</b></div>
          <div class="pv-row"><span>Lines</span><span>{{ p.lineCount }} · <b>{{ p.newLineCount }} new</b>@if (p.lineCount > p.newLineCount) { <span class="muted">({{ p.lineCount - p.newLineCount }} already imported)</span> }</span></div>
          <div class="pv-row"><span>Credits</span><span class="mono">{{ p.credits | number:'1.2-2' }} <span class="muted">({{ p.creditCount }})</span></span></div>
          <div class="pv-row"><span>Debits</span><span class="mono">{{ p.debits | number:'1.2-2' }} <span class="muted">({{ p.debitCount }})</span></span></div>
          @if (!p.newLineCount) { <div class="pv-warn">Nothing new: every line of this file is already imported.</div> }
          @if (p.hasBalance) {
            <div class="pv-row"><span>Opening balance</span><span class="mono">{{ p.opening === null ? '—' : (p.opening | number:'1.2-2') }}</span></div>
            <div class="pv-row pv-total"><span>Closing balance</span><span class="mono">₱ {{ p.closing | number:'1.2-2' }}</span></div>
            <div class="muted">From the bank's running balances@if (p.order) {, listed {{ p.order }}}.
              @if (p.currentBalance !== null) { Balance in HOMSys now: {{ p.currentBalance | number:'1.2-2' }}. }</div>
            @if (!p.balancesAddUp) { <div class="pv-warn">The running balances in this file don't add up line by line — check the file against the portal before importing.</div> }
          } @else {
            <div class="pv-warn">This bank's export has no running balance — check the credit and debit totals against the portal instead.</div>
          }
          <label class="pv-confirm">
            <p-checkbox [(ngModel)]="balanceConfirmed" [binary]="true" />
            <span>@if (p.hasBalance) { I checked the bank portal: the closing balance <b class="mono">₱ {{ p.closing | number:'1.2-2' }}</b> is correct. }
              @else { I checked the bank portal: these totals are correct. }</span>
          </label>
        </div>
      } @else {
        <p class="muted mt-0">The portal's export as downloaded (.xls / .xlsx / .csv). Re-uploading the same day is fine — only lines not imported yet come in.
          You'll confirm the balance before anything is imported.</p>
        <p-select [options]="importAccounts()" [(ngModel)]="uploadAccountId" optionLabel="label" optionValue="id" placeholder="Account" styleClass="w-full" appendTo="body" />
      }
      <input #file type="file" accept=".xls,.xlsx,.csv" hidden (change)="choose(file)" />
      <ng-template pTemplate="footer">
        @if (preview()) {
          <p-button label="Back" [text]="true" (onClick)="resetImport()" />
          <p-button [label]="preview()!.newLineCount ? 'Import ' + preview()!.newLineCount + ' new' : 'Import'" icon="pi pi-check"
            [disabled]="!balanceConfirmed || !preview()!.newLineCount" [loading]="uploading()" (onClick)="upload()" />
        } @else {
          <p-button label="Cancel" [text]="true" (onClick)="importVisible = false" />
          <p-button label="Choose file" icon="pi pi-upload" [disabled]="!uploadAccountId" [loading]="uploading()" (onClick)="file.click()" />
        }
      </ng-template>
    </p-dialog>

    <p-dialog header="Match bank credit" [(visible)]="matchVisible" [modal]="true" [style]="{ width: '640px' }">
      @if (matching(); as l) {
        <div class="mb-2">{{ l.accountLabel }} · {{ l.txnDate | date:'MM/dd/yyyy' }} · <b>{{ l.credit | number:'1.2-2' }}</b> · {{ l.description }}</div>
        <table class="bs-mini w-full">
          <tr><th>Branch</th><th>Slip</th><th>Deposit date</th><th class="num">Slip total</th><th>Receipts</th><th></th></tr>
          @for (c of candidates(); track c.id) {
            <tr [class.muted]="!c.amountMatches">
              <td>{{ c.siteCode }}</td><td>{{ c.slipRefNo || '—' }}</td><td>{{ c.depositDate | date:'MM/dd/yyyy' }}</td>
              <td class="num">{{ c.totalAmount | number:'1.2-2' }}</td><td>{{ c.collectionCount }}</td>
              <td>@if (c.amountMatches) { <p-button label="Match" size="small" [loading]="busy()" (onClick)="match(l, c)" /> } @else { amount differs }</td>
            </tr>
          } @empty { <tr><td colspan="6" class="muted">No open deposit slip on this account around that date.</td></tr> }
        </table>
      }
    </p-dialog>

    <p-dialog header="Bank accounts" [(visible)]="accountsVisible" [modal]="true" [style]="{ width: '820px' }">
      <div class="bs-acct-top">
        <p class="muted my-0">Company bank accounts. Add one, edit it, choose the branches that deposit into it, or delete an account that has no deposits or statements.</p>
        <p-button label="Add account" icon="pi pi-plus" size="small" [outlined]="true" (onClick)="newAccount()" />
      </div>
      <div class="bs-acct-list mb-3">
        <table class="bs-mini w-full">
          <tr><th>GL</th><th>Account</th><th>Name</th><th>Branches</th><th></th></tr>
          @for (a of activeAccounts(); track a.id) {
            <tr [class.font-bold]="acct.id === a.id"><td>{{ a.homsAcctNo ?? '—' }}</td><td>{{ a.label }}</td><td>{{ a.accountName }}</td>
              <td>{{ siteCodes(a.siteIds) || 'all branches' }}</td><td><p-button icon="pi pi-pencil" [text]="true" size="small" (onClick)="editAccount(a)" /></td></tr>
          }
        </table>
      </div>
      @if (acct.id || adding()) {
        <div class="font-bold mb-2">{{ acct.id ? 'Edit ' + editing()?.label : 'New bank account' }}</div>
        <div class="bs-form">
          <label class="bs-f"><span>Bank code</span>
            <input pInputText [(ngModel)]="acct.bankCode" maxlength="10" list="bs-bank-codes" placeholder="BDO, BPI, MBTC, PBCOM, CBC, SBC, LBP…" /></label>
          <label class="bs-f"><span>Account no.</span>
            <input pInputText class="mono" [(ngModel)]="acct.accountNo" maxlength="30" placeholder="As printed on the statement" /></label>
          <label class="bs-f"><span>Account name</span>
            <input pInputText [(ngModel)]="acct.accountName" maxlength="100" placeholder="e.g. BDO UNIBANK, INC." /></label>
          <label class="bs-f"><span>HOMS GL account (cash in bank)</span>
            <input pInputText class="mono" type="number" [(ngModel)]="acct.homsAcctNo" placeholder="e.g. 10212" /></label>
          <label class="bs-f"><span>HOMS bank abbreviation (optional)</span>
            <input pInputText [(ngModel)]="acct.bankAbbr" maxlength="20" placeholder="Same as the bank code" /></label>
          <label class="bs-f"><span>Branches that deposit into it</span>
            <p-multiselect [options]="sites()" [(ngModel)]="acct.siteIds" optionLabel="code" optionValue="id" placeholder="Branches (none = all)" appendTo="body" display="chip" /></label>
        </div>
        <datalist id="bs-bank-codes">@for (c of bankCodes; track c) { <option [value]="c"></option> }</datalist>
        @if (acct.bankCode.trim() && !isLayoutBank(acct.bankCode)) { <div class="muted mt-1">Statements can be imported only for {{ bankCodes.join(', ') }}; other banks can still take deposits.</div> }
        @if (editing()?.homsBankAc; as homs) {
          @if (homs !== acct.accountNo.trim()) { <div class="muted mt-1">HOMS (BANKS4W) has <span class="mono">{{ homs }}</span>, and keeps writing it in its records; HOMSys uses the number above for statements and slips.</div> }
        }
      }
      <ng-template pTemplate="footer">
        @if (acct.id) {
          @if (deleting()) {
            <span class="muted mr-2">Delete {{ editing()?.label }}?</span>
            <p-button label="Cancel" [text]="true" (onClick)="deleting.set(false)" />
            <p-button label="Yes, delete" severity="danger" [loading]="busy()" (onClick)="deleteAccount()" />
          } @else {
            <span [pTooltip]="editing()?.canDelete ? undefined : 'It has deposits or statement uploads, so it cannot be deleted.'" tooltipPosition="top">
              <p-button label="Delete account" severity="danger" [text]="true" [disabled]="!editing()?.canDelete" (onClick)="deleting.set(true)" />
            </span>
            <p-button label="Save" [loading]="busy()" (onClick)="saveAccount()" />
          }
        } @else if (adding()) {
          <p-button label="Cancel" [text]="true" (onClick)="editAccount(null)" />
          <p-button label="Add account" [loading]="busy()" (onClick)="saveAccount()" />
        }
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    /* Page styles live in styles.scss under .rv (Bank Statement design); only the dialogs' small tables stay here. */
    :host { display: block; height: 100%; }
    .bs-mini { border-collapse: collapse; font-size: .82rem; }
    .bs-mini th, .bs-mini td { padding: .2rem .5rem; text-align: left; border-bottom: 1px solid var(--p-surface-border); }
    .bs-mini .num, .num { text-align: right; }
    .bs-form { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: .5rem; align-items: end; }
    .bs-acct-top { display: flex; justify-content: space-between; align-items: center; gap: 1rem; margin-bottom: .6rem; }
    .bs-acct-top p-button { flex-shrink: 0; white-space: nowrap; }
    .bs-f { display: flex; flex-direction: column; gap: .25rem; min-width: 0; font-size: .8rem; color: var(--p-text-muted-color); }
    .bs-acct-list { max-height: 50vh; overflow-y: auto; }
    /* Import confirmation step */
    .pv { display: flex; flex-direction: column; gap: .45rem; font-size: .88rem; }
    .pv-row { display: flex; justify-content: space-between; gap: 1rem; }
    .pv-row > span:first-child { color: var(--p-text-muted-color); }
    .pv-total { border-top: 1px solid var(--p-surface-border); padding-top: .5rem; margin-top: .15rem; font-weight: 700; font-size: 1rem; }
    .pv-warn { background: #fffbeb; color: #92400e; padding: .5rem .6rem; border-radius: 6px; font-size: .8rem; }
    .pv-confirm { display: flex; gap: .55rem; align-items: flex-start; margin-top: .5rem; padding: .6rem .7rem; border: 1px solid var(--p-surface-border); border-radius: 8px; cursor: pointer; }
    .mono { font-family: 'IBM Plex Mono', ui-monospace, Consolas, monospace; }
    .muted { color: var(--p-text-muted-color); font-size: .8rem; }
  `]
})
export class BankStatementPageComponent implements OnInit, OnDestroy {
  private api = inject(BankStatementService);
  private collections = inject(CollectionService);
  private toolbar = inject(GlobalToolbarService);
  private exportSvc = inject(ExportService);
  private toast = inject(MessageService);
  private auth = inject(AuthService);

  protected readonly canManage = this.auth.isAdmin() || this.auth.hasPermission('collections-manage');
  /** Statement import: the Finance Manager or the Finance Verifier. */
  protected readonly canImport = this.canManage || this.auth.hasPermission('collections-verify');
  protected readonly bankCodes = BANK_CODES;
  protected readonly lineTabs: { label: string; value: LineTab }[] = [
    { label: 'All lines', value: 'all' }, { label: 'Unmatched credits', value: 'unmatched' },
    { label: 'Credits', value: 'credits' }, { label: 'Debits', value: 'debits' },
  ];
  protected readonly min = Math.min;

  accounts = signal<BankAccountDto[]>([]);
  sites = signal<CollectionSiteDto[]>([]);
  lines = signal<BankStatementLineDto[]>([]);
  candidates = signal<DepositCandidateDto[]>([]);
  matching = signal<BankStatementLineDto | null>(null);
  accountId = signal<number | null>(null);
  // Opens on every line (user 2026-10-16: on "Unmatched" the debits looked missing — that tab is open credits only).
  tab = signal<LineTab>('all');
  q = signal('');
  /** Transaction-date range (yyyy-MM-dd), filtered by the API. */
  from = signal<string | null>(null);
  to = signal<string | null>(null);
  sortKey = signal<SortKey>('date');
  sortDir = signal<1 | -1>(-1);
  page = signal(0);
  pageSize = signal(50);
  showBalances = signal(false);
  loading = signal(true);
  uploading = signal(false);
  busy = signal(false);

  uploadAccountId: number | null = null;
  importVisible = false;
  /** Import step 2: the previewed file and the user's "balance is correct" tick. */
  preview = signal<BankUploadPreviewDto | null>(null);
  importFile: File | null = null;
  balanceConfirmed = false;
  matchVisible = false;
  accountsVisible = false;
  acct = this.blankAccount();
  /** The account open in the Bank accounts dialog (its HOMS number / delete rule) and the delete confirmation. */
  editing = signal<BankAccountDto | null>(null);
  deleting = signal(false);
  adding = signal(false);

  activeAccounts = computed(() => this.accounts().filter(a => a.isActive));
  /** Cards / filter: accounts that have statement lines (all 30 BANKS4W accounts would crowd the page). */
  shownAccounts = computed(() => this.activeAccounts().filter(a => a.lineCount > 0 || a.id === this.accountId()));
  /** Import: accounts whose bank has a statement layout. */
  importAccounts = computed(() => this.activeAccounts().filter(a => (BANK_CODES as readonly string[]).includes(a.bankCode)));
  private accountById = computed(() => new Map(this.accounts().map(a => [a.id, a])));
  asOf = computed(() => this.activeAccounts().map(a => a.asOfDate).filter(Boolean).sort().at(-1) ?? null);
  /** The account holder, when every account is in the same name. */
  companyName = computed(() => { const n = [...new Set(this.activeAccounts().map(a => a.accountName))]; return n.length === 1 ? n[0] : ''; });

  /** Tab (credits/debits client-side; unmatched is a server filter) + search, sorted. */
  sorted = computed(() => {
    const t = this.q().toLowerCase().trim(), tab = this.tab(), key = this.sortKey(), dir = this.sortDir();
    const rows = this.lines().filter(l => (tab !== 'credits' || l.credit > 0) && (tab !== 'debits' || l.debit > 0)
      && (!t || [l.description, l.reference ?? '', l.checkNo ?? '', l.bankBranch ?? '', l.matchedSlip ?? ''].some(x => x.toLowerCase().includes(t))));
    const val = (l: BankStatementLineDto) => key === 'debit' ? l.debit : key === 'credit' ? l.credit : l.txnDate + ' ' + (l.txnTime ?? '').padStart(8, '0') + ' ' + String(l.id).padStart(10, '0');
    return [...rows].sort((a, b) => { const x = val(a), y = val(b); return (x < y ? -1 : x > y ? 1 : 0) * dir; });
  });
  pageCount = computed(() => Math.max(1, Math.ceil(this.sorted().length / this.pageSize())));
  pageRows = computed(() => this.sorted().slice(this.page() * this.pageSize(), (this.page() + 1) * this.pageSize()));

  ngOnInit() {
    this.loadAccounts();
    this.collections.mySites().subscribe(res => this.sites.set(res.data ?? []));
    this.load();
    this.toolbar.set({
      title: 'Bank Statements',
      refresh: { onClick: () => { this.loadAccounts(); this.load(); }, loading: this.loading },
      ...(this.canImport ? { import: { label: 'Import statement', onClick: () => this.openImport(this.accountId()) } } : {}),
      export: {
        label: 'Export',
        pdf: () => this.exportSvc.exportPdf('Bank Statement Lines', EXPORT_COLS, this.rows()),
        excel: () => this.exportSvc.exportExcel('Bank Statement Lines', EXPORT_COLS, this.rows()),
        csv: () => this.exportSvc.exportCsv('Bank Statement Lines', EXPORT_COLS, this.rows()),
      },
    });
  }

  ngOnDestroy() { this.toolbar.clear(); }

  private loadAccounts() { this.api.accounts().subscribe(res => this.accounts.set(res.data ?? [])); }

  load() {
    this.loading.set(true);
    this.api.lines(this.accountId(), this.from(), this.to(), this.tab() === 'unmatched' ? 'unmatched' : 'all').subscribe({
      next: res => { this.lines.set(res.data ?? []); this.page.set(0); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  selectAccount(id: number | null) { this.accountId.set(id); this.load(); }

  setTab(t: LineTab) {
    const reload = (t === 'unmatched') !== (this.tab() === 'unmatched');
    this.tab.set(t);
    this.page.set(0);
    if (reload) this.load();
  }

  sortBy(k: SortKey) {
    this.sortDir.set(this.sortKey() === k ? (this.sortDir() === 1 ? -1 : 1) : -1);
    this.sortKey.set(k);
  }

  arrow(k: SortKey) { return this.sortKey() !== k ? '↕' : this.sortDir() < 0 ? '↓' : '↑'; }

  acctOf(l: BankStatementLineDto) { return this.accountById().get(l.bankAccountId); }

  openImport(accountId: number | null) { this.uploadAccountId = accountId; this.resetImport(); this.importVisible = true; }

  /** No statement for today or yesterday yet. */
  isStale(a: BankAccountDto) {
    if (!a.asOfDate) return true;
    return (Date.now() - new Date(a.asOfDate + 'T00:00:00').getTime()) / 86_400_000 > 2;
  }

  /** Step 1: read the chosen file (nothing saved) and show what it would import, for the user to confirm. */
  choose(input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = '';
    if (!file || !this.uploadAccountId) return;
    this.uploading.set(true);
    this.api.preview(this.uploadAccountId, file).subscribe({
      next: res => { this.uploading.set(false); this.importFile = file; this.balanceConfirmed = false; this.preview.set(res.data!); },
      error: err => this.fail(err, () => this.uploading.set(false))
    });
  }

  resetImport() { this.preview.set(null); this.importFile = null; this.balanceConfirmed = false; }

  /** Step 2: import the previewed file; the server checks the confirmed balance is the file's closing balance. */
  upload() {
    const p = this.preview(), file = this.importFile;
    if (!p || !file || !this.uploadAccountId || !this.balanceConfirmed) return;
    this.uploading.set(true);
    this.api.upload(this.uploadAccountId, file, p.closing).subscribe({
      next: res => {
        this.uploading.set(false);
        this.importVisible = false;
        this.resetImport();
        const d = res.data!;
        this.toast.add({ severity: 'success', summary: d.upload.accountLabel,
          detail: `${d.upload.lineCount} lines (${d.upload.newLineCount} new), as of ${d.upload.asOfDate}. ${d.matched} deposit slip(s) matched.` });
        this.loadAccounts();
        this.load();
      },
      error: err => this.fail(err, () => this.uploading.set(false))
    });
  }

  openMatch(l: BankStatementLineDto) {
    this.matching.set(l);
    this.candidates.set([]);
    this.matchVisible = true;
    this.api.candidates(l.id).subscribe(res => this.candidates.set(res.data ?? []));
  }

  match(l: BankStatementLineDto, c: DepositCandidateDto) {
    this.busy.set(true);
    this.api.match(l.id, c.id).subscribe({
      next: () => { this.busy.set(false); this.matchVisible = false; this.toast.add({ severity: 'success', summary: 'Matched', detail: `${c.siteCode}${c.slipRefNo ? ' slip ' + c.slipRefNo : ' deposit (no reference)'}` }); this.load(); },
      error: err => this.fail(err, () => this.busy.set(false))
    });
  }

  unmatch(l: BankStatementLineDto) {
    this.api.unmatch(l.id).subscribe({
      next: () => { this.toast.add({ severity: 'info', summary: 'Unmatched', detail: 'The slip\'s collections are back to Collected.' }); this.load(); },
      error: err => this.fail(err)
    });
  }

  // ── accounts ──────────────────────────────────────────────────────────

  private blankAccount() {
    return { id: null as number | null, bankCode: '', accountNo: '', accountName: '', isActive: true, siteIds: [] as number[],
             homsAcctNo: null as number | null, bankAbbr: '' };
  }

  newAccount() { this.editAccount(null); this.adding.set(true); }
  isLayoutBank(code: string) { return (BANK_CODES as readonly string[]).includes(code.trim().toUpperCase()); }

  openAccounts() { this.acct = this.blankAccount(); this.editing.set(null); this.deleting.set(false); this.accountsVisible = true; }

  editAccount(a: BankAccountDto | null) {
    this.acct = a ? { id: a.id, bankCode: a.bankCode, accountNo: a.accountNo, accountName: a.accountName, isActive: a.isActive, siteIds: [...a.siteIds],
                      homsAcctNo: a.homsAcctNo, bankAbbr: a.bankAbbr ?? '' }
      : this.blankAccount();
    this.adding.set(false);
    this.editing.set(a);
    this.deleting.set(false);
  }

  saveAccount() {
    const { id, ...dto } = this.acct;
    this.busy.set(true);
    this.api.saveAccount(id, { ...dto, bankCode: dto.bankCode.trim(), accountNo: dto.accountNo.trim(), accountName: dto.accountName.trim(),
                               homsAcctNo: dto.homsAcctNo ? +dto.homsAcctNo : null, bankAbbr: dto.bankAbbr.trim() }).subscribe({
      next: () => { this.busy.set(false); this.editAccount(null); this.loadAccounts(); this.toast.add({ severity: 'success', summary: 'Bank account', detail: 'Saved.' }); },
      error: err => this.fail(err, () => this.busy.set(false))
    });
  }

  deleteAccount() {
    const a = this.editing();
    if (!a) return;
    this.busy.set(true);
    this.api.deleteAccount(a.id).subscribe({
      next: () => { this.busy.set(false); this.editAccount(null); this.loadAccounts(); this.toast.add({ severity: 'success', summary: 'Bank account', detail: `${a.label} deleted.` }); },
      error: err => this.fail(err, () => { this.busy.set(false); this.deleting.set(false); })
    });
  }

  siteCodes(ids: number[]) { return ids.map(id => this.sites().find(s => s.id === id)?.code ?? id).join(', '); }

  private rows() { return this.sorted() as unknown as Record<string, unknown>[]; }

  private fail(err: { error?: { message?: string } }, done?: () => void) {
    done?.();
    this.toast.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Please try again.' });
  }
}
