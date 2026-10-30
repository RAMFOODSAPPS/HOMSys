import { Component, computed, HostListener, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { interval, Observable, Subscription } from 'rxjs';
import { filter, switchMap, take } from 'rxjs/operators';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { ApiResponse } from '../../../core/models/auth.model';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { AuthService } from '../../../core/services/auth.service';
import { CollectionService } from '../../../core/services/collection.service';
import { RemittanceService } from '../../../core/services/remittance.service';
import { BankStatementService } from '../../../core/services/bank-statement.service';
import { ExportService } from '../../../core/services/export.service';
import {
  BankAccountOptionDto, CollectionSiteDto, DEDUCTION_TYPES, FLAG_LABELS, HomsPostingDto, RemittanceCrDto, RemittanceDto,
  RemittanceFlag, RemittanceLineDto, RemittanceListItemDto, SaveRemittanceDto, statusLabel
} from '../../../core/models/collection.model';

const NEW_KEY = '/collections/remittance';

interface CrEdit { receiptNo: string; dcrNo: string; receiptDate: Date | null; }
type LineFilter = 'review' | 'ok' | 'all';

/** Status filter chips of the RA list (Returned / Voided only show up when there are some). */
const LIST_STATUSES: [string, string][] = [
  ['all', 'All'], ['Draft', 'Draft'], ['ForVerification', 'For verification'], ['Returned', 'Returned'], ['Verified', 'Verified'], ['Voided', 'Voided'],
];
/** Flags the uploader can't clear by correcting the Excel alone (red, like the design's "hard" lines). */
const HARD_FLAGS: RemittanceFlag[] = ['OverBalance', 'Duplicate', 'NotFound'];

/**
 * Remittance Advice (NKA chains): one payment, 1+ CRs, each filled from the chain's RA Excel and counter-checked
 * against the system invoices. The Custodian Verifier encodes and submits; the Finance Verifier verifies or returns it as a whole.
 * Layout: claude.ai/design "Remittance Advice" — RA list, payment / history column, collection receipts with their lines.
 */
@Component({
  selector: 'app-remittance-page',
  standalone: true,
  providers: [MessageService],
  imports: [FormsModule, DatePipe, DecimalPipe, InputTextModule, InputNumberModule, DatePickerModule, SelectModule,
    ToastModule, DialogModule, TextareaModule, TooltipModule],
  template: `
    <p-toast position="top-right" />
    <div class="ra">
        <aside class="ra-list">
          <div class="ra-list-top">
            <div class="ra-list-h">
              <span><b>Remittance Advices</b> <small>{{ visibleList().length }}</small></span>
              @if (canEncode) { <button type="button" class="ra-btn brand sm" (click)="newRa()">+ New</button> }
            </div>
            <input class="ra-in" [ngModel]="listQuery()" (ngModelChange)="listQuery.set($event)" placeholder="Search RA, chain, OR no." />
            <div class="ra-chipset">
              @for (s of listChips(); track s.v) {
                <button type="button" [class.on]="listStatus() === s.v" (click)="listStatus.set(s.v)">{{ s.label }}<small>{{ s.n }}</small></button>
              }
            </div>
          </div>
          <div class="ra-list-body">
            @for (g of listGroups(); track g.label) {
              <div class="ra-list-group">{{ g.label }}</div>
              @for (x of g.items; track x.id) {
                <button type="button" class="ra-item" [class.on]="x.id === ra()?.id" (click)="selectRa(x.id)">
                  <span class="ra-item-row"><b><span class="mono">#{{ x.id }}</span> · {{ x.chainName || '—' }}</b><small>{{ whenLabel(x.createdAt) }}</small></span>
                  <span class="ra-item-row mono"><span class="ra-ell">{{ x.receiptNos || 'No CRs yet' }}</span><span>{{ x.crTotal | number:'1.2-2' }}</span></span>
                  <span class="ra-item-row">
                    <span class="ra-pills">
                      <span [class]="'ra-st st-' + x.verificationStatus">{{ statusLabel(x.verificationStatus) }}</span>
                      @if (x.openFlags) { <span class="ra-st st-fix">{{ x.openFlags }} to fix</span> }
                    </span>
                    <small class="ra-ell">{{ x.createdBy }}</small>
                  </span>
                </button>
              }
            } @empty { <div class="ra-empty">No Remittance Advices match.</div> }
          </div>
          <div class="ra-list-foot">↑ ↓ to move between RAs</div>
        </aside>

      <div class="ra-detail">
        <div class="ra-grid">
          <div class="ra-side">
            @if (error()) { <div class="ra-msg err">{{ error() }}</div> }
            @if (ra(); as r) {
              @if (r.verificationStatus === 'Returned' && r.returnReason) { <div class="ra-msg warn">Returned by finance: {{ r.returnReason }}</div> }
              @if (r.verificationStatus === 'Voided') { <div class="ra-msg err">Voided: {{ r.voidReason }}</div> }
              @if (r.bankLine; as b) {
                <div class="ra-msg" [class.ok]="b.confirmed">
                  Bank credit {{ b.account }} · {{ b.txnDate | date:'MM/dd/yyyy' }} · <span class="mono">{{ b.credit | number:'1.2-2' }}</span>@if (b.reference) { · ref {{ b.reference }} }
                  — {{ b.confirmed ? 'approved by ' + b.confirmedBy : 'verifying the RA approves it.' }}
                </div>
              }
              @if (fetching()) { <div class="ra-msg">Getting {{ fetchCount() }} invoice(s) and their RFCs from HO — the lines re-check by themselves every few seconds.</div> }
              @if (r.canEdit && r.missing.length) {
                <div class="ra-notice">
                  <b>Not ready to submit</b>
                  <div class="ra-notice-chips">
                    @if (!r.deposit) { <button type="button" (click)="openPayment()">Payment · enter the deposit (bank account, date)</button> }
                    @for (c of r.crs; track c.id) {
                      @if (toFix(c)) { <button type="button" (click)="pickCr(c.id, 'review')"><span class="mono">CR {{ c.receiptNo }}</span> · {{ toFix(c) }} line(s) to fix</button> }
                    }
                    @for (m of otherMissing(r); track $index) { <button type="button" (click)="goMissing(m.crId)">{{ m.text }}</button> }
                  </div>
                </div>
              }
            }

            <section class="ra-box">
              <div class="ra-box-h" (click)="paymentOpen.set(!paymentOpen())">
                <span class="ra-chev">{{ paymentOpen() ? '▾' : '▸' }}</span>
                <b>Payment</b>
                @if (ra(); as r) {
                  <small>{{ r.chainName || '—' }} · {{ r.paymentMode === 'Check' ? 'Cheque' : 'Bank transfer' }} · <span class="mono">{{ r.checkAmount | number:'1.2-2' }}</span></small>
                  <span class="ra-recon" [class.ok]="r.crTotal === r.checkAmount">CRs <span class="mono">{{ r.crTotal | number:'1.2-2' }}</span> of <span class="mono">{{ r.checkAmount | number:'1.2-2' }}</span></span>
                }
              </div>
              @if (paymentOpen()) {
                <div class="ra-form">
                  <div class="ra-f"><span>Chain</span><input pInputText [(ngModel)]="hdr.chainName" maxlength="50" placeholder="From the RA Excel (e.g. SVI)" [disabled]="!canEdit()" /></div>
                  <div class="ra-f"><span>Payment mode</span>
                    <p-select [(ngModel)]="hdr.paymentMode" [options]="modes" optionLabel="label" optionValue="value" styleClass="w-full" appendTo="body" [disabled]="!canEdit()" /></div>
                  @if (hdr.paymentMode === 'Check') {
                    <div class="ra-f"><span>Bank (drawee)</span><input pInputText [(ngModel)]="hdr.bank" maxlength="50" [disabled]="!canEdit()" /></div>
                    <div class="ra-f2">
                      <div class="ra-f"><span>Check no.</span><input pInputText [(ngModel)]="hdr.checkNo" maxlength="20" class="mono" [disabled]="!canEdit()" /></div>
                      <div class="ra-f"><span>Check date</span>
                        <p-datepicker [(ngModel)]="hdr.checkDate" dateFormat="mm/dd/yy" appendTo="body" styleClass="w-full" inputStyleClass="mono" [disabled]="!canEdit()" /></div>
                    </div>
                  }
                  <div class="ra-f"><span>Amount of the payment</span>
                    <p-inputnumber [(ngModel)]="hdr.checkAmount" mode="decimal" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0"
                      styleClass="w-full" inputStyleClass="mono num-in" [disabled]="!canEdit()" /></div>
                  <div class="ra-f" [class.req]="canEdit() && !hdr.depAccountId"><span>Deposited into *</span>
                    <p-select [(ngModel)]="hdr.depAccountId" [options]="accounts()" optionLabel="label" optionValue="id" [showClear]="canEdit()"
                      placeholder="Company account" styleClass="w-full" appendTo="body" [disabled]="!canEdit()" /></div>
                  <div class="ra-f"><span>Slip / transfer ref. no. (optional)</span>
                    <input pInputText [(ngModel)]="hdr.depSlip" maxlength="30" class="mono" placeholder="Optional" [disabled]="!canEdit()" /></div>
                  <div class="ra-f" [class.req]="canEdit() && !hdr.depDate"><span>Deposit date *</span>
                    <p-datepicker [(ngModel)]="hdr.depDate" dateFormat="mm/dd/yy" appendTo="body" styleClass="w-full" inputStyleClass="mono" [disabled]="!canEdit()" /></div>
                  <div class="ra-f"><span>Remarks (optional)</span><input pInputText [(ngModel)]="hdr.remarks" maxlength="500" [disabled]="!canEdit()" /></div>
                  @if (canEdit()) {
                    <button type="button" class="ra-btn go" [disabled]="busy()" (click)="saveHeader()">{{ ra() ? 'Save payment' : 'Create Remittance Advice' }}</button>
                  }
                </div>
              }
            </section>

            @if (ra(); as r) {
              <section class="ra-box">
                <div class="ra-box-h" (click)="historyOpen.set(!historyOpen())">
                  <span class="ra-chev">{{ historyOpen() ? '▾' : '▸' }}</span><b>History</b><small>{{ r.history.length }} events</small>
                </div>
                @if (historyOpen()) {
                  <div class="ra-hist">
                    @for (h of historyDesc(); track $index) {
                      <div class="ra-hist-row"><span class="mono">{{ h.at | date:'MM/dd/yy hh:mm a' }}</span><b>{{ h.action }}</b><span>{{ h.reason }}</span><small>{{ h.by }}</small></div>
                    }
                  </div>
                }
              </section>
            }
          </div>

          <section class="ra-main">
            @if (ra(); as r) {
              <div class="ra-main-h">
                <span><b>Collection receipts</b> <small>One CR per RA Excel</small></span>
                <div class="ra-actions">
                  @if (r.canVoid) { <button type="button" class="ra-btn danger" (click)="voidVisible = true">Void</button> }
                  @if (r.canSubmit) {
                    <span [pTooltip]="r.missing.length ? 'Not ready: ' + r.missing.join(' ') : (dirtyHeader() ? 'Save the payment first.' : undefined)" tooltipPosition="bottom">
                      <button type="button" class="ra-btn go" [class.dim]="r.missing.length > 0 || dirtyHeader()"
                        [disabled]="busy() || r.missing.length > 0 || dirtyHeader()" (click)="submit()">Submit for verification</button>
                    </span>
                  }
                  @if (r.canVerify) {
                    <button type="button" class="ra-btn danger" (click)="returnVisible = true">Return for correction</button>
                    <button type="button" class="ra-btn go" [disabled]="busy()" (click)="verify()">Verify Remittance Advice</button>
                  }
                </div>
              </div>
              @if (r.canEdit || r.crs.length) {
                <div class="ra-cards">
                  @if (r.canEdit) {
                    <button type="button" class="ra-card ra-card-add" [class.on]="addOpen()" (click)="addOpen.set(!addOpen())"
                      pTooltip="Add CR — upload an RA Excel" tooltipPosition="bottom" aria-label="Add CR">+</button>
                  }
                  @for (c of r.crs; track c.id) {
                    <button type="button" class="ra-card" [class.on]="c.id === selCr()?.id" (click)="pickCr(c.id)"
                      [pTooltip]="(c.deskLabel || '—') + ' desk · ' + c.lines.length + ' invoices · ' + (c.amount | number:'1.2-2')" tooltipPosition="bottom">
                      <span class="mono no">CR {{ c.receiptNo }}</span>
                      @if (r.canEdit) { <span class="ra-st" [class.st-review]="toFix(c)" [class.st-Verified]="!toFix(c)">{{ toFix(c) ? toFix(c) + ' to fix' : 'Ready' }}</span> }
                      @else { <span [class]="'ra-st st-' + c.verificationStatus">{{ statusLabel(c.verificationStatus) }}</span> }
                    </button>
                  }
                </div>
              }
              @if (r.canEdit && addOpen()) {
                <div class="ra-add">
                  <div class="ra-f"><span>RA Excel</span><input type="file" accept=".xls,.xlsx" (change)="pickNew($event)" /></div>
                  <div class="ra-f"><span>OR no.</span><input pInputText [(ngModel)]="newCr.receiptNo" maxlength="20" class="mono w140" placeholder="From the Excel" /></div>
                  <div class="ra-f"><span>DCR no.</span><input pInputText [(ngModel)]="newCr.dcrNo" maxlength="9" class="mono w140" placeholder="From the Excel" /></div>
                  <button type="button" class="ra-btn go sm" [disabled]="busy() || !newCr.file" (click)="addCr()">Upload &amp; apply</button>
                  <small class="ra-add-hint">OR no. and DCR no. fill in from the Excel and stay editable.</small>
                </div>
              }
              @if (selCr(); as c) {
                <div class="ra-cr">
                  <div class="ra-cr-row">
                    <div class="ra-f"><span>OR no.</span><input pInputText [(ngModel)]="edit(c).receiptNo" maxlength="20" class="mono w110" [disabled]="!r.canEdit" /></div>
                    <div class="ra-f"><span>DCR no.</span><input pInputText [(ngModel)]="edit(c).dcrNo" maxlength="9" class="mono w110" [disabled]="!r.canEdit" /></div>
                    <div class="ra-f w130"><span>OR date</span>
                      <p-datepicker [(ngModel)]="edit(c).receiptDate" dateFormat="mm/dd/yy" appendTo="body" styleClass="w-full" inputStyleClass="mono" [disabled]="!r.canEdit" /></div>
                    <div class="ra-f"><span>RA Excel</span>
                      <span class="ra-file">
                        @if (c.excelFile; as x) { <a (click)="open(c.id, x.id, x.fileName)">{{ x.fileName }}</a> }
                        @if (r.canEdit) { <label class="ra-btn xs">Replace<input type="file" accept=".xls,.xlsx" hidden (change)="replaceExcel(c, $event)" /></label> }
                      </span>
                    </div>
                    <span class="ra-grow"></span>
                    @if (r.canEdit) {
                      <span class="ra-cr-act">
                        <label class="ra-btn sm">+ OR soft copy<input type="file" accept=".pdf,.jpg,.jpeg,.png" hidden (change)="addSoftCopy(c, $event)" /></label>
                        <button type="button" class="ra-btn sm" [disabled]="busy()" (click)="saveCr(c)">Save OR / DCR</button>
                        @if (removing() === c.id) {
                          <button type="button" class="ra-btn sm danger" [disabled]="busy()" (click)="removeCr(c)">Confirm remove</button>
                          <button type="button" class="ra-btn sm" (click)="removing.set(null)">Keep</button>
                        } @else { <button type="button" class="ra-btn sm" [disabled]="busy()" (click)="removing.set(c.id)">Remove CR</button> }
                      </span>
                    }
                  </div>
                  @if (c.attachments.length) {
                    <div class="ra-files">
                      @for (a of c.attachments; track a.id) {
                        <span><i class="pi pi-file"></i><a (click)="open(c.id, a.id, a.fileName)">{{ a.fileName }}</a>
                          @if (r.canEdit) { <i class="pi pi-times" pTooltip="Remove" (click)="removeFile(c, a.id)"></i> }</span>
                      }
                    </div>
                  }
                  @for (p of c.homsPostings; track p.id) {
                    @if (p.status !== 'Cancelled') { <div class="ra-msg" [class.ok]="p.status === 'Done'" [class.err]="p.status === 'Held' || p.status === 'Failed'">{{ homsText(p) }}</div> }
                  }
                  <div class="ra-tiles">
                    <div><small>CR amount</small><span class="mono">{{ c.amount | number:'1.2-2' }}</span></div>
                    <div><small>Applied</small><span class="mono">{{ c.applied | number:'1.2-2' }}</span></div>
                    <div pTooltip="Only the net is applied (like the HOMS OR); the RA's deductions stay open on the invoices">
                      <small>RA deductions (left open)</small><span class="mono">{{ c.deductions | number:'1.2-2' }}</span></div>
                    <div><small>Unapplied</small><span class="mono strong" [class.bad]="c.unapplied > 0.005" [class.good]="c.unapplied <= 0.005">{{ c.unapplied | number:'1.2-2' }}</span></div>
                  </div>
                </div>

                <div class="ra-filter">
                  <div class="ra-seg">
                    @for (f of lineFilters(c); track f.v) {
                      <button type="button" [class.on]="lineFilter() === f.v" (click)="lineFilter.set(f.v)">{{ f.label }}<span class="mono">{{ f.n }}</span></button>
                    }
                  </div>
                  <input class="ra-in w230" [ngModel]="query()" (ngModelChange)="query.set($event)" placeholder="Search invoice, customer, branch" />
                </div>

                <div class="ra-tbl-wrap">
                  <table class="ra-tbl">
                    <thead><tr>
                      <th>Row</th><th>Invoice</th><th>Branch</th><th>Customer</th><th>Date</th>
                      <th class="num">Gross (RA)</th><th class="num">System</th><th class="num">Deductions</th><th class="num">Net</th>
                      <th class="num" pTooltip="Gross (RA) − System: 0.00 = they match; negative = the RA shows less than RAM's invoice">Variance</th>
                      <th>Check
                        <button type="button" class="ra-xls" [disabled]="!rows().length" (click)="exportLines(c)"
                          pTooltip="Export the lines shown to Excel" tooltipPosition="left"><i class="pi pi-file-excel"></i> Export</button></th>
                    </tr></thead>
                    <tbody>
                      @for (l of rows(); track l.id) {
                        <tr [class]="'t-' + rowTone(l)" [class.sel]="selLineId() === l.id" (click)="selLineId.set(selLineId() === l.id ? null : l.id)">
                          <td class="mono dim">{{ l.rowNo }}</td>
                          <td class="mono">{{ l.docNo }}</td>
                          <td class="nowrap">{{ l.siteCode ?? l.whseNo }}</td>
                          <td class="cust"><div [title]="l.systemCustName ?? ''">
                            <span class="mono code" [class.bad]="custDiff(l)">{{ l.custKey }}@if (custDiff(l)) { ≠ {{ l.systemCustKey }} }</span>
                            <span class="ra-ell">{{ l.systemCustName }}</span></div></td>
                          <td class="mono nowrap" [class.bad]="dateDiff(l)">{{ l.docDate | date:'MM/dd/yy' }}@if (dateDiff(l)) { · sys {{ l.systemDocDate | date:'MM/dd/yy' }} }</td>
                          <td class="mono num">{{ l.gross | number:'1.2-2' }}</td>
                          <td class="mono num" [class.bad]="l.systemTotal !== null && l.systemTotal !== l.gross">{{ l.systemTotal === null ? '—' : (l.systemTotal | number:'1.2-2') }}</td>
                          <td class="mono num dim" [pTooltip]="dedTip(l)">{{ dedTotal(l) ? (dedTotal(l) | number:'1.2-2') : '—' }}</td>
                          <td class="mono num">{{ l.net | number:'1.2-2' }}</td>
                          <td class="mono num" [class.bad]="!!variance(l)" [class.dim]="variance(l) === 0">{{ variance(l) === null ? '—' : (variance(l) | number:'1.2-2') }}</td>
                          <td class="nowrap">
                            @if (!l.flags.length) { <span class="tone-ok">Matched</span> }
                            @for (f of l.flags; track f; let last = $last) {
                              <span [class]="'tone-' + flagTone(f)" [pTooltip]="flagTip(l, f)">{{ flagLabel(f) }}@if (f === 'RfcDiff') { <span class="rfc-nos">({{ rfcNos(l) }})</span> }</span>@if (!last) { <span class="dim"> · </span> }
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                  @if (!rows().length) {
                    <div class="ra-empty">{{ query().trim() ? 'No lines match your search.' : lineFilter() === 'review' ? 'Nothing to fix in this CR — every line matches its invoice.' : 'No lines.' }}</div>
                  }
                </div>
                <div class="ra-foot"><span>Showing {{ rows().length }} of {{ c.lines.length }} lines</span><span>Deductions = CEWT + RFC/BO/RTV + Other (hover for breakdown)</span></div>
              } @else {
                <div class="ra-empty">No CRs yet — use the <b>+ Add CR</b> tile to upload the first RA Excel.</div>
              }
            } @else {
              <div class="ra-main-h"><span><b>Collection receipts</b> <small>One CR per RA Excel</small></span></div>
              <div class="ra-empty">{{ canEncode
                ? 'Fill in the payment and create the Remittance Advice, then add one CR per RA Excel.'
                : 'Pick a Remittance Advice from the list.' }}</div>
            }
          </section>
        </div>
      </div>
    </div>

    <p-dialog header="Return for correction" [modal]="true" [(visible)]="returnVisible" [style]="{ width: '28rem' }">
      <textarea pTextarea [(ngModel)]="returnReason" rows="3" maxlength="500" class="w-full" placeholder="What the Custodian Verifier has to fix"></textarea>
      <ng-template #footer>
        <button type="button" class="ra-btn" (click)="returnVisible = false">Cancel</button>
        <button type="button" class="ra-btn danger" [disabled]="busy() || !returnReason.trim()" (click)="doReturn()">Return all CRs</button>
      </ng-template>
    </p-dialog>
    <p-dialog header="Void Remittance Advice" [modal]="true" [(visible)]="voidVisible" [style]="{ width: '28rem' }">
      <textarea pTextarea [(ngModel)]="voidReason" rows="3" maxlength="500" class="w-full" placeholder="Reason"></textarea>
      <ng-template #footer>
        <button type="button" class="ra-btn" (click)="voidVisible = false">Cancel</button>
        <button type="button" class="ra-btn danger" [disabled]="busy() || !voidReason.trim()" (click)="doVoid()">Void RA and its CRs</button>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    /* Page styles live in styles.scss under .ra (Remittance Advice design). */
    :host { display: block; height: 100%; }
  `]
})
export class RemittancePageComponent implements OnInit, OnDestroy {
  private api = inject(RemittanceService);
  private cols = inject(CollectionService);
  private bank = inject(BankStatementService);
  private exporter = inject(ExportService);
  private toolbar = inject(GlobalToolbarService);
  private tabBar = inject(TabBarService);
  private router = inject(Router);
  private toast = inject(MessageService);
  private auth = inject(AuthService);

  protected readonly statusLabel = statusLabel;
  protected readonly modes = [{ label: 'Cheque', value: 'Check' }, { label: 'Bank transfer / direct deposit', value: 'BankTransfer' }];
  protected readonly canEncode = this.auth.isAdmin() || this.auth.hasPermission('collections-ra-encode');

  ra = signal<RemittanceDto | null>(null);
  list = signal<RemittanceListItemDto[]>([]);
  ho = signal<CollectionSiteDto | null>(null);
  accounts = signal<BankAccountOptionDto[]>([]);
  busy = signal(false);
  refreshing = signal(false);
  error = signal<string | null>(null);
  /** CR whose "Remove" waits for a second click. */
  removing = signal<number | null>(null);
  returnVisible = false; returnReason = '';
  voidVisible = false; voidReason = '';

  hdr = this.blankHeader();
  private savedHeader = '';
  private crEdits: Record<number, CrEdit> = {};
  newCr: { receiptNo: string; dcrNo: string; file: File | null } = { receiptNo: '', dcrNo: '', file: null };

  canEdit = computed(() => this.ra()?.canEdit ?? this.canEncode);
  fetchCount = computed(() => this.ra()?.crs.reduce((n, c) => n + c.lines.filter(l => l.flags.includes('Fetching')).length, 0) ?? 0);
  fetching = computed(() => this.fetchCount() > 0);

  // ── view state (presentation only) ──
  selCrId = signal<number | null>(null);
  /** Line row highlighted by a click (screen aid only). */
  selLineId = signal<number | null>(null);
  lineFilter = signal<LineFilter>('review');
  query = signal('');
  paymentOpen = signal(true);
  historyOpen = signal(false);
  addOpen = signal(false);
  listQuery = signal('');
  listStatus = signal('all');

  selCr = computed(() => {
    const r = this.ra();
    return r ? r.crs.find(c => c.id === this.selCrId()) ?? r.crs[0] ?? null : null;
  });
  rows = computed(() => {
    const c = this.selCr(), f = this.lineFilter(), q = this.query().trim().toLowerCase();
    if (!c) return [];
    return c.lines.filter(l => {
      const ok = this.matches(l);
      if ((f === 'review' && ok) || (f === 'ok' && !ok)) return false;
      return !q || [String(l.docNo), l.custKey, l.systemCustName ?? '', l.siteCode ?? ''].some(x => x.toLowerCase().includes(q));
    });
  });
  historyDesc = computed(() => [...(this.ra()?.history ?? [])].reverse());
  listChips = computed(() => {
    const l = this.list();
    return LIST_STATUSES.map(([v, label]) => ({ v, label, n: v === 'all' ? l.length : l.filter(x => x.verificationStatus === v).length }))
      .filter(x => !['Returned', 'Voided'].includes(x.v) || x.n > 0);
  });
  visibleList = computed(() => {
    const s = this.listStatus(), q = this.listQuery().trim().toLowerCase();
    return this.list().filter(x => (s === 'all' || x.verificationStatus === s)
      && (!q || `#${x.id} ${x.chainName} ${x.receiptNos} ${x.checkNo ?? ''}`.toLowerCase().includes(q)));
  });
  listGroups = computed(() => {
    const groups: { label: string; items: RemittanceListItemDto[] }[] = [];
    for (const x of this.visibleList()) {
      const days = this.daysAgo(x.createdAt), label = days === 0 ? 'Today' : days === 1 ? 'Yesterday' : 'Earlier';
      let g = groups.find(y => y.label === label);
      if (!g) groups.push(g = { label, items: [] });
      g.items.push(x);
    }
    return groups;
  });

  private navSub?: Subscription;
  private pollSub?: Subscription;

  ngOnInit() {
    this.api.headOffice().subscribe(res => {
      this.ho.set(res.data ?? null);
      if (res.data) this.bank.depositAccounts(res.data.id).subscribe(a => this.accounts.set(a.data ?? []));
    });
    this.loadList();
    this.handleNavState();
    this.navSub = this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => this.handleNavState());
    // One tab for the page (the list picks the RA in place): unsaved payment edits of the new or open RA.
    this.tabBar.registerDirtyChecker(NEW_KEY, () => this.ra()
      ? this.dirtyHeader()
      : !!(this.hdr.chainName.trim() || this.hdr.checkNo.trim() || this.hdr.checkAmount));
    this.syncToolbar();
  }

  ngOnDestroy() {
    this.tabBar.unregisterDirtyChecker(NEW_KEY);
    this.toolbar.clear();
    this.navSub?.unsubscribe();
    this.pollSub?.unsubscribe();
  }

  /** ↑ / ↓ move between the RAs of the list (not while typing). */
  @HostListener('window:keydown', ['$event'])
  onKey(e: KeyboardEvent) {
    const tag = (e.target as HTMLElement)?.tagName;
    if (/INPUT|TEXTAREA|SELECT/.test(tag) || (e.target as HTMLElement)?.isContentEditable || e.ctrlKey || e.altKey || e.metaKey) return;
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
    const ids = this.visibleList().map(x => x.id), i = ids.indexOf(this.ra()?.id ?? -1);
    const next = ids[i < 0 ? 0 : Math.max(0, Math.min(ids.length - 1, i + (e.key === 'ArrowDown' ? 1 : -1)))];
    if (next != null && next !== this.ra()?.id) { e.preventDefault(); this.selectRa(next); }
  }

  private handleNavState() {
    const id = (history.state as Record<string, unknown>)?.['remittanceId'] as number | undefined;
    if (id) { if (id !== this.ra()?.id) this.load(id); return; }
    this.reset();
  }

  private reset() {
    this.pollSub?.unsubscribe();
    this.ra.set(null);
    this.error.set(null);
    this.hdr = this.blankHeader();
    this.crEdits = {};
    this.paymentOpen.set(true);
    this.tabBar.updateTabState(NEW_KEY, undefined);
    this.loadList();
    this.syncToolbar();
  }

  private loadList() { this.api.list().subscribe(res => this.list.set(res.data ?? [])); }

  private load(id: number) {
    this.refreshing.set(true);
    this.api.get(id).subscribe({
      next: res => { this.refreshing.set(false); if (res.data) this.show(res.data, true); },
      error: err => { this.refreshing.set(false); this.error.set(err?.error?.message ?? 'Could not load the Remittance Advice.'); }
    });
  }

  /** reset = take the server's payment and OR / DCR values; otherwise unsaved edits survive the refresh. */
  private show(r: RemittanceDto, reset = false) {
    const sameRa = this.ra()?.id === r.id;
    const keepHeader = sameRa && !reset && this.dirtyHeader();
    this.ra.set(r);
    this.error.set(null);
    const server = {
      chainName: r.chainName, paymentMode: r.paymentMode, bank: r.bank ?? '', checkNo: r.checkNo ?? '', checkDate: this.toDate(r.checkDate),
      checkAmount: r.checkAmount, depAccountId: r.deposit?.bankAccountId ?? null, depSlip: r.deposit?.slipRefNo ?? '',
      depDate: this.toDate(r.deposit?.depositDate ?? null), remarks: r.remarks ?? '',
    };
    if (!keepHeader) this.hdr = server;
    this.savedHeader = JSON.stringify(server);
    if (!sameRa || reset) this.crEdits = {};
    else for (const id of Object.keys(this.crEdits)) if (!r.crs.some(c => c.id === +id)) delete this.crEdits[+id];
    if (!sameRa) {
      // A different RA: start on its first CR, on the lines to fix when it has any.
      const first = r.crs[0];
      this.selCrId.set(first?.id ?? null);
      this.lineFilter.set(first && this.toFix(first) ? 'review' : 'all');
      this.query.set('');
      this.paymentOpen.set(r.canEdit);
      this.historyOpen.set(false);
      this.addOpen.set(r.canEdit && !r.crs.length);
      this.removing.set(null);
    }
    // The page's tab remembers the RA (switching tabs and back reopens it).
    this.tabBar.updateTabState(NEW_KEY, { remittanceId: r.id });
    this.syncToolbar();
    this.watchFetch();
    if (!sameRa && r.canEdit && !this.fetching() && r.crs.some(c => c.lines.some(l => l.flags.includes('RfcDiff')))) {
      // "RFC differs": have HO re-read those invoices' RFCs (BMS may have posted them since; the server does it at most every 30 min).
      this.api.rematch(r.id).subscribe(res => { if (res.data && this.ra()?.id === r.id) { this.ra.set(res.data); this.watchFetch(); } });
    }
  }

  /** While HO is fetching invoices, re-check the lines every 8 s (up to ~5 min). */
  private watchFetch() {
    this.pollSub?.unsubscribe();
    const r = this.ra();
    if (!r || !r.canEdit || !this.fetching()) return;
    this.pollSub = interval(8000).pipe(take(40), switchMap(() => this.api.rematch(r.id))).subscribe({
      next: res => {
        if (!res.data) return;
        this.ra.set(res.data);
        if (!this.fetching()) { this.pollSub?.unsubscribe(); this.toast.add({ severity: 'success', summary: 'RA', detail: 'All invoices are in — lines re-checked.' }); }
      },
      error: () => this.pollSub?.unsubscribe()
    });
  }

  private syncToolbar() {
    const r = this.ra();
    this.toolbar.set({
      title: r ? `Remittance Advice #${r.id}` : 'Remittance Advice',
      add: this.canEncode ? { onClick: () => this.newRa() } : undefined,
      refresh: { onClick: () => { this.loadList(); if (r) this.load(r.id); }, loading: this.refreshing },
      list: { onClick: () => this.router.navigate(['/collections/list']) },
    });
  }

  newRa() {
    if (this.dirtyHeader()) { this.toast.add({ severity: 'warn', summary: 'RA', detail: 'Save the payment first (or refresh to drop the changes).' }); return; }
    this.reset();
  }

  /** Opens an RA from the list in place (same tab). */
  selectRa(id: number) {
    if (id === this.ra()?.id) return;
    if (this.dirtyHeader()) { this.toast.add({ severity: 'warn', summary: 'RA', detail: 'Save the payment first (or refresh to drop the changes).' }); return; }
    this.load(id);
  }

  dirtyHeader() { return !!this.ra() && this.ra()!.canEdit && JSON.stringify(this.hdr) !== this.savedHeader; }

  // ── actions ──

  private run(call: Observable<ApiResponse<RemittanceDto>>, done?: string, after?: () => void, reset = false, refreshOnError = false) {
    this.busy.set(true);
    call.subscribe({
      next: res => {
        this.busy.set(false);
        if (!res.data) return;
        this.show(res.data, reset);
        this.loadList();
        if (done) this.toast.add({ severity: 'success', summary: 'Remittance Advice', detail: done });
        after?.();
      },
      error: err => {
        this.busy.set(false);
        this.toast.add({ severity: 'error', summary: 'Remittance Advice', detail: err?.error?.message ?? 'Request failed.', life: 8000 });
        // Submit re-checks every line first: show the refreshed flags (unsaved edits are kept).
        if (refreshOnError && this.ra()) this.api.get(this.ra()!.id).subscribe(r => r.data && this.show(r.data));
      }
    });
  }

  saveHeader() {
    const h = this.hdr;
    if ((h.depAccountId || h.depSlip.trim() || h.depDate) && !(h.depAccountId && h.depDate)) {
      this.toast.add({ severity: 'warn', summary: 'RA', detail: 'Complete the deposit: bank account and date (the slip / ref. no. is optional).' });
      return;
    }
    const dto: SaveRemittanceDto = {
      chainName: h.chainName.trim() || null, paymentMode: h.paymentMode as 'Check' | 'BankTransfer',
      bank: h.paymentMode === 'Check' ? h.bank.trim() || null : null, checkNo: h.paymentMode === 'Check' ? h.checkNo.trim() || null : null,
      checkDate: h.paymentMode === 'Check' ? this.iso(h.checkDate) : null, checkAmount: h.checkAmount ?? 0,
      deposit: h.depAccountId && h.depDate ? { bankAccountId: h.depAccountId, slipRefNo: h.depSlip.trim(), depositDate: this.iso(h.depDate)! } : null,
      remarks: h.remarks.trim() || null,
    };
    const r = this.ra();
    this.run(r ? this.api.update(r.id, dto) : this.api.create(dto), r ? 'Payment saved.' : 'Remittance Advice created — add its CRs.', undefined, true);
  }

  edit(c: RemittanceCrDto): CrEdit {
    return this.crEdits[c.id] ??= { receiptNo: c.receiptNo, dcrNo: c.dcrNo?.toString() ?? '', receiptDate: this.toDate(c.receiptDate) };
  }

  pickNew(e: Event) { this.newCr.file = (e.target as HTMLInputElement).files?.[0] ?? null; }

  addCr() {
    const r = this.ra();
    if (!r || !this.newCr.file) return;
    if (this.dirtyHeader()) { this.toast.add({ severity: 'warn', summary: 'RA', detail: 'Save the payment first.' }); return; }
    this.run(this.api.uploadCr(r.id, null, this.newCr.file, this.newCr.receiptNo.trim(), this.newCr.dcrNo.trim(), null), 'CR added from the RA Excel.',
      () => {
        this.newCr = { receiptNo: '', dcrNo: '', file: null };
        this.addOpen.set(false);
        const added = this.ra()?.crs.at(-1);
        if (added) this.pickCr(added.id);
      });
  }

  replaceExcel(c: RemittanceCrDto, e: Event) {
    const input = e.target as HTMLInputElement, file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const x = this.edit(c);
    this.run(this.api.uploadCr(this.ra()!.id, c.id, file, x.receiptNo.trim(), x.dcrNo.trim(), this.iso(x.receiptDate)), `CR ${c.receiptNo}: RA Excel replaced, lines re-checked.`,
      () => delete this.crEdits[c.id]);
  }

  saveCr(c: RemittanceCrDto) {
    const x = this.edit(c);
    this.run(this.api.updateCr(this.ra()!.id, c.id, { receiptNo: x.receiptNo.trim(), dcrNo: x.dcrNo.trim(), receiptDate: this.iso(x.receiptDate) }), 'CR saved.',
      () => delete this.crEdits[c.id]);
  }

  removeCr(c: RemittanceCrDto) {
    this.removing.set(null);
    this.run(this.api.removeCr(this.ra()!.id, c.id), `CR ${c.receiptNo} removed.`);
  }

  addSoftCopy(c: RemittanceCrDto, e: Event) {
    const input = e.target as HTMLInputElement, file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.busy.set(true);
    this.cols.addAttachment(c.id, file).subscribe({
      next: () => { this.busy.set(false); this.load(this.ra()!.id); },
      error: err => { this.busy.set(false); this.toast.add({ severity: 'error', summary: 'Attachment', detail: err?.error?.message ?? 'Upload failed.' }); }
    });
  }

  removeFile(c: RemittanceCrDto, attachmentId: number) {
    this.busy.set(true);
    this.cols.deleteAttachment(c.id, attachmentId).subscribe({
      next: () => { this.busy.set(false); this.load(this.ra()!.id); },
      error: err => { this.busy.set(false); this.toast.add({ severity: 'error', summary: 'Attachment', detail: err?.error?.message ?? 'Could not remove it.' }); }
    });
  }

  open(collectionId: number, attachmentId: number, fileName: string) {
    this.cols.attachment(collectionId, attachmentId).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      if (!/\.(pdf|png|jpe?g)$/i.test(fileName)) a.download = fileName; else a.target = '_blank';
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    });
  }

  submit() { this.run(this.api.submit(this.ra()!.id), 'Submitted to the Finance Verifier.', undefined, false, true); }
  verify() { this.run(this.api.verify(this.ra()!.id), 'Verified — one OR per CR is queued for HOMS.'); }
  doReturn() { this.run(this.api.returnRa(this.ra()!.id, this.returnReason.trim()), 'Returned to the Custodian Verifier.', () => { this.returnVisible = false; this.returnReason = ''; }); }
  doVoid() { this.run(this.api.voidRa(this.ra()!.id, this.voidReason.trim()), 'Voided.', () => { this.voidVisible = false; this.voidReason = ''; }); }

  // ── view helpers ──

  pickCr(id: number, f?: LineFilter) {
    const c = this.ra()?.crs.find(x => x.id === id);
    if (!c) return;
    this.selCrId.set(id);
    this.selLineId.set(null);
    this.lineFilter.set(f ?? (this.toFix(c) ? 'review' : 'all'));
    this.query.set('');
    this.removing.set(null);
  }

  openPayment() { this.paymentOpen.set(true); }

  /** "Not ready" items other than the deposit and the lines to fix (those have their own chips). */
  otherMissing(r: RemittanceDto) {
    return r.missing.filter(m => !m.includes('Enter the deposit') && !m.includes("don't match their invoice")).map(m => {
      const no = /^CR ([^:]+): /.exec(m)?.[1];
      const cr = no ? r.crs.find(c => c.receiptNo === no) : undefined;
      return { text: cr ? `CR ${no} · ${m.slice(no!.length + 5)}` : m, crId: cr?.id ?? null };
    });
  }
  goMissing(crId: number | null) { if (crId) this.pickCr(crId); else this.openPayment(); }

  /** Lines of a CR that don't match their invoice (submit is blocked until the Excel is corrected). */
  toFix(c: RemittanceCrDto) { return c.lines.filter(l => !this.matches(l)).length; }
  lineFilters(c: RemittanceCrDto) {
    const fix = this.toFix(c);
    return [
      { v: 'review' as LineFilter, label: 'Needs review', n: fix },
      { v: 'ok' as LineFilter, label: 'Matched', n: c.lines.length - fix },
      { v: 'all' as LineFilter, label: 'All', n: c.lines.length },
    ];
  }
  /** No flags: it matches its invoice, so it's applied. */
  matches(l: RemittanceLineDto) { return !l.flags.length; }
  flagLabel(f: RemittanceFlag) { return FLAG_LABELS[f] ?? f; }
  flagTone(f: RemittanceFlag) { return f === 'Fetching' ? 'wait' : HARD_FLAGS.includes(f) ? 'hard' : 'fix'; }
  rowTone(l: RemittanceLineDto) { return this.matches(l) ? 'ok' : l.flags.some(f => HARD_FLAGS.includes(f)) ? 'hard' : 'fix'; }
  custDiff(l: RemittanceLineDto) { return !!l.systemCustKey && l.systemCustKey !== l.custKey; }
  dateDiff(l: RemittanceLineDto) { return !!l.systemDocDate && !!l.docDate && l.systemDocDate !== l.docDate; }
  dedTotal(l: RemittanceLineDto) { return l.cewt + l.rfcBoRtv + l.other; }
  dedTip(l: RemittanceLineDto) {
    return l.deductions.length
      ? l.deductions.map(x => `${DEDUCTION_TYPES.find(t => t.value === x.type)?.label ?? x.type} ${this.money(x.amount)}${x.remark && x.type !== 'Ewt2307' ? ' — ' + x.remark : ''}`).join(' · ')
      : 'No deductions';
  }
  /** Gross (RA) − System; null when the invoice isn't in HOMSys. */
  variance(l: RemittanceLineDto) { return l.systemTotal === null ? null : Math.round((l.gross - l.systemTotal) * 100) / 100; }
  /** Labels that explain themselves on hover. */
  flagTip(l: RemittanceLineDto, f: RemittanceFlag) {
    if (f === 'VatExcluded' && l.systemTotal !== null)
      return `RA ${this.money(l.gross)} = invoice ${this.money(l.systemTotal)} without its 12% VAT — short ${this.money(l.systemTotal - l.gross)}`;
    if (f === 'RfcDiff')
      return `The RA deducts RFC/BO/RTV ${this.money(l.rfcBoRtv)}; RFCs posted in BMS: `
        + (l.rfcs?.length ? `${l.rfcs.length} RFC${l.rfcs.length === 1 ? '' : 's'}, total ` : '') + this.money(l.systemRfc ?? 0)
        + (l.rfcs?.length ? ' — ' + l.rfcs.map(x => `${x.rfcNo ? 'RFC ' + x.rfcNo : 'RFC (no. not synced yet)'}${x.rfcDate ? ' ' + this.shortDate(x.rfcDate) : ''} ${this.money(x.amount)}`).join(', ') : '')
        + ' — the records don\'t match: post the RFC in BMS (HOMSys re-reads it from HO) or correct the RA Excel';
    if (f === 'OverBalance' && l.openBalance !== null)
      return `Open balance ${this.money(l.openBalance)}, less than the net ${this.money(l.net)}`
        + (l.appliedElsewhere ? ` — already applied: ${l.appliedElsewhere}` : '')
        + (l.systemRfc ? ` — RFCs posted in BMS: ${this.money(l.systemRfc)}` : '');
    return undefined;
  }
  private money(n: number) { return n.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
  /** The invoice's RFC #s for the "RFC differs" label: first two, then "+N"; all of them (with dates / amounts) on hover. */
  rfcNos(l: RemittanceLineDto) {
    const nos = (l.rfcs ?? []).filter(x => x.rfcNo).map(x => String(x.rfcNo));
    if (!nos.length) return l.rfcs?.length ? 'RFC # not synced yet' : 'no RFC posted';
    return `${nos.length} RFC${nos.length === 1 ? '' : 's'}: ` + nos.slice(0, 2).join(', ') + (nos.length > 2 ? ` +${nos.length - 2}` : '');
  }
  private shortDate(iso: string) { const [y, m, d] = iso.split('-'); return `${m}/${d}/${y.slice(2)}`; }
  private fullDate(iso: string | null) { if (!iso) return null; const [y, m, d] = iso.slice(0, 10).split('-'); return `${m}/${d}/${y}`; }
  /** The lines shown (current filter + search) as an Excel sheet, one row per line, with the full check text and RFC #s. */
  exportLines(c: RemittanceCrDto) {
    const r = this.ra();
    if (!r) return;
    const headers = ['Row', 'Invoice', 'Branch', 'Cust key (RA)', 'Cust key (system)', 'Customer', 'Date (RA)', 'Date (system)',
      'Gross (RA)', 'System', 'CEWT', 'RFC/BO/RTV', 'Other', 'Deductions', 'Net', 'Variance', 'Check',
      'RFCs posted in BMS', 'RFC #s', 'Deduction details'];
    const rows = this.rows().map(l => [
      l.rowNo, l.docNo, l.siteCode ?? l.whseNo, l.custKey, l.systemCustKey, l.systemCustName, this.fullDate(l.docDate), this.fullDate(l.systemDocDate),
      l.gross, l.systemTotal, l.cewt, l.rfcBoRtv, l.other, this.dedTotal(l), l.net, this.variance(l),
      l.flags.length ? l.flags.map(f => this.flagLabel(f)).join(' · ') : 'Matched',
      l.systemRfc,
      (l.rfcs ?? []).map(x => `${x.rfcNo || '(no. not synced)'} (${x.rfcDate ? this.shortDate(x.rfcDate) + ', ' : ''}${this.money(x.amount)})`).join(', '),
      l.deductions.length ? this.dedTip(l) : '',
    ]);
    const view = this.lineFilter() === 'review' ? 'needs review' : this.lineFilter() === 'ok' ? 'matched' : '';
    const name = `RA ${r.id} ${r.chainName || ''} - CR ${c.receiptNo || c.id}` + (view ? ` (${view})` : '') + (this.query().trim() ? ' (search)' : '');
    this.exporter.exportExcelRows(name.replace(/[\\/:*?"<>|]/g, '-').replace(/\s+/g, ' ').trim(), 'Lines', headers, rows, [8, 9, 10, 11, 12, 13, 14, 15, 17]);
  }
  homsText(p: HomsPostingDto) {
    const name = `HOMS ${p.kind}${p.docNo ? ' ' + p.docNo : ''}`;
    switch (p.status) {
      case 'Done': return `${name}: posted${p.file ? ' in ' + p.file : ''}.`;
      case 'Held': return `${name}: on hold — ${p.message}`;
      case 'Failed': return `${name}: failed — ${p.message}`;
      default: return `${name}: queued — the HO watcher writes it within a minute.`;
    }
  }
  private daysAgo(iso: string) {
    const d = new Date(iso), t = new Date();
    return Math.round((new Date(t.getFullYear(), t.getMonth(), t.getDate()).getTime() - new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()) / 864e5);
  }
  /** Today: the time; this week: the weekday; older: the date. */
  whenLabel(iso: string) {
    const d = new Date(iso), days = this.daysAgo(iso);
    if (days === 0) return d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
    if (days < 7) return d.toLocaleDateString('en-US', { weekday: 'short' });
    return d.toLocaleDateString('en-US', { month: '2-digit', day: '2-digit', year: '2-digit' });
  }

  private blankHeader() {
    return { chainName: '', paymentMode: 'Check', bank: '', checkNo: '', checkDate: null as Date | null, checkAmount: null as number | null,
      depAccountId: null as number | null, depSlip: '', depDate: null as Date | null, remarks: '' };
  }
  private toDate(s: string | null) { return s ? new Date(s + 'T00:00:00') : null; }
  private iso(d: Date | null) {
    return d ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` : null;
  }
}
