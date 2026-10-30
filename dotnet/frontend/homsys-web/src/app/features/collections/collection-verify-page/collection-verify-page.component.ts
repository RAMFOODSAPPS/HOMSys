import { Component, computed, HostListener, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { CollectionService } from '../../../core/services/collection.service';
import { BankStatementService } from '../../../core/services/bank-statement.service';
import { RemittanceService } from '../../../core/services/remittance.service';
import { BankStatementLineDto, CollectionDto, CollectionListItemDto, CollectionSiteDto, DEDUCTION_TYPES, inDateRange, PAYMENT_MODES, RemittanceListItemDto } from '../../../core/models/collection.model';

/** Matured PDCs are confirmed by the CNC Verifier on the PDC Monitor (that posts the JV), not here. */
type QueueTab = 'ForVerification' | 'Returned' | 'Verified' | 'all';

/** One line of the "Check against the receipt" checklist. */
interface CheckItem { key: string; label: string; value: string; sub: string; mono?: boolean; }

const TABS: { value: QueueTab; label: string }[] = [
  { value: 'ForVerification', label: 'For verification' }, { value: 'Returned', label: 'Returned' },
  { value: 'Verified', label: 'Verified' }, { value: 'all', label: 'All' },
];
const RETURN_REASONS = ['Amount mismatch', 'Wrong customer', 'Check details mismatch', 'Unreadable attachment', 'Wrong invoice applied'];
const MODE_ORDER: Record<string, number> = { BankTransfer: 0, PostDatedCheck: 1, Check: 2, Cash: 3 };
const SHORT_MODE: Record<string, string> = { Cash: 'CASH', Check: 'CHEQUE',PostDatedCheck: 'PDC', BankTransfer: 'TRANSFER' };

/**
 * Finance Verifier queue (Verification Queue design): queue on the left, the receipt to check in the middle
 * (tick every item against the soft copy before Verify), the soft copy on the right (zoom / rotate / hide).
 * J / K still move through the queue (no on-screen hint); after Verify or Return the next receipt opens.
 */
@Component({
  selector: 'app-collection-verify-page',
  standalone: true,
  providers: [MessageService],
  imports: [ToastModule, TooltipModule, FormsModule, DatePipe, DecimalPipe],
  template: `
    <p-toast position="top-right" />
    <div class="rv vq" [class.no-preview]="!showPreview()">

      <aside class="vq-side">
        <div class="vq-side-top">
          <div class="vq-side-title"><span>Verification queue</span></div>
          <div class="vq-tabs">
            @for (t of tabs; track t.value) {
              <button type="button" [class.on]="tab() === t.value" (click)="tab.set(t.value)">
                @if (t.value !== 'all') { <span class="dot" [class]="'dot dot-' + t.value"></span> }
                {{ t.label }}<span class="mono cnt">{{ counts()[t.value] || 0 }}</span>
              </button>
            }
          </div>
          <select class="sel" [ngModel]="siteId()" (ngModelChange)="siteId.set($event)">
            <option [ngValue]="null">All branches</option>
            @for (s of sites(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
          </select>
          <div class="dates" title="Receipt date">
            <input type="date" class="sel" [ngModel]="from()" (ngModelChange)="from.set($event || null)" aria-label="Receipt date from" />
            <span class="muted">to</span>
            <input type="date" class="sel" [ngModel]="to()" (ngModelChange)="to.set($event || null)" aria-label="Receipt date to" />
          </div>
          <div class="search">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7"></circle><path d="m20 20-3.5-3.5"></path></svg>
            <input [ngModel]="q()" (ngModelChange)="q.set($event)" placeholder="Receipt or customer" />
          </div>
        </div>
        <div class="vq-list">
          @for (r of raQueue(); track r.id) {
            <button type="button" class="vq-item ra-row" (click)="openRa(r.id)" [title]="'Opens the Remittance Advice — verified or returned as a whole'">
              <span class="mono strong">RA #{{ r.id }} · {{ r.chainName }}</span>
              <span class="mono strong r">{{ r.crTotal | number:'1.2-2' }}</span>
              <span class="cust">{{ r.crCount }} CR{{ r.crCount === 1 ? '' : 's' }}: {{ r.receiptNos }}</span>
              <span class="mode mode-RA">REMITTANCE</span>
              <span class="st"><span [class]="'dot dot-' + r.verificationStatus"></span>{{ statusText(r.verificationStatus) }} · HO</span>
              <span class="r wait">{{ r.checkNo ? 'check ' + r.checkNo : '' }}</span>
            </button>
          }
          @for (it of queue(); track it.id) {
            <button type="button" class="vq-item" [class.on]="col()?.id === it.id" [class.bank]="it.bankMatch" (click)="select(it)">
              <span class="mono strong">{{ it.receiptType }} {{ it.receiptNo }}</span>
              <span class="mono strong r">{{ it.amount | number:'1.2-2' }}</span>
              <span class="cust">{{ it.custName || it.custKey }}</span>
              <span [class]="'mode mode-' + it.paymentMode">{{ shortMode(it.paymentMode) }}</span>
              <span class="st"><span [class]="'dot dot-' + it.verificationStatus"></span>{{ statusText(it.verificationStatus) }} · {{ it.siteCode }}</span>
              <span class="r wait" [class.late]="waiting(it) >= 3">Waiting {{ it.verificationStatus === 'Verified' ? '—' : waiting(it) + 'd' }}</span>
              @if (it.bankMatch) { <span class="bm-tag">✓ {{ it.bankMatch === 'Approved' ? 'Bank match approved' : 'Bank match found' }}</span> }
            </button>
          } @empty {
            <div class="empty">{{ loading() ? 'Loading…' : 'Nothing in this view.' }}</div>
          }
        </div>
      </aside>

      <section class="vq-main">
        @if (col(); as c) {
          <div class="vq-head">
            <div class="vq-title">
              <h1>{{ c.receiptType }} {{ c.receiptNo }} <span class="dot-sep">·</span> {{ c.siteName }}</h1>
              <span [class]="'pill st-' + c.verificationStatus">{{ statusText(c.verificationStatus) }}</span>
              <span class="pill">{{ c.paymentStatus }}</span>
              @if (c.pdcState) { <span class="pill pill-warn">PDC: {{ c.pdcState }}</span> }
            </div>
            <div class="vq-head-r">
              @if (!showPreview() && (c.attachments.length || c.bankLine)) {
                <button type="button" class="btn-sm" (click)="showPreview.set(true)">
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 3H6a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8z"></path><path d="M14 3v5h5"></path></svg>
                  Show {{ fileName() || 'preview' }}</button>
              }
              <a href="" (click)="$event.preventDefault(); openFull(c.id)">Open full page</a>
            </div>
          </div>

          <div class="vq-body">
            <div class="vq-check-head">
              <span class="strong">Check against the receipt</span>
              <div class="prog">
                <div class="bar"><div [style.width.%]="progress().pct"></div></div>
                <span class="mono muted">{{ progress().done }} / {{ progress().total }}</span>
                @if (isOpen()) { <button type="button" class="link" (click)="checkAll()">{{ progress().done < progress().total ? 'Check all' : 'Uncheck all' }}</button> }
              </div>
            </div>
            <div class="vq-checks">
              @for (k of checks(); track k.key) {
                <button type="button" class="chk" [class.on]="isChecked(k.key)" [disabled]="!isOpen()" (click)="toggle(k.key)">
                  <span class="box">{{ isChecked(k.key) ? '✓' : '' }}</span>
                  <span class="muted lbl">{{ k.label }}</span>
                  <span class="val"><span [class.mono]="k.mono">{{ k.value }}</span><span class="muted">{{ k.sub }}</span></span>
                </button>
              }
            </div>

            @if (c.bankLine; as b) {
              <div class="vq-bank" [class.ok]="b.confirmed">
                <span class="ico">✓</span>
                <div class="txt">
                  <span class="strong">{{ b.confirmed ? 'Bank match approved' + (b.confirmedBy ? ' by ' + b.confirmedBy : '') : 'Bank match found — verifying approves it' }}</span>
                  <span class="mono">{{ b.account }} · {{ b.txnDate | date:'MM/dd/yyyy' }} · credit {{ b.credit | number:'1.2-2' }}@if (b.reference) { · ref {{ b.reference }} }</span>
                </div>
                <button type="button" class="btn-sm" (click)="viewBank()">View in bank statement</button>
              </div>
            }

            <div class="vq-inv">
              <span class="strong">Applied to invoices</span>
              <div class="box-tbl">
                <div class="tbl-wrap">
                  <!-- Fixed layout: the money columns share one width so they line up evenly. -->
                  <table class="tbl" style="table-layout: fixed">
                    <colgroup>
                      <col style="width: 40px" /><col style="width: 17%" />
                      <col style="width: 14%" /><col style="width: 14%" /><col style="width: 14%" />
                      <col /><col style="width: 14%" />
                    </colgroup>
                    <thead><tr><th class="n">#</th><th>Invoice / date</th><th class="num">Total</th><th class="num">RFC</th><th class="num">Applied</th><th class="num">Deduction</th><th class="num" [pTooltip]="balanceHint(c)" tooltipPosition="top">{{ balanceLabel(c) }}</th></tr></thead>
                    <tbody class="mono">
                      @for (i of c.invoices; track i.postedInvoiceId; let n = $index) {
                        <tr>
                          <td class="n muted">{{ n + 1 }}</td>
                          <td class="nowrap">{{ i.docNo }}<div class="sub">{{ i.docDate | date:'MM/dd/yyyy' }}</div></td>
                          <td class="num">{{ i.total | number:'1.2-2' }}</td>
                          <td class="num" [class.neg]="i.rfcTotal < 0">{{ i.rfcTotal | number:'1.2-2' }}</td>
                          <td class="num strong">{{ i.amountApplied | number:'1.2-2' }}</td>
                          <td class="ded num">
                            @for (d of dedOf(c, i.postedInvoiceId); track d.id) { <div>{{ dedLabel(d.type) }} {{ d.amount | number:'1.2-2' }}@if (d.reference) { <span class="muted"> {{ d.reference }}</span> }</div> }
                            @empty { <span class="nil">—</span> }
                          </td>
                          <td class="num strong">{{ i.balance | number:'1.2-2' }}</td>
                        </tr>
                      } @empty { <tr><td colspan="7" class="muted">On-account (no invoice).</td></tr> }
                    </tbody>
                  </table>
                </div>
                <div class="inv-foot">
                  <span>Received <b class="mono">{{ c.amount | number:'1.2-2' }}</b></span>
                  <span>Applied <b class="mono">{{ c.amount - c.unappliedAmount | number:'1.2-2' }}</b></span>
                  @if (deductions(c)) { <span>Deductions <b class="mono">{{ deductions(c) | number:'1.2-2' }}</b></span> }
                  <span>Unapplied <b class="mono">{{ c.unappliedAmount | number:'1.2-2' }}</b></span>
                </div>
              </div>
            </div>

            @if (c.remarks) { <div class="muted">Remarks: {{ c.remarks }}</div> }
            <div class="muted">Encoded by {{ c.createdBy }} · submitted {{ c.submittedAt | date:'MM/dd/yyyy hh:mm a' }}</div>
          </div>

          <div class="vq-foot">
            @if (returning()) {
              <div class="ret">
                <span class="strong">Reason for returning</span>
                <div class="chips">
                  @for (r of reasons; track r) { <button type="button" [class.on]="reason() === r" (click)="reason.set(reason() === r ? '' : r)">{{ r }}</button> }
                </div>
                <textarea rows="2" [ngModel]="note()" (ngModelChange)="note.set($event)" placeholder="Add details for the branch (optional)"></textarea>
              </div>
            }
            <div class="foot-bar">
              <span class="muted">{{ hint() }}</span>
              <div class="foot-btns">
                @if (c.canConfirmMatch) {
                  <button type="button" class="btn btn-ret" [disabled]="busy()" (click)="matchAction(c, false)">Reject bank match</button>
                  <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="matchAction(c, true)">Approve bank match</button>
                } @else {
                @if (returning()) { <button type="button" class="btn" (click)="cancelReturn()">Cancel</button> }
                <button type="button" class="btn btn-ret" [disabled]="!canAct() || busy() || (returning() && !reason() && !note().trim())"
                  (click)="returning() ? doReturn() : returning.set(true)">{{ returning() ? 'Confirm return' : 'Return to branch' }}</button>
                @if (!returning()) {
                  <button type="button" class="btn btn-primary" [disabled]="!canAct() || busy() || progress().done < progress().total" (click)="verify(c)">Verify</button>
                }
                }
              </div>
            </div>
          </div>
        } @else {
          <div class="empty">{{ loading() ? 'Loading…' : 'Select a receipt on the left.' }}</div>
        }
      </section>

      @if (showPreview()) {
        <section class="vq-prev">
          <div class="prev-head">
            @if (col()?.bankLine) {
              <div class="prev-tabs">
                <button type="button" [class.on]="pane() === 'receipt'" (click)="pane.set('receipt')">Receipt</button>
                <button type="button" [class.on]="pane() === 'bank'" (click)="viewBank()">Bank statement</button>
              </div>
            }
            <div class="prev-name">
              @if (pane() === 'bank') {
                <span>{{ col()?.bankLine?.account }} · {{ col()?.bankLine?.txnDate | date:'MM/dd/yyyy' }}</span>
              } @else {
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#64748b" stroke-width="2"><path d="M14 3H6a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8z"></path><path d="M14 3v5h5"></path></svg>
              @if ((col()?.attachments?.length ?? 0) > 1) {
                <select class="sel sm" [ngModel]="viewing()" (ngModelChange)="pick($event)">
                  @for (a of col()!.attachments; track a.id) { <option [ngValue]="a.id">{{ a.fileName }}</option> }
                </select>
              } @else { <span>{{ fileName() || 'No soft copy' }}</span> }
              }
            </div>
            <div class="prev-tools">
              @if (imgUrl() && pane() === 'receipt') {
                <button type="button" title="Zoom out" (click)="zoomBy(-0.25)">−</button>
                <span class="mono muted zoom">{{ zoom() * 100 | number:'1.0-0' }}%</span>
                <button type="button" title="Zoom in" (click)="zoomBy(0.25)">+</button>
                <button type="button" title="Rotate" (click)="rot.set((rot() + 90) % 360)"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 12a8 8 0 1 1-2.3-5.7"></path><path d="M20 4v5h-5"></path></svg></button>
                <span class="sep"></span>
              }
              <button type="button" title="Hide preview" (click)="showPreview.set(false)">×</button>
            </div>
          </div>
          @if (pane() === 'bank') {
            <div class="prev-body bank-prev">
              @if (bankLoading()) { <span class="muted">Loading the bank statement…</span> }
              @else {
                <table class="tbl">
                  <thead><tr><th>Description / ref.</th><th class="num">Debit</th><th class="num">Credit</th><th class="num">Balance</th></tr></thead>
                  <tbody>
                    @for (l of bankRows(); track l.id) {
                      <tr [class.hit]="l.id === col()?.bankLine?.id" [attr.id]="'bl-' + l.id">
                        <td>{{ l.description }}<div class="sub mono">{{ l.reference || l.checkNo || '' }}@if (l.txnTime) { · {{ l.txnTime }} }@if (l.matchedSlip && l.id !== col()?.bankLine?.id) { · {{ l.matchedSiteCode }} {{ l.matchedSlip }} }</div></td>
                        <td class="num mono">{{ l.debit ? (l.debit | number:'1.2-2') : '' }}</td>
                        <td class="num mono">{{ l.credit ? (l.credit | number:'1.2-2') : '' }}</td>
                        <td class="num mono muted">{{ l.balance === null ? '' : (l.balance | number:'1.2-2') }}</td>
                      </tr>
                    } @empty { <tr><td colspan="4" class="muted">No lines on this date.</td></tr> }
                  </tbody>
                </table>
              }
            </div>
          } @else {
          <div class="prev-body">
            @if (pdfUrl()) { <iframe [src]="pdfUrl()" title="Receipt"></iframe> }
            @else if (imgUrl()) { <img [src]="imgUrl()" alt="Receipt" [style.transform]="'scale(' + zoom() + ') rotate(' + rot() + 'deg)'" /> }
            @else { <span class="muted">{{ col() ? 'No soft copy attached.' : '' }}</span> }
          </div>
          }
        </section>
      }
    </div>
  `,
  styles: [`
    /* Page styles live in styles.scss under .rv.vq (Verification Queue design). */
    :host { display: block; height: 100%; }
  `]
})
export class CollectionVerifyPageComponent implements OnInit, OnDestroy {
  private api = inject(CollectionService);
  private router = inject(Router);
  private raApi = inject(RemittanceService);
  private toolbar = inject(GlobalToolbarService);
  private toast = inject(MessageService);
  private sanitizer = inject(DomSanitizer);
  private bank = inject(BankStatementService);

  protected readonly tabs = TABS;
  protected readonly reasons = RETURN_REASONS;

  all = signal<CollectionListItemDto[]>([]);
  sites = signal<CollectionSiteDto[]>([]);
  col = signal<CollectionDto | null>(null);
  loading = signal(true);
  busy = signal(false);
  tab = signal<QueueTab>('ForVerification');
  siteId = signal<number | null>(null);
  q = signal('');
  /** Receipt-date range (yyyy-MM-dd from the date inputs). */
  from = signal<string | null>(null);
  to = signal<string | null>(null);
  /** Ticked checklist items per collection (screen aid only, not saved). */
  checked = signal<Record<number, Record<string, boolean>>>({});
  showPreview = signal(true);
  viewing = signal<number | null>(null);
  pdfUrl = signal<SafeResourceUrl | null>(null);
  imgUrl = signal<string | null>(null);
  zoom = signal(1);
  rot = signal(0);
  returning = signal(false);
  reason = signal('');
  note = signal('');
  private objectUrl: string | null = null;
  /** Preview pane: the receipt soft copy, or the bank statement around the matched credit. */
  pane = signal<'receipt' | 'bank'>('receipt');
  bankRows = signal<BankStatementLineDto[]>([]);
  bankLoading = signal(false);
  private bankFor: number | null = null;

  /** Receipts finance looks at here (drafts and voided stay out). */
  private inScope = computed(() => this.all().filter(c => !c.remittanceId && ['ForVerification', 'Returned', 'Verified'].includes(c.verificationStatus)));
  /** Remittance Advices (NKA): one queue row per RA; opening it shows every CR and accepted difference. */
  ras = signal<RemittanceListItemDto[]>([]);
  raQueue = computed(() => {
    const tab = this.tab(), t = this.q().toLowerCase().trim(), site = this.siteId();
    if (site) return [];   // RAs are Head Office's
    return this.ras().filter(r => ['ForVerification', 'Returned', 'Verified'].includes(r.verificationStatus)
        && (tab === 'all' || r.verificationStatus === tab)
        && (!t || [`ra ${r.id}`, r.chainName, r.checkNo ?? '', r.receiptNos].some(x => x.toLowerCase().includes(t))))
      .sort((a, b) => (a.submittedAt ?? '').localeCompare(b.submittedAt ?? ''));
  });
  counts = computed(() => {
    const site = this.siteId(), from = this.from(), to = this.to(), r: Record<string, number> = {};
    for (const c of this.inScope().filter(x => (!site || x.siteId === site) && inDateRange(x.receiptDate, from, to))) {
      r[c.verificationStatus] = (r[c.verificationStatus] ?? 0) + 1; r['all'] = (r['all'] ?? 0) + 1;
    }
    if (!site) for (const x of this.ras().filter(x => ['ForVerification', 'Returned', 'Verified'].includes(x.verificationStatus))) {
      r[x.verificationStatus] = (r[x.verificationStatus] ?? 0) + 1; r['all'] = (r['all'] ?? 0) + 1;
    }
    return r;
  });
  /** Bank transfers first, then PDCs, then checks and cash; oldest submission first within each. */
  queue = computed(() => {
    const tab = this.tab(), site = this.siteId(), t = this.q().toLowerCase().trim(), from = this.from(), to = this.to();
    return this.inScope()
      .filter(c => (tab === 'all' || c.verificationStatus === tab)
                   && (!site || c.siteId === site) && inDateRange(c.receiptDate, from, to))
      .filter(c => !t || [c.receiptType + ' ' + c.receiptNo, c.custKey, c.custName].some(x => x.toLowerCase().includes(t)))
      .sort((a, b) => (MODE_ORDER[a.paymentMode] ?? 9) - (MODE_ORDER[b.paymentMode] ?? 9) || (a.submittedAt ?? '').localeCompare(b.submittedAt ?? ''));
  });

  isOpen = computed(() => this.col()?.verificationStatus === 'ForVerification');
  canAct = computed(() => !!this.col()?.canVerify);

  checks = computed<CheckItem[]>(() => {
    const c = this.col();
    if (!c) return [];
    const mode = PAYMENT_MODES.find(m => m.value === c.paymentMode)?.label ?? c.paymentMode;
    const applied = c.amount - c.unappliedAmount;
    const d = new DatePipe('en-US'), n = new DecimalPipe('en-US');
    const items: CheckItem[] = [
      { key: 'receipt', label: 'Receipt', value: `${c.receiptType} ${c.receiptNo}`, sub: `Dated ${d.transform(c.receiptDate, 'MM/dd/yyyy')}`, mono: true },
      c.multiCustomer
        ? { key: 'customer', label: 'Customers', value: `Chain payment — ${new Set(c.invoices.map(i => i.custKey)).size} customers`,
            sub: [...new Set(c.invoices.map(i => `${i.custKey} ${i.custName}`))].join(' · ') }
        : { key: 'customer', label: 'Customer', value: c.custName || c.custKey, sub: `Customer key ${c.custKey}` },
      { key: 'amount', label: 'Amount', value: `₱ ${n.transform(c.amount, '1.2-2')}`, sub: mode, mono: true },
    ];
    if (c.checkNo) items.push({ key: 'check', label: 'Check', value: `${c.bank} #${c.checkNo}`, sub: `Check date ${d.transform(c.checkDate, 'MM/dd/yyyy')}` });
    if (c.deposit) items.push({ key: 'deposit', label: 'Deposit', value: `${c.deposit.bank}${c.deposit.slipRefNo ? ' slip ' + c.deposit.slipRefNo : ' (no reference)'}`,
      sub: `${d.transform(c.deposit.depositDate, 'MM/dd/yyyy')} · ${!c.bankLine ? 'not yet on the bank statement'
        : `bank credit ${d.transform(c.bankLine.txnDate, 'MM/dd/yyyy')} ₱ ${n.transform(c.bankLine.credit, '1.2-2')}${c.bankLine.reference ? ' ref ' + c.bankLine.reference : ''}`
          + (c.bankLine.confirmed ? ' (approved)' : ' — verifying approves this match')}` });
    items.push({ key: 'alloc', label: 'Allocation', value: `₱ ${n.transform(applied, '1.2-2')} to ${c.invoices.length} invoice${c.invoices.length === 1 ? '' : 's'}`,
      sub: `Unapplied ₱ ${n.transform(c.unappliedAmount, '1.2-2')}` });
    items.push({ key: 'attach', label: 'Attachment', value: c.attachments.map(a => a.fileName).join(', ') || 'None attached', sub: 'Readable, signed and stamped' });
    return items;
  });
  progress = computed(() => {
    const c = this.col(), keys = this.checks().map(k => k.key);
    const ck = (c && this.checked()[c.id]) || {};
    const done = keys.filter(k => ck[k]).length;
    return { done, total: keys.length, pct: keys.length ? Math.round(done / keys.length * 100) : 0 };
  });
  hint = computed(() => {
    const c = this.col();
    if (!c) return '';
    if (c.canConfirmMatch) return 'The bank credit was found after verification — compare it with the deposit, then approve or reject the match.';
    if (!this.isOpen()) return `This receipt is ${this.statusText(c.verificationStatus).toLowerCase()}.`;
    if (!c.canVerify) return 'You encoded this collection — another verifier has to check it.';
    if (this.returning()) return 'Pick a reason or add a note.';
    if (this.progress().done < this.progress().total) return 'Tick each item to enable Verify.';
    return 'Ready to verify. The next receipt opens automatically.';
  });
  fileName = computed(() => this.col()?.attachments.find(a => a.id === this.viewing())?.fileName ?? this.col()?.attachments[0]?.fileName ?? '');

  ngOnInit() {
    this.api.mySites().subscribe(res => this.sites.set(res.data ?? []));
    this.load(true);
    this.toolbar.set({
      title: 'Verification Queue',
      refresh: { onClick: () => this.load(), loading: this.loading },
      list: { onClick: () => this.router.navigate(['/collections/list']) },
    });
  }

  ngOnDestroy() {
    this.toolbar.clear();
    this.clearPreview();
  }

  /** J / K move through the queue (not while typing). */
  @HostListener('window:keydown', ['$event'])
  onKey(e: KeyboardEvent) {
    const tag = (e.target as HTMLElement)?.tagName;
    if (/INPUT|TEXTAREA|SELECT/.test(tag) || e.ctrlKey || e.altKey || e.metaKey) return;
    const k = e.key.toLowerCase();
    if (k !== 'j' && k !== 'k') return;
    const list = this.queue(), i = list.findIndex(x => x.id === this.col()?.id);
    const next = list[Math.max(0, Math.min(list.length - 1, i + (k === 'j' ? 1 : -1)))];
    if (next && next.id !== this.col()?.id) this.select(next);
  }

  openRa(id: number) { this.router.navigate(['/collections/remittance'], { state: { remittanceId: id } }); }

  load(selectFirst = false, afterId?: number) {
    this.loading.set(true);
    this.raApi.list().subscribe(res => this.ras.set(res.data ?? []));
    this.api.list(null, null).subscribe({
      next: res => {
        this.all.set(res.data ?? []);
        this.loading.set(false);
        const current = this.col();
        if (afterId !== undefined || selectFirst || (current && !this.queue().some(x => x.id === current.id))) {
          const next = this.queue().find(x => x.id !== afterId);
          if (next) this.select(next); else { this.col.set(null); this.clearPreview(); }
        }
      },
      error: () => this.loading.set(false)
    });
  }

  /** Shows the matched credit in its day's bank statement (same account), the line highlighted. */
  viewBank() {
    const c = this.col(), b = c?.bankLine;
    if (!c || !b) return;
    this.showPreview.set(true);
    this.pane.set('bank');
    if (this.bankFor === b.id) { this.scrollToHit(); return; }
    this.bankLoading.set(true);
    this.bankRows.set([]);
    this.bank.lines(c.deposit?.bankAccountId ?? null, b.txnDate, b.txnDate, 'all').subscribe({
      next: res => {
        this.bankFor = b.id;
        this.bankRows.set((res.data ?? []).filter(l => l.accountLabel === b.account));
        this.bankLoading.set(false);
        this.scrollToHit();
      },
      error: () => this.bankLoading.set(false)
    });
  }

  private scrollToHit() {
    setTimeout(() => document.getElementById('bl-' + this.col()?.bankLine?.id)?.scrollIntoView({ block: 'center' }));
  }

  select(row: CollectionListItemDto) {
    this.returning.set(false); this.reason.set(''); this.note.set('');
    this.pane.set('receipt'); this.bankFor = null; this.bankRows.set([]);
    this.api.get(row.id).subscribe(res => {
      const c = res.data!;
      this.col.set(c);
      this.clearPreview();
      const first = c.attachments[0];
      if (first) this.preview(c.id, first.id, first.contentType);
      if (c.canConfirmMatch) this.viewBank();   // a verified CR's late match: the bank credit is what's being checked
    });
  }

  pick(attachmentId: number) {
    const c = this.col(), a = c?.attachments.find(x => x.id === attachmentId);
    if (c && a) this.preview(c.id, a.id, a.contentType);
  }

  preview(collectionId: number, attachmentId: number, contentType: string) {
    this.api.attachment(collectionId, attachmentId).subscribe(blob => {
      this.clearPreview();
      this.objectUrl = URL.createObjectURL(blob);
      this.viewing.set(attachmentId);
      if (contentType === 'application/pdf') this.pdfUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl));
      else this.imgUrl.set(this.objectUrl);
    });
  }

  private clearPreview() {
    if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
    this.objectUrl = null;
    this.viewing.set(null);
    this.pdfUrl.set(null);
    this.imgUrl.set(null);
    this.zoom.set(1);
    this.rot.set(0);
  }

  zoomBy(d: number) { this.zoom.set(Math.min(3, Math.max(0.5, +(this.zoom() + d).toFixed(2)))); }

  isChecked(key: string) { const c = this.col(); return !!(c && this.checked()[c.id]?.[key]); }

  toggle(key: string) {
    const c = this.col();
    if (!c) return;
    this.checked.update(m => ({ ...m, [c.id]: { ...(m[c.id] ?? {}), [key]: !m[c.id]?.[key] } }));
  }

  checkAll() {
    const c = this.col();
    if (!c) return;
    const on = this.progress().done < this.progress().total;
    this.checked.update(m => ({ ...m, [c.id]: Object.fromEntries(this.checks().map(k => [k.key, on])) }));
  }

  cancelReturn() { this.returning.set(false); this.reason.set(''); this.note.set(''); }

  /** Verified CR whose match was found later: approving makes it Deposited; rejecting keeps that line out of auto-matching.
   *  (A matured PDC's match is the CNC Verifier's, on the PDC Monitor.) */
  matchAction(c: CollectionDto, approve: boolean) {
    this.busy.set(true);
    (approve ? this.api.confirmMatch(c.id) : this.api.rejectMatch(c.id, null)).subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.add({ severity: 'success', summary: 'Bank match', detail: approve
          ? `${c.receiptType} ${c.receiptNo} deposited.` : `${c.receiptType} ${c.receiptNo}: match rejected.` });
        this.load(false, c.id);
      },
      error: err => this.fail(err)
    });
  }

  verify(c: CollectionDto) {
    this.busy.set(true);
    this.api.verify(c.id).subscribe({
      next: () => { this.busy.set(false); this.toast.add({ severity: 'success', summary: 'Verified', detail: `${c.receiptType} ${c.receiptNo} — queued for HOMS posting.` }); this.load(false, c.id); },
      error: err => this.fail(err)
    });
  }

  doReturn() {
    const c = this.col();
    if (!c) return;
    const reason = [this.reason(), this.note().trim()].filter(Boolean).join(' — ');
    this.busy.set(true);
    this.api.returnToBranch(c.id, reason).subscribe({
      next: () => {
        this.busy.set(false);
        this.cancelReturn();
        this.toast.add({ severity: 'warn', summary: 'Returned', detail: `${c.receiptType} ${c.receiptNo} sent back to the branch.` });
        this.load(false, c.id);
      },
      error: err => this.fail(err)
    });
  }

  openFull(id: number) { this.router.navigate(['/collections'], { state: { collectionId: id } }); }

  dedOf(c: CollectionDto, invoiceId: number) { return c.deductions.filter(d => d.postedInvoiceId === invoiceId); }
  deductions(c: CollectionDto) { return c.deductions.reduce((s, d) => s + d.amount, 0); }
  /** Invoice balance after this receipt: "Current Balance" once it's Deposited (on the bank statement), else "Balance To Be". */
  balanceLabel(c: CollectionDto) { return c.paymentStatus === 'Deposited' ? 'Current Balance' : 'Balance To Be'; }
  balanceHint(c: CollectionDto) {
    return c.paymentStatus === 'Deposited'
      ? 'Invoice balance after this receipt — its deposit is on the bank statement'
      : 'Invoice balance once this receipt goes through — not yet deposited';
  }

  dedLabel(t: string) { return DEDUCTION_TYPES.find(d => d.value === t)?.label ?? t; }
  shortMode(m: string) { return SHORT_MODE[m] ?? m; }
  statusText(s: string) { return s === 'ForVerification' ? 'For verification' : s; }

  /** Whole days since the custodian submitted it. */
  waiting(c: CollectionListItemDto) {
    return c.submittedAt ? Math.max(0, Math.floor((Date.now() - new Date(c.submittedAt).getTime()) / 86_400_000)) : 0;
  }

  private fail(err: { error?: { message?: string } }) {
    this.busy.set(false);
    this.toast.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Please try again.' });
  }
}
