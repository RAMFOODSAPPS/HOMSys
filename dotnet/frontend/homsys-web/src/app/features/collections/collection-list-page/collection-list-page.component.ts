import { AfterViewInit, Component, computed, ElementRef, HostListener, inject, OnDestroy, OnInit, signal, ViewChild } from '@angular/core';
import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { AuthService } from '../../../core/services/auth.service';
import { CollectionService } from '../../../core/services/collection.service';
import { ApiResponse } from '../../../core/models/auth.model';
import {
  CollectionDto, CollectionListItemDto, CollectionSiteDto, DEDUCTION_TYPES, inDateRange, InvoiceHistoryDto, isoDay, PAYMENT_MODES, RECEIPT_TYPES, statusLabel
} from '../../../core/models/collection.model';
import { collectionSteps } from '../collection-steps';

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Branch', field: 'siteCode' },
  { header: 'Type', field: 'receiptType' },
  { header: 'Receipt No.', field: 'receiptNo' },
  { header: 'Receipt Date', field: 'receiptDate' },
  { header: 'Customer', field: 'custKey' },
  { header: 'Customer Name', field: 'custName' },
  { header: 'Mode', field: 'paymentMode' },
  { header: 'Amount', field: 'amount' },
  { header: 'Unapplied', field: 'unappliedAmount' },
  { header: 'Invoices', field: 'invoiceCount' },
  { header: 'Deposit Slip', field: 'depositSlipRef' },
  { header: 'Payment', field: 'paymentStatus' },
  { header: 'Document Status', field: 'verificationStatus', formatter: v => statusLabel(v as string) },
  { header: 'PDC', field: 'pdcState' },
  { header: 'Check Date', field: 'checkDate' },
  { header: 'Encoded By', field: 'createdBy' },
  { header: 'HOMS Posting', field: 'homs', formatter: v => (v as string) ?? '' },
  { header: 'Found in HOMS', field: 'postedInBms', formatter: v => (v ? 'Yes' : '') },
];

const C = { maroon: '#860000', amber: '#B7791F', blue: '#2563C9', slate: '#5B6676', green: '#1E8A4C', red: '#C8261B' };
const DOC: Record<string, string> = { Draft: C.slate, Returned: C.amber, ForVerification: C.blue, Verified: C.green, Voided: C.red };
const PAY: Record<string, string> = {
  Draft: C.slate, Collected: C.blue, Pending: C.blue, Deposited: C.green,
  Immature: C.amber, Matured: C.maroon, Succeeded: C.green, Bounced: C.red,
};
const HOMS: Record<string, string> = { Posted: C.green, Queued: C.blue, Held: C.amber, Failed: C.red };
const VIEW_KEY = 'homsys_collections_view';

/** Needs someone's attention: still a draft / returned, money left on-account, or a bounced check. */
const attention = (c: CollectionListItemDto) =>
  c.verificationStatus === 'Draft' || c.verificationStatus === 'Returned' || c.unappliedAmount > 0 || c.pdcState === 'Bounced';
const payOf = (c: CollectionListItemDto) => c.pdcState ?? c.paymentStatus;
const modeOf = (m: string) => m === 'PostDatedCheck' ? 'PDC' : m === 'BankTransfer' ? 'Bank transfer' : m === 'Check' ? 'Cheque' : m;

interface Action { label: string; kind: 'primary' | 'secondary' | 'danger'; run: () => void; disabled?: boolean; hint?: string }

/**
 * Collections list (design: claude.ai/design "Collections List v2", 2026-10-09): document-status tabs, filters, and a
 * Table or Reading-pane view with a preview of the selected receipt. Every action in the preview is the collection
 * page's own (same endpoints, shown only when the server's Can* flags allow it); the progress tracker is shared.
 */
@Component({
  selector: 'app-collection-list-page',
  standalone: true,
  providers: [MessageService],
  imports: [SelectModule, DatePickerModule, InputTextModule, ToggleSwitchModule, TooltipModule, ToastModule, FormsModule,
    DatePipe, DecimalPipe, NgTemplateOutlet],
  template: `
    <p-toast position="top-right" />
    <div class="pdcm cll">
      <section #sec class="pdcm-panel cll-fit" [style.height.px]="view() === 'split' ? secH() : null">
        <div class="pdcm-tabs">
          @for (t of tabs(); track t.key) {
            <button type="button" [class.on]="tab() === t.key" (click)="tab.set(t.key); resetPage()">
              <i [style.background]="t.color"></i>{{ t.label }}
              <span class="pdcm-badge" [class.has]="t.count > 0" [style.--c]="t.key === 'all' ? '#1C1B1A' : t.color">{{ t.count }}</span>
            </button>
          }
        </div>

        <div class="pdcm-filters">
          <input pInputText class="pdcm-search" [ngModel]="q()" (ngModelChange)="q.set($event); resetPage()" placeholder="Search receipt, customer, deposit ref." />
          <p-select [options]="branchOpts()" [ngModel]="branch()" (ngModelChange)="branch.set($event); resetPage()" [showClear]="true" placeholder="All my branches" appendTo="body" />
          <p-select [options]="modeOpts()" [ngModel]="modeF()" (ngModelChange)="modeF.set($event); resetPage()" optionLabel="label" optionValue="value" [showClear]="true" placeholder="All modes" appendTo="body" />
          <p-select [options]="payOpts()" [ngModel]="pay()" (ngModelChange)="pay.set($event); resetPage()" [showClear]="true" placeholder="All payment statuses" appendTo="body" />
          <span class="pdcm-win">Receipt date
            <p-datepicker [ngModel]="from()" (ngModelChange)="from.set($event); resetPage()" dateFormat="mm/dd/yy" [showButtonBar]="true" appendTo="body" styleClass="cll-date" placeholder="From" />
            –
            <p-datepicker [ngModel]="to()" (ngModelChange)="to.set($event); resetPage()" dateFormat="mm/dd/yy" [showButtonBar]="true" appendTo="body" styleClass="cll-date" placeholder="To" />
          </span>
          <label class="pdcm-switch"><p-toggleswitch [ngModel]="attnOnly()" (ngModelChange)="attnOnly.set($event); resetPage()" /> Needs attention</label>
          @if (hasFilters()) { <button type="button" class="cll-clear" (click)="clearFilters()">Clear filters</button> }
          <div class="cll-filter-acts">
            @if (canEncode) { <button type="button" class="pdcm-btn primary" (click)="newCollection()">+ New collection</button> }
            <button type="button" class="pdcm-btn" (click)="exportSvc.exportExcel('Collections', cols, exportRows())">Export</button>
          </div>
        </div>

        <div class="cll-body" [class.split]="view() === 'split'">
          <div class="cll-list">
            @if (view() === 'table') {
              <div class="pdcm-scroll">
                <table class="pdcm-table cll-table">
                  <thead>
                    <tr>
                      <th class="pdcm-sort" (click)="sortBy('date')">Receipt · date {{ sort() === 'date' ? (asc() ? '↑' : '↓') : '' }}</th>
                      <th>Customer</th><th>Mode</th>
                      <th class="num cll-sortable" (click)="sortBy('amount')">Amount {{ sort() === 'amount' ? (asc() ? '↑' : '↓') : '↕' }}</th>
                      <th class="num">Unapplied</th><th>Deposit ref.</th><th>Document</th><th>Payment</th><th>HOMS</th><th>Encoded by</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (c of pageRows(); track c.id) {
                      <tr [class.sel]="selId() === c.id" (click)="select(c.id)" (dblclick)="openFull(c.id)">
                        <td class="nw cll-acc" [class.on]="selId() === c.id">
                          <div class="cll-strong">{{ c.receiptType }} {{ c.receiptNo }}@if (c.remittanceId) { <small class="pdcm-dim" title="Part of a Remittance Advice"> · RA #{{ c.remittanceId }}</small> }</div>
                          <small>{{ c.receiptDate | date:'MM/dd/yyyy' }} · <span class="pdcm-mono">{{ c.siteCode }}</span></small>
                        </td>
                        <td class="cll-cust">{{ c.custKey }} {{ c.custName }}</td>
                        <td class="nw"><div>{{ mode(c.paymentMode) }}</div>@if (c.checkDate && c.paymentMode === 'PostDatedCheck') { <small>dated {{ c.checkDate | date:'MM/dd/yy' }}</small> }</td>
                        <td class="num pdcm-mono nw">{{ c.amount | number:'1.2-2' }}</td>
                        <td class="num pdcm-mono nw" [class.cll-un]="c.unappliedAmount > 0">{{ c.unappliedAmount > 0 ? (c.unappliedAmount | number:'1.2-2') : '—' }}</td>
                        <td class="pdcm-mono pdcm-dim nw">{{ c.depositSlipRef || '—' }}</td>
                        <td><span class="pdcm-status" [style.--c]="docColor(c.verificationStatus)"><i></i>{{ label(c.verificationStatus) }}</span></td>
                        <td><span class="pdcm-status" [style.--c]="payColor(payOf(c))"><i></i>{{ payOf(c) }}</span></td>
                        <td class="nw cll-homs" [style.color]="homsColor(c)">{{ homsText(c) }}</td>
                        <td class="nw pdcm-dim">{{ c.createdBy }}</td>
                      </tr>
                    }
                  </tbody>
                  @if (list().length) {
                    <tfoot>
                      <tr>
                        <td colspan="3">Total · {{ list().length }} collection{{ list().length === 1 ? '' : 's' }}</td>
                        <td class="num pdcm-mono">{{ total() | number:'1.2-2' }}</td>
                        <td class="num pdcm-mono" [class.cll-un]="unTotal() > 0">{{ unTotal() > 0 ? (unTotal() | number:'1.2-2') : '—' }}</td>
                        <td colspan="5"></td>
                      </tr>
                    </tfoot>
                  }
                </table>
              </div>
            } @else {
              <div class="cll-split-head">
                <span>{{ list().length }} collection{{ list().length === 1 ? '' : 's' }} · ₱{{ total() | number:'1.2-2' }}</span>
                <button type="button" class="cll-clear" (click)="sortBy('date')">By date {{ asc() ? '↑' : '↓' }}</button>
              </div>
              <div class="cll-cards">
                @for (g of groups(); track g.label) {
                  <div class="cll-group">{{ g.label }}</div>
                  @for (c of g.items; track c.id) {
                    <button type="button" class="cll-card" [class.on]="selId() === c.id" (click)="select(c.id)">
                      <div class="cll-card-row"><b>{{ c.custName || c.custKey }}</b><span class="pdcm-mono cll-strong">{{ c.amount | number:'1.2-2' }}</span></div>
                      <div class="cll-card-row pdcm-dim"><span>{{ c.receiptType }} {{ c.receiptNo }} · {{ mode(c.paymentMode) }}</span><small>{{ c.receiptDate | date:'MM/dd/yyyy' }}</small></div>
                      <div class="cll-card-pills">
                        <span class="pdcm-status" [style.--c]="docColor(c.verificationStatus)">{{ label(c.verificationStatus) }}</span>
                        <span class="pdcm-status" [style.--c]="payColor(payOf(c))">{{ payOf(c) }}</span>
                        @if (c.unappliedAmount > 0) { <span class="cll-un cll-strong">₱{{ c.unappliedAmount | number:'1.2-2' }} unapplied</span> }
                      </div>
                    </button>
                  }
                }
              </div>
            }
            @if (!list().length) { <div class="pdcm-empty">{{ loading() ? 'Loading…' : 'No collections match these filters.' }}</div> }

            <div class="pdcm-pager">
              <span>{{ list().length ? ((page() - 1) * pageSize() + 1) + '–' + min(page() * pageSize(), list().length) + ' of ' + list().length : '0 of 0' }}</span>
              <div class="pdcm-pages">
                <button type="button" [disabled]="page() === 1" (click)="goto(1)" aria-label="First page">«</button>
                <button type="button" [disabled]="page() === 1" (click)="goto(page() - 1)" aria-label="Previous page">‹</button>
                @for (n of pageNums(); track n) { <button type="button" class="pdcm-num" [class.on]="n === page()" (click)="goto(n)">{{ n }}</button> }
                <button type="button" [disabled]="page() === pageCount()" (click)="goto(page() + 1)" aria-label="Next page">›</button>
                <button type="button" [disabled]="page() === pageCount()" (click)="goto(pageCount())" aria-label="Last page">»</button>
              </div>
              <p-select [options]="sizeOpts" [ngModel]="pageSize()" (ngModelChange)="pageSize.set($event); resetPage()" optionLabel="label" optionValue="value" appendTo="body" />
            </div>
          </div>

          @if (view() === 'split') {
            <div class="cll-pane"><ng-container [ngTemplateOutlet]="preview" /></div>
          }
        </div>
      </section>
    </div>

    @if (view() === 'table' && selId()) {
      <div class="cll-overlay" (click)="close()"></div>
      <div class="cll-pane cll-drawer"><ng-container [ngTemplateOutlet]="preview" /></div>
    }

    <ng-template #preview>
      @if (selRow(); as r) {
        <div class="cll-pv-head">
          <div class="cll-pv-title">
            <b>{{ r.receiptType }} {{ r.receiptNo }}</b>
            <small>{{ detail()?.siteName ?? r.siteCode }}</small>
            <span class="pdcm-status" [style.--c]="docColor(r.verificationStatus)">{{ label(r.verificationStatus) }}</span>
            <span class="pdcm-status" [style.--c]="payColor(payOf(r))">{{ r.pdcState ? 'PDC: ' + r.pdcState : r.paymentStatus }}</span>
            <span class="cll-sep">·</span>
            <span class="cll-pv-cust" [title]="r.custKey + ' ' + r.custName"><span class="pdcm-mono">{{ r.custKey }}</span>{{ r.custName }}</span>
          </div>
          <div class="cll-pv-acts">
            @if (confirm(); as k) {
              <input pInputText [(ngModel)]="reason" maxlength="500" [placeholder]="k === 'void' ? 'Reason for voiding' : 'What needs correcting'" class="cll-reason" />
              <button type="button" class="pdcm-btn sm" (click)="confirm.set(null)">Cancel</button>
              <button type="button" class="pdcm-btn sm danger" [disabled]="busy() || !reason.trim()" (click)="doConfirmed(k)">{{ k === 'void' ? 'Yes, void' : 'Return' }}</button>
            } @else {
              @for (a of actions(); track a.label) {
                <button type="button" class="pdcm-btn sm" [class.go]="a.kind === 'primary'" [class.danger-outline]="a.kind === 'danger'"
                  [disabled]="busy() || a.disabled" [pTooltip]="a.hint" tooltipPosition="bottom" (click)="a.run()">{{ a.label }}</button>
              }
            }
          </div>
          <span class="cll-vsep"></span>
          <div class="cll-pv-nav">
            <button type="button" class="cll-sq" (click)="openFull(r.id)" title="Open full page" aria-label="Open full page">↗</button>
            @if (pos() >= 0) { <span class="pdcm-mono pdcm-sub">{{ pos() + 1 }} of {{ list().length }}</span> }
            <button type="button" class="cll-sq" (click)="move(-1)" title="Previous (↑)" aria-label="Previous record">↑</button>
            <button type="button" class="cll-sq" (click)="move(1)" title="Next (↓)" aria-label="Next record">↓</button>
            <span class="cll-vsep"></span>
            <button type="button" class="cll-sq x" (click)="close()" title="Close (Esc)" aria-label="Close preview">×</button>
          </div>
        </div>

        @if (detail(); as d) {
          <div class="cll-pv-body">
            <div class="cll-summary">
              <div><small>Amount received</small><b class="pdcm-mono">₱{{ d.amount | number:'1.2-2' }}</b></div>
              <div><small>Unapplied</small><b class="pdcm-mono" [class.cll-un]="d.unappliedAmount > 0">{{ d.unappliedAmount > 0 ? '₱' + (d.unappliedAmount | number:'1.2-2') : '—' }}</b></div>
              <div><small>Receipt date</small><span class="pdcm-mono">{{ d.receiptDate | date:'MM/dd/yyyy' }}</span></div>
              <div><small>Payment mode</small><span>{{ modeLong(d.paymentMode) }}</span></div>
            </div>

            @if (notes(d); as n) {
              @if (n.text) { <div class="cll-note" [class.bad]="n.bad"><i>!</i><span>{{ n.text }}</span></div> }
            }

            <div class="cll-card-box">
              <div class="cll-box-head"><b>Progress</b><small>{{ progressText(d) }}</small></div>
              <div class="cll-steps-wrap">
                <div class="cll-steps">
                  @for (s of steps(d); track $index; let i = $index) {
                    <div class="cll-step" [class.done]="s.state === 'done'" [class.bad]="s.state === 'bad'" [class.cur]="s.current" [pTooltip]="s.tip ?? undefined">
                      @if (i > 0) { <span class="cll-line" [class.on]="s.linked"></span> }
                      <span class="cll-dot">{{ s.state === 'done' ? '✓' : s.state === 'bad' ? '✕' : i + 1 }}</span>
                      <span class="cll-step-lbl">{{ s.label }}</span>
                      <span class="cll-step-meta">
                        @if (s.at) { {{ s.at | date:'MM/dd/yy' }}@if (!s.dateOnly) {<br>{{ s.at | date:'hh:mm a' }}} }
                        @if (s.by) {<br>{{ s.by }}}
                      </span>
                    </div>
                  }
                </div>
              </div>
            </div>

            <div class="cll-card-box">
              <div class="cll-box-head"><b>Applied to invoices</b><small>{{ d.invoices.length }} invoice{{ d.invoices.length === 1 ? '' : 's' }}</small></div>
              @if (d.invoices.length) {
                <div class="cll-inv-wrap">
                  <!-- Fixed layout: the money columns share one width so they line up evenly. -->
                  <table class="cll-inv cll-inv-fixed">
                    <colgroup>
                      <col style="width: 11%" /><col style="width: 10%" /><col style="width: 9%" />
                      <col style="width: 11%" /><col style="width: 11%" /><col style="width: 11%" /><col style="width: 11%" />
                      <col /><col style="width: 11%" />
                    </colgroup>
                    <thead>
                      <tr><th>Invoice</th><th>Date</th><th>Customer</th><th class="num">Total</th><th class="num">RFC</th>
                        <th class="num">Open balance</th><th class="num">Applied</th><th class="num">Deductions</th>
                        <th class="num" [pTooltip]="balanceHint(d)">{{ balanceLabel(d) }}</th></tr>
                    </thead>
                    <tbody class="pdcm-mono">
                      @for (i of d.invoices; track i.postedInvoiceId) {
                        <tr>
                          <td><button type="button" class="cll-link" (click)="openInvoice(i.postedInvoiceId)" pTooltip="Payment history of this invoice">{{ i.docNo }}</button></td>
                          <td>{{ i.docDate | date:'MM/dd/yyyy' }}</td>
                          <td class="cll-inv-cust" [pTooltip]="i.custName">{{ i.custKey }}</td>
                          <td class="num">{{ i.total | number:'1.2-2' }}</td>
                          <td class="num" [class.cll-neg]="i.rfcTotal < 0">{{ i.rfcTotal | number:'1.2-2' }}</td>
                          <td class="num">{{ i.availableBalance | number:'1.2-2' }}</td>
                          <td class="num cll-strong">{{ i.amountApplied | number:'1.2-2' }}</td>
                          <td class="cll-inv-ded num">
                            @for (x of dedsOf(d, i.postedInvoiceId); track x.id) {
                              <div>{{ dedLabel(x.type) }} {{ x.amount | number:'1.2-2' }}@if (x.reference) { <small> · {{ x.reference }}</small> }</div>
                            } @empty { <span class="pdcm-sub">—</span> }
                          </td>
                          <td class="num cll-strong" [class.cll-un]="i.balance > 0">{{ i.balance | number:'1.2-2' }}</td>
                        </tr>
                      }
                    </tbody>
                    <tfoot class="pdcm-mono">
                      <tr><td colspan="6">Total</td><td class="num cll-strong">{{ applied(d) | number:'1.2-2' }}</td>
                        <td class="num">{{ deductions(d) ? (deductions(d) | number:'1.2-2') : '—' }}</td>
                        <td class="num cll-strong">{{ balanceTotal(d) | number:'1.2-2' }}</td></tr>
                    </tfoot>
                  </table>
                </div>
              } @else {
                <div class="cll-box-body"><div class="cll-nofile">No invoice applied — the whole amount is kept on-account.</div></div>
              }
            </div>

            <div class="cll-grid">
              <div class="cll-card-box cll-wide">
                <div class="cll-box-head"><b>Receipt details</b></div>
                <div class="cll-box-body">
                  <div class="cll-fields">
                    <div><small>Receipt type</small>{{ receiptTypeLabel(d.receiptType) }}</div>
                    <div><small>Receipt no.</small><span class="pdcm-mono">{{ d.receiptNo }}</span></div>
                    <div><small>Receipt date</small><span class="pdcm-mono">{{ d.receiptDate | date:'MM/dd/yyyy' }}</span></div>
                    <div><small>Payment mode</small>{{ modeLong(d.paymentMode) }}</div>
                    <div><small>Branch</small>{{ d.siteName }}</div>
                    <div><small>Encoded by</small>{{ d.createdBy }}</div>
                    @if (d.remarks) { <div><small>Remarks</small>{{ d.remarks }}</div> }
                  </div>
                  @if (d.checkNo || d.paymentMode === 'PostDatedCheck') {
                    <div class="cll-sec"><span>Check</span><i></i></div>
                    <div class="cll-fields">
                      <div><small>Bank (drawee)</small>{{ d.bank || '—' }}</div>
                      <div><small>Check no.</small><span class="pdcm-mono">{{ d.checkNo || '—' }}</span></div>
                      <div><small>Check date</small><span class="pdcm-mono">{{ (d.checkDate | date:'MM/dd/yyyy') || '—' }}</span></div>
                      <div><small>Amount received</small><span class="pdcm-mono">{{ d.amount | number:'1.2-2' }}</span></div>
                    </div>
                  }
                  <div class="cll-sec"><span>Deposit</span><i></i></div>
                  <div class="cll-fields">
                    <div><small>Deposited into</small>{{ d.deposit?.bank || '—' }}</div>
                    <div><small>Slip / transfer ref. no.</small><span class="pdcm-mono">{{ d.deposit?.slipRefNo || '—' }}</span></div>
                    <div><small>Deposit date</small><span class="pdcm-mono">{{ (d.deposit?.depositDate | date:'MM/dd/yyyy') || '—' }}</span></div>
                    <div><small>Bank statement</small>{{ d.bankLine ? (d.bankLine.confirmed ? 'Matched' : 'Match found — awaiting approval') : 'Not yet matched' }}
                      @if (d.bankLine) { <span class="pdcm-sub">{{ d.bankLine.account }} · {{ d.bankLine.txnDate | date:'MM/dd/yyyy' }}</span> }</div>
                  </div>
                </div>
              </div>

              <div class="cll-side">
                <div class="cll-card-box">
                  <div class="cll-box-head"><b>Allocation</b></div>
                  <div class="cll-box-body cll-alloc">
                    <div><span>Received</span><span class="pdcm-mono">{{ d.amount | number:'1.2-2' }}</span></div>
                    <div><span>Applied ({{ d.invoices.length }} invoice{{ d.invoices.length === 1 ? '' : 's' }})</span><span class="pdcm-mono">{{ applied(d) | number:'1.2-2' }}</span></div>
                    <div><span>Deductions</span><span class="pdcm-mono">{{ deductions(d) | number:'1.2-2' }}</span></div>
                    <div class="tot"><span>Unapplied (on-account)</span><span class="pdcm-mono" [class.cll-un]="d.unappliedAmount > 0">{{ d.unappliedAmount | number:'1.2-2' }}</span></div>
                  </div>
                </div>
                <div class="cll-card-box">
                  <div class="cll-box-head"><b>Receipt soft copy</b><small>PDF, JPG, PNG · 10 MB</small></div>
                  <div class="cll-box-body cll-files">
                    @for (a of d.attachments; track a.id) {
                      <button type="button" class="cll-file" (click)="viewFile(d.id, a.id)">
                        <span class="cll-ext pdcm-mono">{{ ext(a.fileName) }}</span>
                        <span><b>{{ a.fileName }}</b><small>{{ a.size / 1024 | number:'1.0-0' }} KB · {{ a.uploadedBy }}</small></span>
                      </button>
                    } @empty { <div class="cll-nofile">No file attached yet</div> }
                  </div>
                </div>
              </div>
            </div>
          </div>
        } @else {
          <div class="pdcm-empty">Loading…</div>
        }
      } @else {
        <div class="cll-pv-empty">
          <div class="cll-doc"><span></span><span></span><span></span></div>
          <b>Select a collection to preview</b>
          <small>Use ↑ ↓ to move between records</small>
        </div>
      }
    </ng-template>

    @if (invOpen()) {
      <div class="cll-overlay cll-ih-overlay" (click)="closeInvoice()"></div>
      <aside class="cll-ih">
        <div class="cll-ih-head">
          <div>
            <div class="pdcm-eyebrow">Invoice payment history</div>
            @if (inv(); as h) {
              <b>Invoice {{ h.docNo }} <small class="pdcm-mono">{{ h.siteCode }}</small></b>
              <span class="pdcm-sub"><span class="pdcm-mono">{{ h.custKey }}</span> {{ h.custName }} · {{ h.docDate | date:'MM/dd/yyyy' }}@if (h.poNum) { · PO {{ h.poNum }} }@if (h.term) { · {{ h.term }} days }</span>
            }
          </div>
          <button type="button" class="cll-sq x" (click)="closeInvoice()" aria-label="Close" title="Close (Esc)">×</button>
        </div>
        @if (inv(); as h) {
          <div class="cll-ih-body">
            <div class="cll-summary">
              <div><small>Invoice total</small><b class="pdcm-mono">{{ h.total | number:'1.2-2' }}</b></div>
              <div><small>RFC returns</small><b class="pdcm-mono" [class.cll-neg]="h.rfcTotal < 0">{{ h.rfcTotal | number:'1.2-2' }}</b></div>
              <div><small>Paid (HOMSys)</small><b class="pdcm-mono">{{ h.paid | number:'1.2-2' }}</b></div>
              <div><small>{{ invBalanceLabel() }}</small><b class="pdcm-mono" [class.cll-un]="h.openBalance > 0">{{ h.openBalance | number:'1.2-2' }}</b></div>
            </div>
            <div class="cll-card-box">
              <div class="cll-box-head"><b>Ledger</b><small>oldest first</small></div>
              <div class="cll-inv-wrap">
                <table class="cll-inv cll-ledger">
                  <thead><tr><th>Date</th><th>Entry</th><th class="num">Charge</th><th class="num">Payment</th><th class="num">Balance</th></tr></thead>
                  <tbody>
                    @for (e of ledger(); track $index) {
                      <tr [class.off]="!e.counts" [class.mine]="e.mine" [class.click]="!!e.collectionId && !e.mine"
                        (click)="e.collectionId && !e.mine && goToCollection(e.collectionId)">
                        <td class="pdcm-mono">{{ e.date ? (e.date | date:'MM/dd/yyyy') : '—' }}</td>
                        <td class="cll-ledger-entry">
                          <div>{{ e.label }}@if (e.mine) { <span class="cll-this">this receipt</span> }</div>
                          @if (e.sub) { <small>{{ e.sub }}</small> }
                          @for (x of e.deds; track $index) { <small>incl. {{ dedLabel(x.type) }} {{ x.amount | number:'1.2-2' }}@if (x.reference) { · {{ x.reference }} }</small> }
                          @if (!e.counts) { <small class="cll-neg">Not counted — {{ e.why }}</small> }
                        </td>
                        <td class="num pdcm-mono">{{ e.charge ? (e.charge | number:'1.2-2') : '' }}</td>
                        <td class="num pdcm-mono">{{ e.credit ? (e.credit | number:'1.2-2') : '' }}</td>
                        <td class="num pdcm-mono cll-strong">{{ e.balance | number:'1.2-2' }}</td>
                      </tr>
                    }
                  </tbody>
                  <tfoot class="pdcm-mono">
                    <tr><td colspan="4" [pTooltip]="invBalanceHint()">{{ invBalanceLabel() }}</td><td class="num" [class.cll-un]="h.openBalance > 0">{{ h.openBalance | number:'1.2-2' }}</td></tr>
                  </tfoot>
                </table>
              </div>
            </div>
            <p class="pdcm-sub cll-ih-foot">Only collections recorded in HOMSys are listed; payments posted directly in BMS are not.
              Every receipt counts once it is saved, verified or not, except voided receipts and bounced PDCs.</p>
          </div>
        } @else {
          <div class="pdcm-empty">{{ invError() || 'Loading…' }}</div>
        }
      </aside>
    }

    @if (doneToast(); as t) {
      <div class="pdcm-toast"><span>{{ t }}</span><button type="button" class="pdcm-toast-x" (click)="doneToast.set(null)" aria-label="Dismiss">×</button></div>
    }
  `,
})
export class CollectionListPageComponent implements OnInit, AfterViewInit, OnDestroy {
  private api = inject(CollectionService);
  private router = inject(Router);
  private toolbar = inject(GlobalToolbarService);
  protected exportSvc = inject(ExportService);
  private auth = inject(AuthService);
  private toast = inject(MessageService);

  private initialSearch = (history.state as Record<string, unknown>)?.['searchTerm'] as string ?? '';

  protected readonly label = statusLabel;
  protected readonly cols = EXPORT_COLS;
  protected readonly payOf = payOf;
  protected readonly mode = modeOf;
  protected readonly min = Math.min;
  protected readonly sizeOpts = [10, 25, 50, 100].map(n => ({ label: `${n} / page`, value: n }));
  protected readonly canEncode = this.auth.hasPermission('collections-encode') || this.auth.isAdmin();

  all = signal<CollectionListItemDto[]>([]);
  sites = signal<CollectionSiteDto[]>([]);
  loading = signal(true);
  busy = signal(false);

  // ── filters ──
  view = signal<'table' | 'split'>(localStorage.getItem(VIEW_KEY) === 'split' ? 'split' : 'table');
  tab = signal<string>('all');
  q = signal(this.initialSearch);
  branch = signal<string | null>(null);
  /** Payment-mode filter (mode() formats a mode for display). */
  modeF = signal<string | null>(null);
  pay = signal<string | null>(null);
  from = signal<Date | null>(null);
  to = signal<Date | null>(null);
  attnOnly = signal(false);
  sort = signal<'date' | 'amount'>('date');
  asc = signal(false);
  pageSize = signal(25);
  private pageNo = signal(1);

  private base = computed(() => {
    const t = this.q().toLowerCase().trim(), br = this.branch(), md = this.modeF(), py = this.pay(), at = this.attnOnly();
    const [f, to] = [isoDay(this.from()), isoDay(this.to())];
    return this.all().filter(c => (!br || c.siteCode === br) && (!md || c.paymentMode === md) && (!py || payOf(c) === py)
      && inDateRange(c.receiptDate, f, to) && (!at || attention(c))
      && (!t || [c.receiptType + ' ' + c.receiptNo, c.custKey, c.custName, c.depositSlipRef ?? '', c.siteCode].some(x => x.toLowerCase().includes(t))));
  });

  tabs = computed(() => {
    const b = this.base();
    return [{ key: 'all', label: 'All collections', color: '#BDB7B0', count: b.length },
      ...Object.keys(DOC).map(k => ({ key: k, label: statusLabel(k), color: DOC[k], count: b.filter(c => c.verificationStatus === k).length }))];
  });

  list = computed(() => {
    const tb = this.tab(), dir = this.asc() ? 1 : -1, byDate = this.sort() === 'date';
    return this.base().filter(c => tb === 'all' || c.verificationStatus === tb)
      .sort((a, b) => dir * (byDate ? a.receiptDate.localeCompare(b.receiptDate) || a.id - b.id : a.amount - b.amount));
  });
  total = computed(() => this.list().reduce((s, c) => s + c.amount, 0));
  unTotal = computed(() => this.list().reduce((s, c) => s + c.unappliedAmount, 0));

  hasFilters = computed(() => !!(this.q().trim() || this.branch() || this.modeF() || this.pay() || this.from() || this.to() || this.attnOnly()));

  branchOpts = computed(() => [...new Set([...this.sites().map(s => s.code), ...this.all().map(c => c.siteCode)])].sort());
  modeOpts = computed(() => [...new Set(this.all().map(c => c.paymentMode))].sort().map(v => ({ value: v, label: modeOf(v) })));
  payOpts = computed(() => [...new Set(this.all().map(payOf))].sort());

  // ── paging ──
  pageCount = computed(() => Math.max(1, Math.ceil(this.list().length / this.pageSize())));
  page = computed(() => Math.min(this.pageNo(), this.pageCount()));
  pageRows = computed(() => this.list().slice((this.page() - 1) * this.pageSize(), this.page() * this.pageSize()));
  pageNums = computed(() => {
    const n = this.pageCount(), p = this.page();
    let a = Math.max(1, p - 2);
    const b = Math.min(n, a + 4);
    a = Math.max(1, b - 4);
    return Array.from({ length: b - a + 1 }, (_, i) => a + i);
  });
  goto(n: number) { this.pageNo.set(Math.max(1, Math.min(this.pageCount(), n))); }
  resetPage() { this.pageNo.set(1); }

  /** Reading pane: the page's rows grouped by how recent the receipt date is. */
  groups = computed(() => {
    const now = new Date(), today = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
    const out: { label: string; items: CollectionListItemDto[] }[] = [];
    for (const c of this.pageRows()) {
      const d = Date.parse(c.receiptDate), n = Math.round((today - d) / 864e5), dt = new Date(d);
      const g = n <= 0 ? 'Today' : n === 1 ? 'Yesterday' : n <= 7 ? 'Earlier this week'
        : dt.getUTCMonth() === now.getMonth() && dt.getUTCFullYear() === now.getFullYear() ? 'Earlier this month' : 'Older';
      let grp = out.find(x => x.label === g);
      if (!grp) out.push(grp = { label: g, items: [] });
      grp.items.push(c);
    }
    return out;
  });

  sortBy(k: 'date' | 'amount') { this.asc.set(this.sort() === k ? !this.asc() : false); this.sort.set(k); }
  setView(v: 'table' | 'split') { localStorage.setItem(VIEW_KEY, v); this.view.set(v); setTimeout(() => this.fit()); }

  /** Reading pane: the panel fills what's left of the page's scroll area (below its own top, above the shell's
   *  bottom padding), so the page itself never scrolls; the list and the preview scroll inside it. */
  @ViewChild('sec') private sec?: ElementRef<HTMLElement>;
  secH = signal(600);
  @HostListener('window:resize')
  fit() {
    const el = this.sec?.nativeElement, box = el?.closest('.layout-content') as HTMLElement | null;
    if (!el || !box || this.view() !== 'split') return;
    const top = el.getBoundingClientRect().top - box.getBoundingClientRect().top + box.scrollTop;
    const padBottom = parseFloat(getComputedStyle(box).paddingBottom) || 0;
    this.secH.set(Math.max(420, Math.floor(box.clientHeight - top - padBottom)));
  }
  ngAfterViewInit() { setTimeout(() => this.fit()); }
  clearFilters() {
    this.q.set(''); this.branch.set(null); this.modeF.set(null); this.pay.set(null); this.from.set(null); this.to.set(null); this.attnOnly.set(false);
    this.resetPage();
  }

  docColor(s: string) { return DOC[s] ?? C.slate; }
  payColor(s: string) { return PAY[s] ?? C.slate; }
  homsText(c: CollectionListItemDto) { return c.homs ? (c.homs === 'Posted' ? '✓ Posted' : c.homs) : c.postedInBms ? 'In HOMS' : 'Not posted'; }
  homsColor(c: CollectionListItemDto) { return c.homs ? HOMS[c.homs] : c.postedInBms ? C.green : '#6B6661'; }
  modeLong(m: string) { return PAYMENT_MODES.find(x => x.value === m)?.label ?? modeOf(m); }
  receiptTypeLabel(t: string) { return RECEIPT_TYPES.find(x => x.value === t)?.label ?? t; }
  ext(name: string) { return (name.split('.').pop() ?? '').toUpperCase(); }

  ngOnInit() {
    this.api.mySites().subscribe(res => this.sites.set(res.data ?? []));
    this.load();
    this.toolbar.set({
      title: 'Collections',
      add: this.canEncode ? { onClick: () => this.newCollection() } : undefined,
      refresh: { onClick: () => this.load(), loading: this.loading },
      views: {
        options: [
          { label: 'Table', value: 'table', icon: 'pi pi-table' },
          { label: 'Reading pane', value: 'split', icon: 'pi pi-objects-column' },
        ],
        value: this.view,
        onChange: v => this.setView(v === 'split' ? 'split' : 'table'),
      },
      export: {
        pdf: () => this.exportSvc.exportPdf('Collections', EXPORT_COLS, this.exportRows()),
        excel: () => this.exportSvc.exportExcel('Collections', EXPORT_COLS, this.exportRows()),
        csv: () => this.exportSvc.exportCsv('Collections', EXPORT_COLS, this.exportRows()),
      },
    });
  }

  ngOnDestroy() { this.toolbar.clear(); clearTimeout(this.toastTimer); }

  load() {
    this.loading.set(true);
    this.api.list(null, null).subscribe({
      next: res => { this.all.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  newCollection() { this.router.navigate(['/collections']); }
  openFull(id: number) { this.router.navigate(['/collections'], { state: { collectionId: id } }); }
  protected exportRows() { return this.list() as unknown as Record<string, unknown>[]; }

  // ── preview ──

  selId = signal<number | null>(null);
  selRow = computed(() => this.all().find(c => c.id === this.selId()) ?? null);
  pos = computed(() => this.list().findIndex(c => c.id === this.selId()));
  detail = signal<CollectionDto | null>(null);
  private cache = new Map<number, CollectionDto>();
  confirm = signal<'void' | 'return' | null>(null);
  reason = '';
  doneToast = signal<string | null>(null);
  private toastTimer?: ReturnType<typeof setTimeout>;

  select(id: number) {
    if (this.selId() === id) return;
    this.selId.set(id);
    this.confirm.set(null);
    const hit = this.cache.get(id);
    this.detail.set(hit ?? null);
    if (!hit) this.api.get(id).subscribe(res => {
      if (!res.data) return;
      this.cache.set(id, res.data);
      if (this.selId() === id) this.detail.set(res.data);
    });
  }

  close() { this.selId.set(null); this.confirm.set(null); }

  move(d: number) {
    const L = this.list();
    if (!L.length) return;
    const i = this.pos(), n = i < 0 ? 0 : Math.max(0, Math.min(L.length - 1, i + d));
    this.select(L[n].id);
    this.pageNo.set(Math.floor(n / this.pageSize()) + 1);
  }

  @HostListener('window:keydown', ['$event'])
  onKey(e: KeyboardEvent) {
    const tag = (e.target as HTMLElement)?.tagName?.toLowerCase();
    if (['input', 'select', 'textarea'].includes(tag) || (e.target as HTMLElement)?.isContentEditable) return;
    if (this.invOpen()) { if (e.key === 'Escape') this.closeInvoice(); return; }
    if (e.key === 'Escape' && this.selId()) { this.close(); return; }
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      if (this.view() !== 'split' && !this.selId()) return;
      e.preventDefault();
      this.move(e.key === 'ArrowDown' ? 1 : -1);
    }
  }

  steps(d: CollectionDto) { return collectionSteps(d); }
  applied(d: CollectionDto) { return d.invoices.reduce((s, i) => s + i.amountApplied, 0); }
  // ── invoice payment history (side panel over the preview) ──

  invOpen = signal(false);
  inv = signal<InvoiceHistoryDto | null>(null);
  invError = signal<string | null>(null);

  openInvoice(postedInvoiceId: number) {
    this.invOpen.set(true);
    this.inv.set(null);
    this.invError.set(null);
    this.api.invoiceHistory(postedInvoiceId).subscribe({
      next: res => this.inv.set(res.data ?? null),
      error: err => this.invError.set(err?.error?.message ?? 'Could not load the invoice history.'),
    });
  }

  closeInvoice() { this.invOpen.set(false); }

  /** Another receipt in the history: preview it if it's in the list, else open its page. */
  goToCollection(id: number) {
    this.closeInvoice();
    if (this.all().some(c => c.id === id)) this.select(id); else this.openFull(id);
  }

  /** Running balance, oldest first: the invoice, its RFC returns, then each payment (applied + deductions). Voided
   *  receipts and bounced PDCs are listed but don't move the balance — the same rule as the server's open balance. */
  ledger = computed(() => {
    const h = this.inv();
    if (!h) return [];
    const mineId = this.detail()?.id;
    type E = { date: string | null; label: string; sub?: string; charge?: number; credit?: number; balance: number; counts: boolean;
      why?: string | null; collectionId?: number; mine?: boolean; deds: { type: string; amount: number; reference: string | null }[] };
    let bal = h.total;
    const out: E[] = [{ date: h.docDate, label: `Invoice ${h.docNo}`, sub: h.poNum ? `PO ${h.poNum}` : undefined, charge: h.total, balance: bal, counts: true, deds: [] }];
    for (const r of h.rfcs) { bal += r; out.push({ date: null, label: 'RFC return', credit: -r, balance: bal, counts: true, deds: [] }); }
    for (const p of h.payments) {
      const amt = p.applied + p.deductions;
      if (p.counts) bal -= amt;
      out.push({
        date: p.receiptDate, label: `${p.receiptType} ${p.receiptNo}`.trim(), collectionId: p.collectionId, mine: p.collectionId === mineId,
        sub: [modeOf(p.paymentMode), p.checkNo ? `#${p.checkNo}` : '', p.pdcState ? `PDC ${p.pdcState}` : '', statusLabel(p.verificationStatus),
              p.counts && p.paymentStatus !== 'Deposited' ? 'not yet deposited' : '', p.siteCode]
          .filter(Boolean).join(' · '),
        credit: amt, balance: bal, counts: p.counts, why: p.notCountedReason, deds: p.deductions ? p.deductionLines : [],
      });
    }
    return out;
  });

  /** "Current Balance" once this receipt is Deposited (its money is on the bank statement), else "Balance To Be". */
  balanceLabel(d: CollectionDto) { return d.paymentStatus === 'Deposited' ? 'Current Balance' : 'Balance To Be'; }
  balanceHint(d: CollectionDto) {
    return d.paymentStatus === 'Deposited'
      ? 'Invoice balance after this receipt — its deposit is on the bank statement'
      : 'Invoice balance once this receipt goes through — not yet deposited';
  }
  balanceTotal(d: CollectionDto) { return d.invoices.reduce((s, i) => s + i.balance, 0); }

  /** History panel: "Current balance" only when every payment counted on the invoice is Deposited. */
  private invAllDeposited = computed(() => {
    const ps = this.inv()?.payments.filter(p => p.counts) ?? [];
    return ps.every(p => p.paymentStatus === 'Deposited');
  });
  invBalanceLabel = computed(() => this.invAllDeposited() ? 'Current balance' : 'Open balance to be');
  invBalanceHint = computed(() => this.invAllDeposited()
    ? 'Every payment counted here is deposited (on the bank statement)'
    : 'Some payments counted here are not deposited yet');

  dedsOf(d: CollectionDto, invoiceId: number) { return d.deductions.filter(x => x.postedInvoiceId === invoiceId); }
  dedLabel(t: string) { return DEDUCTION_TYPES.find(x => x.value === t)?.label ?? t; }
  deductions(d: CollectionDto) { return d.deductions.reduce((s, x) => s + x.amount, 0); }

  progressText(d: CollectionDto) {
    const s = this.steps(d), bad = s.find(x => x.state === 'bad');
    if (d.verificationStatus === 'Voided') return 'Voided';
    if (bad) return `Stopped at ${bad.label}`;
    const next = s.find(x => x.state === 'todo');
    return next ? `Next: ${next.label}` : `${s.filter(x => x.state === 'done').length} of ${s.length} complete`;
  }

  notes(d: CollectionDto): { text: string; bad: boolean } {
    const n: string[] = [];
    if (d.verificationStatus === 'Voided') n.push(`This receipt is void${d.voidReason ? ': ' + d.voidReason : ''}.`);
    if (d.verificationStatus === 'Returned' && d.returnReason) n.push(`Returned by finance: ${d.returnReason}`);
    if (d.pdcOutcome === 'Bounced') n.push(`Check bounced on ${d.pdcOutcomeDate ?? '—'}${d.pdcOutcomeReason ? ' (' + d.pdcOutcomeReason + ')' : ''}. Follow up through the PDC Monitor.`);
    if (d.missing.length) n.push('Still a draft — ' + d.missing.join(' '));
    if (d.bankLine && !d.bankLine.confirmed) n.push('Deposit found on the bank statement — awaiting approval.');
    if (d.unappliedAmount > 0 && d.verificationStatus !== 'Voided')
      n.push(`₱${d.unappliedAmount.toLocaleString('en-PH', { minimumFractionDigits: 2 })} is kept on-account and not yet applied to invoices.`);
    return { text: n.join(' '), bad: d.verificationStatus === 'Voided' || d.verificationStatus === 'Returned' || d.pdcOutcome === 'Bounced' };
  }

  /** The collection page's own actions, offered only where the server says this user may take them. */
  actions = computed<Action[]>(() => {
    const d = this.detail();
    if (!d || d.id !== this.selId()) return [];
    const a: Action[] = [];
    if (d.canVoid) a.push({ label: 'Void', kind: 'danger', run: () => { this.reason = ''; this.confirm.set('void'); } });
    if (d.canVerify) a.push({ label: 'Return for correction', kind: 'secondary', run: () => { this.reason = ''; this.confirm.set('return'); } });
    if (d.canConfirmMatch) a.push({ label: 'Reject match', kind: 'secondary', run: () => this.run(this.api.rejectMatch(d.id, null), 'Bank match rejected — that bank line is kept out of automatic matching.') });
    if (d.canEdit) a.push({ label: 'Edit', kind: 'secondary', run: () => this.openFull(d.id) });
    if (d.paymentMode === 'PostDatedCheck') a.push({ label: 'View in PDC Monitor', kind: 'secondary', run: () => this.router.navigate(['/pdc']) });
    if (d.canSubmit) a.push({ label: 'Submit for verification', kind: 'primary', disabled: d.missing.length > 0, hint: d.missing.join(' ') || undefined,
      run: () => this.run(this.api.submit(d.id), `${d.receiptType} ${d.receiptNo} submitted for verification.`) });
    else if (d.canVerify) a.push({ label: 'Mark as verified', kind: 'primary',
      run: () => this.run(this.api.verify(d.id), `${d.receiptType} ${d.receiptNo} verified — queued for HOMS posting.`) });
    else if (d.canConfirmMatch) a.push({ label: 'Approve bank match', kind: 'primary',
      run: () => this.run(this.api.confirmMatch(d.id), 'Bank match approved — the collection is Deposited' + (d.paymentMode === 'PostDatedCheck' ? '; its JV is queued for HOMS.' : '.')) });
    return a;
  });

  doConfirmed(k: 'void' | 'return') {
    const d = this.detail(), r = this.reason.trim();
    if (!d || !r) return;
    this.run(k === 'void' ? this.api.void(d.id, r) : this.api.returnToBranch(d.id, r),
      k === 'void' ? `${d.receiptType} ${d.receiptNo} voided.` : `${d.receiptType} ${d.receiptNo} returned to the branch for correction.`);
  }

  private run(call: Observable<ApiResponse<CollectionDto>>, done: string) {
    this.busy.set(true);
    call.subscribe({
      next: res => {
        this.busy.set(false);
        this.confirm.set(null);
        if (res.data) { this.cache.set(res.data.id, res.data); if (this.selId() === res.data.id) this.detail.set(res.data); }
        clearTimeout(this.toastTimer);
        this.doneToast.set(done);
        this.toastTimer = setTimeout(() => this.doneToast.set(null), 6000);
        this.load();
      },
      error: err => { this.busy.set(false); this.toast.add({ severity: 'error', summary: 'Collections', detail: err?.error?.message ?? 'Please try again.' }); }
    });
  }

  viewFile(id: number, attachmentId: number) {
    this.api.attachment(id, attachmentId).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    });
  }
}
