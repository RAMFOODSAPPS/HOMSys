import { Component, computed, HostBinding, HostListener, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { AuthService } from '../../core/services/auth.service';
import { TagModule } from 'primeng/tag';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { TextareaModule } from 'primeng/textarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { DatePickerModule } from 'primeng/datepicker';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { ExportService, ExportColumn } from '../../core/services/export.service';
import { CollectionService } from '../../core/services/collection.service';
import { Observable } from 'rxjs';
import { BankAccountOptionDto, PdcDishonorDto, PdcItemDto, SettlementCandidateDto, statusLabel, statusSeverity } from '../../core/models/collection.model';
import { printLodLetter } from './lod-letter';
import { CollectionStep } from '../collections/collection-steps';

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Branch', field: 'siteCode' },
  { header: 'Type', field: 'receiptType' },
  { header: 'Receipt No.', field: 'receiptNo' },
  { header: 'Customer', field: 'custKey' },
  { header: 'Customer Name', field: 'custName' },
  { header: 'Drawee Bank', field: 'bank' },
  { header: 'Check No.', field: 'checkNo' },
  { header: 'Check Date', field: 'checkDate' },
  { header: 'Days to Maturity', field: 'daysToMaturity' },
  { header: 'Amount', field: 'amount' },
  { header: 'Status', field: 'status' },
  { header: 'Next Step', field: 'nextStep' },
  { header: 'Deposit Slip', field: 'depositSlipRef' },
  { header: 'Deposit Date', field: 'depositDate' },
  { header: 'Outcome Date', field: 'outcomeDate' },
  { header: 'Outcome Reason', field: 'outcomeReason' },
  { header: 'Tagged By', field: 'outcomeBy' },
  { header: 'Settle By', field: 'settleBy' },
];

const C = { maroon: '#860000', amber: '#B7791F', violet: '#6D3FD1', blue: '#2563C9', slate: '#5B6676', green: '#1E8A4C', red: '#C8261B', dred: '#8F1D14' };

type StageKey = 'onhand' | 'deposit' | 'cleared' | 'exceptions';
type StatusKey = 'for_claim' | 'upcoming' | 'later' | 'cnc' | 'dep_recorded' | 'dep_tag' | 'succeeded' | 'settled'
  | 'bounced' | 'lod_issue' | 'lod_out' | 'overdue_legal' | 'endorsed_legal';
interface StatusMeta { label: string; stage: StageKey; color: string }

const ST: Record<StatusKey, StatusMeta> = {
  for_claim: { label: 'For claim (matured)', stage: 'onhand', color: C.maroon },
  upcoming: { label: 'Upcoming', stage: 'onhand', color: C.amber },
  later: { label: 'Not yet due', stage: 'onhand', color: C.slate },
  cnc: { label: 'To confirm (CNC)', stage: 'deposit', color: C.violet },
  dep_recorded: { label: 'Deposit recorded, awaiting match', stage: 'deposit', color: C.blue },
  dep_tag: { label: 'Deposited, awaiting tag', stage: 'deposit', color: C.slate },
  succeeded: { label: 'Succeeded', stage: 'cleared', color: C.green },
  settled: { label: 'Bounced — settled by new CR', stage: 'cleared', color: C.green },
  bounced: { label: 'Bounced', stage: 'exceptions', color: C.red },
  lod_issue: { label: 'Bounced — LOD to issue', stage: 'exceptions', color: C.red },
  lod_out: { label: 'LOD out, awaiting settlement', stage: 'exceptions', color: C.amber },
  overdue_legal: { label: 'Overdue — for legal action', stage: 'exceptions', color: C.dred },
  endorsed_legal: { label: 'Endorsed to legal', stage: 'exceptions', color: C.slate },
};

const STAGES: { key: StageKey; step: string; label: string; desc: string; statuses: StatusKey[]; color: string }[] = [
  { key: 'onhand', step: '01', label: 'On hand', desc: 'Checks with the custodian, matured or maturing soon', statuses: ['for_claim', 'upcoming', 'later'], color: C.maroon },
  { key: 'deposit', step: '02', label: 'In deposit', desc: 'Deposited checks being confirmed, matched and tagged', statuses: ['cnc', 'dep_recorded', 'dep_tag'], color: C.maroon },
  { key: 'cleared', step: '03', label: 'Cleared', desc: 'Funds received and reconciled', statuses: ['succeeded', 'settled'], color: C.maroon },
  { key: 'exceptions', step: '!', label: 'Exceptions', desc: 'Bounced checks through LOD and legal escalation',
    statuses: ['bounced', 'lod_issue', 'lod_out', 'overdue_legal', 'endorsed_legal'], color: C.red },
];

const VIEW_KEY = 'homsys_pdc_view';

const DISHONOR: Record<string, StatusKey> = {
  'For LOD': 'lod_issue', 'LOD Issued': 'lod_out', 'Awaiting Settlement': 'lod_out',
  'Overdue': 'overdue_legal', 'For Legal Action': 'endorsed_legal', 'Settled': 'settled',
};

/** Where a PDC is in its life; Immature splits into Upcoming / Not yet due by the "upcoming within" window. */
function statusOf(p: PdcItemDto, win: number): StatusKey {
  if (p.dishonor) return DISHONOR[p.dishonor.state] ?? 'bounced';
  switch (p.state) {
    case 'Bounced': return 'bounced';
    case 'Succeeded': return 'succeeded';
    case 'Deposited': return 'dep_tag';
    case 'Matured': return !p.depositSlipRef ? 'for_claim' : p.bankLine && !p.bankLine.confirmed ? 'cnc' : 'dep_recorded';
    default: return p.daysToMaturity <= win ? 'upcoming' : 'later';
  }
}

const rel = (n: number) => n === 0 ? 'Today' : n > 0 ? `in ${n} day${n > 1 ? 's' : ''}` : `${-n} day${n < -1 ? 's' : ''} ago`;
const mdy = (iso: string | null | undefined) => iso ? `${iso.slice(5, 7)}/${iso.slice(8, 10)}/${iso.slice(0, 4)}` : '';

interface Act { label: string; run: () => void }
interface Row { p: PdcItemDto; st: StatusKey; meta: StatusMeta; action: Act | null; note: string; rel: string; late: boolean }

/**
 * PDC Monitor (design: claude.ai/design "PDC Monitor", 2026-10-09): four stages — On hand, In deposit, Cleared,
 * Exceptions — each split into statuses, with the next step this user can take on every check. v2 (2026-10-09): Table or
 * Reading-pane view with a preview of the check (summary, progress, details, history; endorsing an overdue bounced PDC
 * to legal). The preview only presents PdcItemDto — statuses, actions and permissions are unchanged.
 */
@Component({
  selector: 'app-pdc-page',
  standalone: true,
  providers: [MessageService],
  imports: [InputTextModule, TagModule, SelectModule, ButtonModule, DialogModule, TooltipModule, TextareaModule, InputNumberModule,
    DatePickerModule, ToggleSwitchModule, ToastModule, FormsModule, DatePipe, DecimalPipe, NgTemplateOutlet],
  template: `
    <p-toast position="top-right" />
    <div class="pdcm">
      @if (customer(); as ck) {
        <div class="pdcm-cust">
          <div class="pdcm-cust-name">
            <div class="pdcm-eyebrow">Customer</div>
            <div class="pdcm-cust-title">{{ custLabel(ck) }}</div>
            <div class="pdcm-sub">{{ custTotal().count }} PDC{{ custTotal().count === 1 ? '' : 's' }} · ₱{{ custTotal().amount | number:'1.2-2' }}</div>
          </div>
          <div class="pdcm-cust-stats">
            @for (c of custStats(); track c.key) {
              <div [style.border-left-color]="c.color">
                <span>{{ c.label }}</span>
                <b>{{ c.count }} <span class="pdcm-mono">₱{{ c.amount | number:'1.2-2' }}</span></b>
              </div>
            }
          </div>
          <button type="button" class="pdcm-btn" (click)="setCustomer(null)">Show all customers ×</button>
        </div>
      }

      <div class="pdcm-stagebar" role="tablist" aria-label="Stages">
        @for (st of stageCards(); track st.key) {
          <button type="button" role="tab" [class.on]="stage() === st.key" [style.--c]="st.color" [title]="st.desc" (click)="pickStage(st.key)">
            <i></i>
            <span class="pdcm-stage-txt">
              <span><b>{{ st.label }}</b><span class="pdcm-mono pdcm-stage-n">{{ st.count }}</span></span>
              <small class="pdcm-mono">₱{{ st.amount | number:'1.2-2' }}</small>
            </span>
            @if (st.actions) { <span class="pdcm-stage-act" [title]="st.actions + ' need action'">{{ st.actions }}</span> }
          </button>
        }
      </div>

      <section class="pdcm-panel cll-fit" [class.pdcm-panel-split]="view() === 'split'">
        <div class="pdcm-tabs">
          @for (t of statusTabs(); track t.key) {
            <button type="button" [class.on]="status() === t.key" (click)="pickStatus(t.key)">
              <i [style.background]="t.color"></i>{{ t.label }}
              <span class="pdcm-badge" [class.has]="t.count > 0" [style.--c]="t.key === 'all' ? '#1C1B1A' : t.color">{{ t.count }}</span>
            </button>
          }
          <div class="cll-filter-acts pdcm-tab-acts">
            <button type="button" class="pdcm-btn primary" (click)="jumpToday()">Go to today's deposits</button>
            <button type="button" class="pdcm-btn" (click)="exportList()">Export</button>
          </div>
        </div>

        <div class="pdcm-filters">
          <input pInputText class="pdcm-search" [ngModel]="q()" (ngModelChange)="q.set($event); resetPage()" placeholder="Search customer, receipt, check no." />
          <p-select [options]="customerOpts()" [ngModel]="customer()" (ngModelChange)="setCustomer($event)" optionLabel="label" optionValue="value"
            [filter]="true" [showClear]="true" placeholder="All customers" appendTo="body" styleClass="pdcm-sel-wide" />
          <p-select [options]="branchOpts()" [ngModel]="branch()" (ngModelChange)="branch.set($event); resetPage()" [showClear]="true"
            placeholder="All branches" appendTo="body" />
          <p-select [options]="bankOpts()" [ngModel]="bank()" (ngModelChange)="bank.set($event); resetPage()" [showClear]="true"
            placeholder="All banks" appendTo="body" />
          <label class="pdcm-switch"><p-toggleswitch [ngModel]="actionOnly()" (ngModelChange)="actionOnly.set($event); resetPage()" /> Needs action only</label>
          @if (stage() === 'onhand') {
            <span class="pdcm-win">Upcoming within
              <p-inputnumber [ngModel]="win()" (ngModelChange)="setWin($event)" [min]="1" [max]="90" [showButtons]="true"
                buttonLayout="horizontal" incrementButtonIcon="pi pi-plus" decrementButtonIcon="pi pi-minus" inputStyleClass="pdcm-win-input" />
              days</span>
          }
          <span class="pdcm-result">{{ list().length }} check{{ list().length === 1 ? '' : 's' }} · ₱{{ listTotal() | number:'1.2-2' }}</span>
        </div>

        <div class="cll-body" [class.split]="view() === 'split'">
          <div class="cll-list">
            @if (view() === 'table') {
              <div class="pdcm-scroll">
                <table class="pdcm-table">
                  <thead>
                    <tr>
                      <th>Customer</th><th>Branch</th><th>Receipt</th><th>Check</th>
                      <th class="pdcm-sort" (click)="asc.set(!asc())">Check date {{ asc() ? '↑' : '↓' }}</th>
                      <th class="num">Amount</th><th>Status</th><th class="num">Next step</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (r of pageRows(); track r.p.id) {
                      <tr [class.sel]="selId() === r.p.id" (click)="openDrawer(r)">
                        <td class="pdcm-cust-cell cll-acc" [class.on]="selId() === r.p.id">
                          <div><span>{{ r.p.custKey }} {{ r.p.custName }}</span>
                            <button type="button" class="pdcm-icon" title="Show only this customer's PDCs" aria-label="Filter by this customer"
                              (click)="$event.stopPropagation(); setCustomer(r.p.custKey)"><i class="pi pi-filter"></i></button></div>
                        </td>
                        <td class="pdcm-mono pdcm-dim">{{ r.p.siteCode }}</td>
                        <td class="pdcm-dim nw">{{ r.p.receiptType }} {{ r.p.receiptNo }}</td>
                        <td class="pdcm-dim nw">{{ r.p.bank }} #{{ r.p.checkNo }}</td>
                        <td class="nw"><div>{{ r.p.checkDate | date:'MM/dd/yyyy' }}</div><small [class.late]="r.late">{{ r.rel }}</small></td>
                        <td class="num pdcm-mono nw">{{ r.p.amount | number:'1.2-2' }}</td>
                        <td><span class="pdcm-status" [style.--c]="r.meta.color"><i></i>{{ r.meta.label }}</span></td>
                        <td class="num nw">
                          @if (r.action; as a) { <button type="button" class="pdcm-act" (click)="$event.stopPropagation(); a.run()">{{ a.label }} →</button> }
                          @else { <span class="pdcm-sub">{{ r.note }}</span> }
                        </td>
                      </tr>
                    }
                  </tbody>
                  @if (pageRows().length) {
                    <tfoot>
                      <tr><td colspan="5">Total · {{ list().length }} check{{ list().length === 1 ? '' : 's' }}</td>
                        <td class="num pdcm-mono">{{ listTotal() | number:'1.2-2' }}</td><td colspan="2"></td></tr>
                    </tfoot>
                  }
                </table>
              </div>
            } @else {
              <div class="cll-cards">
                @for (g of groups(); track g.label) {
                  <div class="cll-group pdcm-group"><span>{{ g.label }}</span><span class="pdcm-mono">{{ g.items.length }}</span></div>
                  @for (r of g.items; track r.p.id) {
                    <button type="button" class="cll-card" [class.on]="selId() === r.p.id" (click)="openDrawer(r)">
                      <div class="cll-card-row"><b>{{ r.p.custName || r.p.custKey }}</b><span class="pdcm-mono cll-strong">{{ r.p.amount | number:'1.2-2' }}</span></div>
                      <div class="cll-card-row pdcm-dim"><span>{{ r.p.receiptType }} {{ r.p.receiptNo }} · {{ r.p.bank }} #{{ r.p.checkNo }}</span>
                        <small [class.pdcm-late]="r.late">{{ r.p.checkDate | date:'MM/dd/yyyy' }} · {{ r.rel }}</small></div>
                      <div class="cll-card-pills pdcm-card-foot">
                        <span class="pdcm-status" [style.--c]="r.meta.color">{{ r.meta.label }}</span>
                        <span class="pdcm-next" [class.go]="!!r.action">{{ r.action ? r.action.label + ' →' : r.note }}</span>
                      </div>
                    </button>
                  }
                }
              </div>
            }
            @if (!pageRows().length) {
              <div class="pdcm-empty">{{ loading() ? 'Loading…' : filtered() ? 'No checks match these filters.' : 'No checks in this status.' }}</div>
            }

            <div class="pdcm-pager">
              <span>{{ list().length ? 'Showing ' + ((page() - 1) * pageSize() + 1) + '–' + min(page() * pageSize(), list().length) + ' of ' + list().length : 'Showing 0 of 0' }}</span>
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

    @if (view() === 'table' && sel()) {
      <div class="cll-overlay" (click)="closeDrawer()"></div>
      <div class="cll-pane cll-drawer pdcm-drawer-v2"><ng-container [ngTemplateOutlet]="preview" /></div>
    }

    <ng-template #preview>
      @if (sel(); as r) {
        <div class="cll-pv-head">
          <div class="cll-pv-title">
            <b>{{ r.p.bank }} #{{ r.p.checkNo }}</b>
            <span class="pdcm-status" [style.--c]="r.meta.color"><i></i>{{ r.meta.label }}</span>
            <span class="cll-sep">·</span>
            <span class="cll-pv-cust" [title]="r.p.custKey + ' ' + r.p.custName"><span class="pdcm-mono">{{ r.p.custKey }}</span>{{ r.p.custName }}</span>
          </div>
          <div class="cll-pv-acts">
            <button type="button" class="pdcm-btn sm" (click)="setCustomer(r.p.custKey)">Customer's PDCs</button>
            @if (r.action && !confirmLegal()) { <button type="button" class="pdcm-btn sm primary" (click)="r.action.run()">{{ r.action.label }}</button> }
          </div>
          <div class="cll-pv-nav pdcm-pv-nav">
            @if (canOpen) { <button type="button" class="cll-sq" (click)="open(r.p)" [title]="'Open ' + r.p.receiptType + ' ' + r.p.receiptNo" aria-label="Open receipt">↗</button> }
            @if (pos() >= 0) { <span class="pdcm-mono pdcm-sub">{{ pos() + 1 }} of {{ list().length }}</span> }
            <button type="button" class="cll-sq" (click)="move(-1)" title="Previous (↑)" aria-label="Previous check">↑</button>
            <button type="button" class="cll-sq" (click)="move(1)" title="Next (↓)" aria-label="Next check">↓</button>
            <button type="button" class="cll-sq x" (click)="closeDrawer()" title="Close (Esc)" aria-label="Close preview">×</button>
          </div>
        </div>

        <div class="cll-pv-body">
          @if (confirmLegal()) {
            <div class="pdcm-endorse">
              <div>
                <b>Endorse to legal</b>
                <div class="pdcm-sub">The LOD settlement deadline has passed. Endorsing moves this check to Exceptions › Endorsed to legal and records it in the history.</div>
              </div>
              <div class="pdcm-endorse-row">
                <div>
                  <small>Endorse to</small>
                  <div class="pdcm-radio">
                    @for (o of legalTargets; track o) {
                      <button type="button" [class.on]="endorseTo === o" (click)="endorseTo = o"><i></i>{{ o }}</button>
                    }
                  </div>
                </div>
                <div>
                  <small>Note (optional)</small>
                  <input pInputText [(ngModel)]="legalNote" maxlength="500" placeholder="e.g. Customer unresponsive after LOD" class="w-full" />
                </div>
              </div>
              <div class="pdcm-endorse-acts">
                <button type="button" class="pdcm-btn sm" (click)="confirmLegal.set(false)">Cancel</button>
                <button type="button" class="pdcm-btn sm primary" [disabled]="busy()" (click)="confirmEndorse(r)">Confirm endorsement</button>
              </div>
            </div>
          }

          <div class="cll-summary">
            <div><small>Amount</small><b class="pdcm-mono">₱{{ r.p.amount | number:'1.2-2' }}</b></div>
            <div><small>Check date</small><span class="pdcm-mono">{{ r.p.checkDate | date:'MM/dd/yyyy' }}</span><small [class.pdcm-late]="r.late">{{ r.rel }}</small></div>
            <div><small>Bank</small><span>{{ r.p.bank || '—' }}</span><small>#{{ r.p.checkNo }}</small></div>
            <div><small>Next step</small><span class="pdcm-next-big" [class.go]="!!r.action">{{ r.action?.label ?? r.note }}</span></div>
          </div>

          @if (banner(r); as n) {
            <div class="cll-note" [class.bad]="n.bad"><i>!</i><span>{{ n.text }}</span></div>
          }

          <div class="cll-card-box">
            <div class="cll-box-head"><b>Progress</b><small>{{ progress(r).text }}</small></div>
            <div class="cll-steps-wrap">
              <div class="cll-steps">
                @for (s of progress(r).steps; track $index; let i = $index) {
                  <div class="cll-step" [class.done]="s.state === 'done'" [class.bad]="s.state === 'bad'" [class.cur]="s.current" [pTooltip]="s.tip ?? undefined">
                    @if (i > 0) { <span class="cll-line" [class.on]="s.linked" [class.bad]="s.state === 'bad'"></span> }
                    <span class="cll-dot">{{ s.state === 'done' ? '✓' : s.state === 'bad' ? '✕' : i + 1 }}</span>
                    <span class="cll-step-lbl">{{ s.label }}</span>
                    <span class="cll-step-meta">{{ s.at ? (s.at | date:'MM/dd/yy') : '' }}</span>
                  </div>
                }
              </div>
            </div>
          </div>

          <div class="cll-grid">
            <div class="cll-card-box cll-wide">
              <div class="cll-box-head"><b>Check details</b></div>
              <div class="cll-box-body">
                <div class="cll-sec"><span>Check</span><i></i></div>
                <div class="cll-fields">
                  <div><small>Bank (drawee)</small>{{ r.p.bank || '—' }}</div>
                  <div><small>Check no.</small><span class="pdcm-mono">{{ r.p.checkNo || '—' }}</span></div>
                  <div><small>Check date</small><span class="pdcm-mono">{{ r.p.checkDate | date:'MM/dd/yyyy' }}</span></div>
                  <div><small>Amount</small><span class="pdcm-mono">{{ r.p.amount | number:'1.2-2' }}</span></div>
                </div>
                <div class="cll-sec"><span>Receipt</span><i></i></div>
                <div class="cll-fields">
                  <div><small>Receipt</small><span class="pdcm-mono">{{ r.p.receiptType }} {{ r.p.receiptNo }}</span></div>
                  <div><small>Branch</small><span class="pdcm-mono">{{ r.p.siteCode }}</span></div>
                  <div><small>Customer code</small><span class="pdcm-mono">{{ r.p.custKey }}</span></div>
                  <div><small>Customer</small>{{ r.p.custName }}</div>
                </div>
                @if (r.p.depositSlipRef || r.p.outcome || r.p.dishonor) {
                  <div class="cll-sec"><span>Deposit &amp; outcome</span><i></i></div>
                  <div class="cll-fields">
                    @if (r.p.depositSlipRef) { <div><small>Deposit</small>{{ r.p.depositBank }} {{ r.p.depositSlipRef }} · {{ r.p.depositDate | date:'MM/dd/yyyy' }}</div> }
                    @if (r.p.bankLine; as b) { <div><small>Bank statement</small>{{ b.confirmed ? 'Confirmed by ' + b.confirmedBy : 'Credit found, awaiting CNC' }} · {{ b.txnDate | date:'MM/dd/yyyy' }}</div> }
                    @if (r.p.outcome) { <div><small>Outcome</small>{{ r.p.outcome }} {{ r.p.outcomeDate | date:'MM/dd/yyyy' }}@if (r.p.outcomeReason) { · {{ r.p.outcomeReason }} }</div> }
                    @if (r.p.dishonor?.lodDueDate; as due) { <div><small>Settle by</small>{{ due | date:'MM/dd/yyyy' }}</div> }
                    @if (r.p.dishonor?.settledByReceipt; as cr) { <div><small>Settled by</small>{{ cr }}</div> }
                    @if (r.p.dishonor?.legalEndorsedTo; as to) { <div><small>Endorsed to</small>{{ to }}</div> }
                  </div>
                }
                @if (extras(r).length) {
                  <div class="pdcm-more">
                    @for (x of extras(r); track x.label) { <p-button [label]="x.label" [icon]="x.icon" size="small" [text]="true" (onClick)="x.run()" /> }
                  </div>
                }
              </div>
            </div>
            <div class="cll-card-box">
              <div class="cll-box-head"><b>History</b></div>
              <div class="cll-box-body pdcm-hist">
                @for (e of history(); track $index; let last = $last) {
                  <div class="pdcm-ev">
                    <div class="pdcm-ev-rail"><i [style.background]="last ? r.meta.color : '#CFC9C2'"></i><span></span></div>
                    <div class="pdcm-ev-text"><div>{{ e.text }}</div><small>{{ e.at | date:'MM/dd/yy h:mm a' }}</small></div>
                  </div>
                } @empty { <div class="pdcm-sub">{{ historyLoading() ? 'Loading…' : 'No history yet.' }}</div> }
              </div>
            </div>
          </div>
        </div>
      } @else {
        <div class="cll-pv-empty">
          <div class="cll-doc pdcm-chk"><span></span><span></span></div>
          <b>Select a check to preview</b>
          <small>Use ↑ ↓ to move between checks</small>
        </div>
      }
    </ng-template>

    @if (legalToast(); as t) {
      <div class="pdcm-toast">
        <span>{{ t.text }}</span>
        <button type="button" class="pdcm-toast-link" (click)="viewEndorsed(t.id)">View in Endorsed to legal</button>
        <button type="button" class="pdcm-toast-x" (click)="legalToast.set(null)" aria-label="Dismiss">×</button>
      </div>
    }

    <p-dialog [header]="'Confirm matured PDC — ' + (confirming()?.receiptType ?? '') + ' ' + (confirming()?.receiptNo ?? '')"
      [(visible)]="confirmVisible" [modal]="true" [style]="{ width: '820px' }">
      @if (confirming(); as p) {
        <div class="pdc-cf">
          <section>
            <h4>Check (PDC)</h4>
            <dl>
              <dt>Customer</dt><dd>{{ p.custName || p.custKey }} <small>{{ p.custKey }} · {{ p.siteCode }}</small></dd>
              <dt>Drawee / check no.</dt><dd>{{ p.bank }} #{{ p.checkNo }}</dd>
              <dt>Check date</dt><dd>{{ p.checkDate | date:'MM/dd/yyyy' }}</dd>
              <dt>Amount</dt><dd class="amt">{{ p.amount | number:'1.2-2' }}</dd>
            </dl>
          </section>
          <section>
            <h4>Deposit slip</h4>
            <dl>
              <dt>Deposited into</dt><dd>{{ p.depositBank }}</dd>
              <dt>Slip ref.</dt><dd>{{ p.depositSlipRef || '—' }}</dd>
              <dt>Deposit date</dt><dd [class.diff]="p.depositDate! < p.checkDate">{{ p.depositDate | date:'MM/dd/yyyy' }}</dd>
              <dt>Recorded by</dt><dd>{{ p.depositBy }}</dd>
            </dl>
          </section>
          <section class="bank">
            <h4>Bank statement</h4>
            <dl>
              <dt>Account</dt><dd>{{ p.bankLine?.account }}</dd>
              <dt>Credit date</dt><dd>{{ p.bankLine?.txnDate | date:'MM/dd/yyyy' }}</dd>
              <dt>Credit</dt><dd class="amt" [class.diff]="p.bankLine?.credit !== p.amount">{{ p.bankLine?.credit | number:'1.2-2' }}</dd>
              <dt>Reference</dt><dd [class.diff]="!!p.bankLine?.reference && p.bankLine?.reference !== p.depositSlipRef">{{ p.bankLine?.reference || '—' }}</dd>
              <dt>Description</dt><dd><small>{{ p.bankLine?.description }}</small></dd>
              <dt>Matched by</dt><dd>{{ p.bankLine?.method }}</dd>
            </dl>
          </section>
        </div>
        @if (p.bankLine?.credit !== p.amount) {
          <p class="pdc-cf-note">The credit differs from this check's amount — it covers the whole deposit slip (other checks on the same slip are confirmed together).</p>
        }
        @if (rejecting) {
          <div class="pdc-field">
            <label>Why isn't this credit the check? (required)</label>
            <textarea pTextarea [(ngModel)]="rejectReason" rows="2" maxlength="400" class="w-full" placeholder="e.g. credit belongs to another deposit"></textarea>
          </div>
        }
      }
      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" (onClick)="confirmVisible = false" />
        @if (rejecting) {
          <p-button label="Reject match" severity="danger" [loading]="busy()" [disabled]="!rejectReason.trim()" (onClick)="doReject()" />
        } @else {
          <p-button label="Reject" severity="danger" [outlined]="true" (onClick)="rejecting = true" />
          <p-button label="Confirm & post JV" icon="pi pi-check" severity="success" [loading]="busy()" (onClick)="doConfirm()" />
        }
      </ng-template>
    </p-dialog>

    <p-dialog [header]="'Record PDC deposit — ' + (depositing()?.receiptType ?? '') + ' ' + (depositing()?.receiptNo ?? '')"
      [(visible)]="depositVisible" [modal]="true" [style]="{ width: '420px' }">
      @if (depositing(); as p) {
        <p class="muted">{{ p.custName || p.custKey }} · {{ p.bank }} #{{ p.checkNo }} · {{ p.amount | number:'1.2-2' }}</p>
        <div class="pdc-field">
          <label>Deposited into</label>
          <p-select [options]="depAccounts()" [(ngModel)]="dep.accountId" optionLabel="label" optionValue="id" placeholder="Company account" appendTo="body" styleClass="w-full" />
        </div>
        <div class="pdc-field"><label>Slip ref. no. (optional)</label><input pInputText [(ngModel)]="dep.slip" maxlength="30" class="w-full" /></div>
        <div class="pdc-field">
          <label>Deposit date</label>
          <p-datepicker [(ngModel)]="dep.date" dateFormat="mm/dd/yy" appendTo="body" [showIcon]="true" [maxDate]="today" />
        </div>
      }
      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" (onClick)="depositVisible = false" />
        <p-button label="Save" [loading]="busy()" [disabled]="!dep.accountId || !dep.date" (onClick)="saveDeposit()" />
      </ng-template>
    </p-dialog>

    <p-dialog [header]="'Tag PDC ' + (tagging()?.receiptNo ?? '')" [(visible)]="tagVisible" [modal]="true" [style]="{ width: '440px' }">
      <div class="pdc-field">
        <label>Outcome</label>
        <p-select [options]="outcomes()" [(ngModel)]="tag.outcome" optionLabel="label" optionValue="value" appendTo="body" styleClass="w-full" />
      </div>
      @if (tag.outcome !== 'None') {
        <div class="pdc-field">
          <label>Date the bank cleared / returned it</label>
          <p-datepicker [(ngModel)]="tag.date" dateFormat="mm/dd/yy" appendTo="body" [showIcon]="true" [maxDate]="today" />
        </div>
      }
      <div class="pdc-field">
        <label>Reason {{ tag.outcome === 'Succeeded' ? '(optional)' : '(required)' }}</label>
        <textarea pTextarea [(ngModel)]="tag.reason" rows="2" maxlength="500" class="w-full"
          [placeholder]="tag.outcome === 'Bounced' ? 'e.g. DAIF, account closed' : ''"></textarea>
      </div>
      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" (onClick)="tagVisible = false" />
        <p-button label="Save" [severity]="tag.outcome === 'Bounced' ? 'danger' : 'success'" [loading]="busy()" [disabled]="!tagValid()" (onClick)="saveTag()" />
      </ng-template>
    </p-dialog>

    <p-dialog [header]="'Bounced check follow-up — ' + (following()?.receiptType ?? '') + ' ' + (following()?.receiptNo ?? '')"
      [(visible)]="followVisible" [modal]="true" [style]="{ width: '660px' }">
      @if (following(); as p) {
        @if (p.dishonor; as d) {
          <p class="muted">{{ p.custName || p.custKey }} ({{ p.custKey }}) · {{ p.bank }} #{{ p.checkNo }} · <b>{{ p.amount | number:'1.2-2' }}</b>
            · bounced {{ p.outcomeDate | date:'MM/dd/yyyy' }} — {{ p.outcomeReason }}</p>
          <p-tag [value]="d.state" [severity]="sev(d.state)" />
          <ol class="pdc-fu">
            <li [class.done]="!!d.lodIssuedDate">
              <b>1 · Letter of Dishonor</b>
              @if (d.lodIssuedDate) {
                <div class="muted">Issued {{ d.lodIssuedDate | date:'MM/dd/yyyy' }} by {{ d.lodIssuedBy }} · {{ d.lodSettleDays }} days to settle once received
                  <p-button label="Print LOD" icon="pi pi-print" size="small" [text]="true" (onClick)="printLod()" /></div>
              }
              @if (canIssue(d)) {
                <div class="pdc-fu-row">
                  <p-datepicker [(ngModel)]="fu.lodDate" dateFormat="mm/dd/yy" appendTo="body" [showIcon]="true" [maxDate]="today" placeholder="LOD date" />
                  <p-inputnumber [(ngModel)]="fu.days" [min]="1" [max]="90" [showButtons]="true" inputStyleClass="pdc-days" />
                  <span class="muted">days to settle</span>
                  <p-button [label]="d.lodIssuedDate ? 'Reissue' : 'Issue LOD'" size="small" [outlined]="!!d.lodIssuedDate" [loading]="busy()"
                    [disabled]="!fu.lodDate || !fu.days" (onClick)="issueLod()" />
                </div>
              }
            </li>
            <li [class.done]="!!d.lodReceivedDate">
              <b>2 · Received by the customer</b>
              @if (d.lodReceivedDate) {
                <div class="muted">{{ d.lodReceivedDate | date:'MM/dd/yyyy' }} · settle by <b>{{ d.lodDueDate | date:'MM/dd/yyyy' }}</b>
                  @if (d.lodCopyAttachmentId) { <p-button label="Received copy" icon="pi pi-paperclip" size="small" [text]="true" (onClick)="viewLodCopy()" /> }</div>
              } @else if (canIssue(d) && d.lodIssuedDate) {
                <div class="pdc-fu-row">
                  <p-datepicker [(ngModel)]="fu.rcvDate" dateFormat="mm/dd/yy" appendTo="body" [showIcon]="true" [maxDate]="today" placeholder="Date received" />
                  <input #lodFile type="file" accept=".pdf,.jpg,.jpeg,.png" (change)="fu.file = lodFile.files?.[0] ?? null" />
                  <p-button label="Record" size="small" [loading]="busy()" [disabled]="!fu.rcvDate || !fu.file" (onClick)="recordReceived()" />
                </div>
                <small class="muted">Attach the customer's signed received copy (PDF / JPG / PNG). The deadline runs from this date.</small>
              } @else { <div class="muted">After the LOD is issued.</div> }
            </li>
            <li [class.done]="!!d.settledByCollectionId">
              <b>3 · Settled — new CR</b>
              @if (d.settledByCollectionId) {
                <div class="muted">{{ d.settledByReceipt }} of {{ d.settledReceiptDate | date:'MM/dd/yyyy' }} · linked by {{ d.settledBy }}</div>
                <div class="pdc-fu-row">
                  <input pInputText [(ngModel)]="fu.unsettleReason" maxlength="500" placeholder="Reason, if this is the wrong CR" class="pdc-fu-grow" />
                  <p-button label="Remove settlement" severity="danger" size="small" [outlined]="true" [loading]="busy()"
                    [disabled]="!fu.unsettleReason.trim()" (onClick)="unsettle()" />
                </div>
              } @else {
                <div class="pdc-fu-row">
                  <p-select [options]="candidates()" [(ngModel)]="fu.crId" optionLabel="label" optionValue="id" appendTo="body" styleClass="pdc-fu-grow"
                    placeholder="Verified CR of this customer" emptyMessage="No eligible CR yet" />
                  <p-button label="Settle" severity="success" size="small" [loading]="busy()" [disabled]="!fu.crId" (onClick)="settle()" />
                </div>
                <small class="muted">Encode the customer's new CR on Collections and have it verified. It must cover the full {{ p.amount | number:'1.2-2' }}.</small>
              }
            </li>
            <li [class.done]="!!d.legalEndorsedDate" [class.late]="d.state === 'Overdue'">
              <b>4 · Legal action</b>
              @if (d.legalEndorsedDate) {
                <div class="muted">Endorsed to {{ d.legalEndorsedTo ?? 'legal' }} {{ d.legalEndorsedDate | date:'MM/dd/yyyy' }} by {{ d.legalEndorsedBy }}@if (d.legalRemarks) { — {{ d.legalRemarks }} }</div>
              } @else if (d.state === 'Overdue') {
                <div class="pdc-fu-row">
                  <p-datepicker [(ngModel)]="fu.legalDate" dateFormat="mm/dd/yy" appendTo="body" [showIcon]="true" [maxDate]="today" />
                  <p-select [options]="legalTargets" [(ngModel)]="fu.legalTo" appendTo="body" />
                  <input pInputText [(ngModel)]="fu.legalRemarks" maxlength="500" placeholder="Remarks (optional)" class="pdc-fu-grow" />
                  <p-button label="Endorse for legal action" severity="danger" size="small" [loading]="busy()" [disabled]="!fu.legalDate" (onClick)="endorseLegal()" />
                </div>
              } @else { <div class="muted">Only when it's still unsettled after the deadline.</div> }
            </li>
          </ol>
        }
      }
      <ng-template pTemplate="footer"><p-button label="Close" [text]="true" (onClick)="followVisible = false" /></ng-template>
    </p-dialog>
  `,
  styles: [`
    .muted { color: var(--p-text-muted-color); font-size: .8rem; }
    :host ::ng-deep .pdc-days { width: 3.5rem; }
    .pdc-field { display: flex; flex-direction: column; gap: .3rem; margin-bottom: .75rem; }
    .pdc-field label { font-weight: 500; font-size: .85rem; }
    .pdc-fu { list-style: none; padding: 0; margin: .75rem 0 0; display: flex; flex-direction: column; gap: .7rem; }
    .pdc-fu li { border-left: 3px solid var(--p-surface-border); padding: .1rem 0 .1rem .75rem; }
    .pdc-fu li.done { border-left-color: #22c55e; } .pdc-fu li.late { border-left-color: #ef4444; }
    .pdc-fu-row { display: flex; gap: .4rem; align-items: center; flex-wrap: wrap; margin: .35rem 0 .2rem; }
    :host ::ng-deep .pdc-fu-grow { flex: 1; min-width: 12rem; }
  `]
})
export class PdcPageComponent implements OnInit, OnDestroy {
  private api = inject(CollectionService);
  private router = inject(Router);
  private toolbar = inject(GlobalToolbarService);
  private exportSvc = inject(ExportService);
  private toast = inject(MessageService);
  private auth = inject(AuthService);

  protected readonly sev = statusSeverity;
  protected readonly today = new Date();
  protected readonly min = Math.min;
  protected readonly sizeOpts = [5, 10, 25, 50].map(n => ({ label: `${n} per page`, value: n }));
  protected readonly legalTargets = ['In-house legal', 'External counsel'];
  /** CNC Verifiers have no collections access, so they can't open the collection page. */
  protected canOpen = this.auth.isAdmin() || ['collections-encode', 'collections-verify', 'collections-manage'].some(k => this.auth.hasPermission(k));

  all = signal<PdcItemDto[]>([]);
  loading = signal(true);
  busy = signal(false);

  // ── filters ──
  stage = signal<StageKey>('onhand');
  status = signal<StatusKey | 'all'>('all');
  q = signal('');
  customer = signal<string | null>(null);
  branch = signal<string | null>(null);
  bank = signal<string | null>(null);
  actionOnly = signal(false);
  win = signal(7);
  asc = signal(true);
  pageSize = signal(10);
  private pages = signal<Record<string, number>>({});

  /** Every PDC with its status, next step (for this user) and lateness. */
  private rowsAll = computed<Row[]>(() => this.all().map(p => {
    const st = statusOf(p, this.win()), meta = ST[st], n = p.daysToMaturity;
    const action = this.actionFor(p, st);
    const late = (st === 'for_claim' && n < -2) || (meta.stage === 'deposit' && n < -7) || st === 'overdue_legal';
    return { p, st, meta, action, note: action ? '' : this.noteFor(p, st), rel: rel(n), late };
  }));

  /** Narrowed by search, customer, branch and bank — what the stage bar and tabs count. */
  private base = computed(() => {
    const t = this.q().toLowerCase().trim(), ck = this.customer(), br = this.branch(), bk = this.bank();
    return this.rowsAll().filter(r => (!ck || r.p.custKey === ck) && (!br || r.p.siteCode === br) && (!bk || r.p.bank === bk)
      && (!t || [r.p.custKey, r.p.custName, r.p.receiptType + ' ' + r.p.receiptNo, r.p.checkNo ?? '', r.p.bank ?? '', r.p.depositSlipRef ?? '']
        .some(x => x.toLowerCase().includes(t))));
  });
  filtered = computed(() => !!(this.q().trim() || this.branch() || this.bank() || this.actionOnly()));


  stageCards = computed(() => STAGES.map(g => {
    const rs = this.base().filter(r => r.meta.stage === g.key);
    return { ...g, count: rs.length, amount: rs.reduce((s, r) => s + r.p.amount, 0), actions: rs.filter(r => r.action).length };
  }));

  private inStage = computed(() => this.base().filter(r => r.meta.stage === this.stage()));
  statusTabs = computed(() => {
    const g = STAGES.find(x => x.key === this.stage())!, rs = this.inStage(), w = this.win();
    return [
      { key: 'all' as const, label: `All ${g.label.toLowerCase()}`, color: '#BDB7B0', count: rs.length },
      ...g.statuses.map(k => ({ key: k, color: ST[k].color, count: rs.filter(r => r.st === k).length,
        label: k === 'upcoming' ? `Upcoming (≤ ${w} days)` : k === 'later' ? `Not yet due (> ${w} days)` : ST[k].label })),
    ];
  });

  list = computed(() => {
    const s = this.status(), dir = this.asc() ? 1 : -1;
    return this.inStage().filter(r => (s === 'all' || r.st === s) && (!this.actionOnly() || r.action))
      .sort((a, b) => dir * a.p.checkDate.localeCompare(b.p.checkDate) || b.p.amount - a.p.amount);
  });
  listTotal = computed(() => this.list().reduce((s, r) => s + r.p.amount, 0));

  // ── paging (remembered per stage + status) ──
  private pageKey = computed(() => this.stage() + '|' + this.status());
  pageCount = computed(() => Math.max(1, Math.ceil(this.list().length / this.pageSize())));
  page = computed(() => Math.min(this.pages()[this.pageKey()] ?? 1, this.pageCount()));
  pageRows = computed(() => this.list().slice((this.page() - 1) * this.pageSize(), this.page() * this.pageSize()));
  pageNums = computed(() => {
    const n = this.pageCount(), p = this.page();
    let from = Math.max(1, p - 2);
    const to = Math.min(n, from + 4);
    from = Math.max(1, to - 4);
    return Array.from({ length: to - from + 1 }, (_, i) => from + i);
  });
  goto(n: number) { this.pages.update(x => ({ ...x, [this.pageKey()]: Math.max(1, Math.min(this.pageCount(), n)) })); }

  // ── view: Table (preview opens as a drawer) or Reading pane (preview always on the right) ──
  view = signal<'table' | 'split'>(localStorage.getItem(VIEW_KEY) === 'split' ? 'split' : 'table');
  setView(v: 'table' | 'split') { localStorage.setItem(VIEW_KEY, v); this.view.set(v); }

  /** Reading pane: the page fills the shell's content area and the panel takes the rest (pure CSS, see
   *  .pdcm-fill in styles.scss), so its bottom gap always equals the shell padding; the list and preview scroll inside. */
  @HostBinding('class.pdcm-fill') get fill() { return this.view() === 'split'; }

  /** Reading pane: the page's checks grouped by status. */
  groups = computed(() => {
    const out: { label: string; items: Row[] }[] = [];
    for (const r of this.pageRows()) {
      let g = out.find(x => x.label === r.meta.label);
      if (!g) out.push(g = { label: r.meta.label, items: [] });
      g.items.push(r);
    }
    return out;
  });

  pos = computed(() => this.list().findIndex(r => r.p.id === this.selId()));

  move(d: number) {
    const L = this.list();
    if (!L.length) return;
    const i = this.pos(), n = i < 0 ? 0 : Math.max(0, Math.min(L.length - 1, i + d));
    this.openDrawer(L[n]);
    this.pages.update(x => ({ ...x, [this.pageKey()]: Math.floor(n / this.pageSize()) + 1 }));
  }

  @HostListener('window:keydown', ['$event'])
  onKey(e: KeyboardEvent) {
    const el = e.target as HTMLElement | null, tag = el?.tagName?.toLowerCase() ?? '';
    if (['input', 'select', 'textarea'].includes(tag) || el?.isContentEditable || document.querySelector('.p-dialog-mask')) return;
    if (e.key === 'Escape' && this.selId()) { this.closeDrawer(); return; }
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      if (this.view() !== 'split' && !this.selId()) return;
      e.preventDefault();
      this.move(e.key === 'ArrowDown' ? 1 : -1);
    }
  }

  /**
   * Progress of one check, read from its own fields (nothing is decided here): Received → Verified → Matured →
   * Deposit recorded → Bank credit found → CNC confirmed → Cleared, or after a bounce → LOD issued → LOD received →
   * Settled / Settlement overdue → Endorsed to legal. Like the collection page's tracker, every step before the
   * furthest one reached counts as done.
   */
  progress(r: Row): { steps: CollectionStep[]; text: string } {
    const p = r.p, d = p.dishonor, bounced = p.outcome === 'Bounced';
    const s: CollectionStep[] = [
      { label: 'Received', state: 'done', at: p.receiptDate, dateOnly: true },
      { label: 'Verified', state: p.verificationStatus === 'Verified' ? 'done' : 'todo', tip: p.verificationStatus === 'Verified' ? null : statusLabel(p.verificationStatus) },
      { label: 'Matured', state: p.daysToMaturity <= 0 ? 'done' : 'todo', at: p.checkDate, dateOnly: true, tip: 'Check date' },
      { label: 'Deposit recorded', state: p.depositSlipRef ? 'done' : 'todo', at: p.depositDate, dateOnly: true,
        tip: p.depositSlipRef ? `${p.depositBank ?? ''} slip ${p.depositSlipRef}` : null },
      { label: 'Bank credit found', state: p.bankLine ? 'done' : 'todo', at: p.bankLine?.txnDate, dateOnly: true, tip: p.bankLine?.account ?? null },
      { label: 'CNC confirmed', state: p.bankLine?.confirmed || ['Deposited', 'Succeeded'].includes(p.state) ? 'done' : 'todo', tip: p.bankLine?.confirmedBy ?? null },
    ];
    if (!bounced) s.push({ label: 'Cleared', state: p.outcome === 'Succeeded' ? 'done' : 'todo', at: p.outcomeDate, dateOnly: true });
    else {
      s.push({ label: 'Bounced', state: 'bad', at: p.outcomeDate, dateOnly: true, tip: p.outcomeReason });
      s.push({ label: 'LOD issued', state: d?.lodIssuedDate ? 'done' : 'todo', at: d?.lodIssuedDate, dateOnly: true });
      s.push({ label: 'LOD received', state: d?.lodReceivedDate ? 'done' : 'todo', at: d?.lodReceivedDate, dateOnly: true });
      if (d?.settledByCollectionId) s.push({ label: 'Settled (new CR)', state: 'done', at: d.settledReceiptDate, dateOnly: true, tip: d.settledByReceipt });
      else {
        const overdue = d?.state === 'Overdue' || !!d?.legalEndorsedDate;
        s.push({ label: overdue ? 'Settlement overdue' : 'Settlement', state: overdue ? 'bad' : 'todo', at: d?.lodDueDate, dateOnly: true, tip: 'Settle-by date' });
        s.push({ label: 'Endorsed to legal', state: d?.legalEndorsedDate ? 'done' : 'todo', at: d?.legalEndorsedDate, dateOnly: true, tip: d?.legalEndorsedTo ?? null });
      }
    }
    const cur = s.map(x => x.state !== 'todo').lastIndexOf(true);
    s.forEach((x, i) => { if (i < cur && x.state === 'todo') x.state = 'done'; x.linked = i > 0 && i <= cur; });
    s[cur].current = true;
    const next = s.find((x, i) => i > cur && x.state === 'todo');
    return { steps: s, text: next ? `Next: ${next.label}` : 'Complete' };
  }

  /** The note under the summary: what this check is waiting on. */
  banner(r: Row): { text: string; bad: boolean } | null {
    const d = r.p.dishonor;
    const text = {
      for_claim: `Matured ${r.rel.toLowerCase()}. Deposit this check.`,
      overdue_legal: 'The LOD settlement deadline has passed. Endorse to legal.',
      lod_issue: 'Check bounced. Issue an LOD to the customer.',
      bounced: 'Check bounced. Review and decide the next step.',
      lod_out: d?.lodReceivedDate ? 'LOD issued. Waiting for the customer to settle.' : 'LOD issued. Record the date the customer received it.',
    }[r.st as string] ?? (r.late ? `This check is overdue at ${r.meta.label.toLowerCase()}.` : '');
    return text ? { text, bad: r.meta.stage === 'exceptions' && r.st !== 'endorsed_legal' } : null;
  }
  resetPage() { this.pages.set({}); }

  // ── filter options ──
  customerOpts = computed(() => {
    const m = new Map<string, string>();
    for (const p of this.all()) if (!m.has(p.custKey)) m.set(p.custKey, `${p.custKey} ${p.custName}`);
    return [...m].map(([value, label]) => ({ value, label })).sort((a, b) => a.label.localeCompare(b.label));
  });
  branchOpts = computed(() => [...new Set(this.all().map(p => p.siteCode))].sort());
  bankOpts = computed(() => [...new Set(this.all().map(p => p.bank).filter((b): b is string => !!b))].sort());
  custLabel(ck: string) { return this.customerOpts().find(o => o.value === ck)?.label ?? ck; }

  custStats = computed(() => {
    const mine = this.rowsAll().filter(r => r.p.custKey === this.customer());
    return STAGES.map(g => {
      const rs = mine.filter(r => r.meta.stage === g.key);
      return { key: g.key, label: g.label, color: g.color, count: rs.length, amount: rs.reduce((s, r) => s + r.p.amount, 0) };
    });
  });
  custTotal = computed(() => {
    const mine = this.all().filter(p => p.custKey === this.customer());
    return { count: mine.length, amount: mine.reduce((s, p) => s + p.amount, 0) };
  });

  pickStage(k: StageKey) { this.stage.set(k); this.status.set('all'); }
  pickStatus(k: StatusKey | 'all') { this.status.set(k); }
  setWin(n: number | null) { if (n) this.win.set(Math.min(90, Math.max(1, n))); }

  /** Filter to one customer; if they have nothing in the current stage, jump to the first stage they do. */
  setCustomer(ck: string | null) {
    this.closeDrawer();
    this.customer.set(ck || null);
    this.q.set('');
    this.resetPage();
    if (!ck) return;
    const mine = this.rowsAll().filter(r => r.p.custKey === ck);
    if (!mine.some(r => r.meta.stage === this.stage())) {
      const g = STAGES.find(x => mine.some(r => r.meta.stage === x.key));
      if (g) this.stage.set(g.key);
      this.status.set('all');
    } else if (this.status() !== 'all' && !mine.some(r => r.st === this.status())) this.status.set('all');
  }

  jumpToday() { this.stage.set('onhand'); this.status.set('for_claim'); this.actionOnly.set(false); }

  /** The next step this user can take on a check, or null (then noteFor explains what it waits on). */
  private actionFor(p: PdcItemDto, st: StatusKey): Act | null {
    const d = p.dishonor;
    switch (st) {
      case 'for_claim': return p.canRecordDeposit ? { label: 'Deposit', run: () => this.openDeposit(p) } : null;
      case 'cnc': return p.canConfirmMatch ? { label: 'Confirm', run: () => this.openConfirm(p) } : null;
      case 'dep_tag': return p.canTag ? { label: 'Tag outcome', run: () => this.openTag(p) } : null;
      case 'bounced': return p.canTag ? { label: 'Review', run: () => this.openTag(p) } : null;
      case 'lod_issue': return d?.canFollowUp ? { label: 'Issue LOD', run: () => this.openFollowUp(p) } : null;
      case 'lod_out': return d?.canFollowUp && !d.lodReceivedDate ? { label: 'Record receipt', run: () => this.openFollowUp(p) } : null;
      case 'overdue_legal': return d?.canFollowUp ? { label: 'Endorse to legal', run: () => this.startEndorse(p) } : null;
      default: return null;
    }
  }

  private noteFor(p: PdcItemDto, st: StatusKey): string {
    switch (st) {
      case 'upcoming': case 'later': return `Matures ${rel(p.daysToMaturity)}`;
      case 'for_claim': return 'For deposit';
      case 'cnc': return 'Awaiting CNC';
      case 'dep_recorded': return 'Awaiting bank match';
      case 'dep_tag': return 'Awaiting tag';
      case 'lod_out': return p.dishonor?.lodDueDate ? `Settle by ${mdy(p.dishonor.lodDueDate)}` : 'LOD sent';
      case 'settled': return p.dishonor?.settledByReceipt ?? '—';
      case 'endorsed_legal': return p.dishonor?.legalEndorsedTo ?? '—';
      default: return '—';
    }
  }

  /** Secondary steps in the drawer that aren't already its main button. */
  extras(r: Row): { label: string; icon: string; run: () => void }[] {
    const p = r.p, x: { label: string; icon: string; run: () => void }[] = [];
    const followUpIsMain = r.st === 'lod_issue' || (r.st === 'lod_out' && !!r.action);
    if (p.dishonor?.canFollowUp && !followUpIsMain)
      x.push({ label: 'Bounced follow-up steps', icon: 'pi pi-envelope', run: () => this.openFollowUp(p) });
    if (p.dishonor?.lodIssuedDate && p.dishonor.canFollowUp) x.push({ label: 'Print LOD', icon: 'pi pi-print', run: () => { this.following.set(p); this.printLod(); } });
    if (p.canTag && !['dep_tag', 'bounced'].includes(r.st)) x.push({ label: 'Change tag', icon: 'pi pi-tag', run: () => this.openTag(p) });
    if (p.canRecordDeposit && r.st !== 'for_claim') x.push({ label: 'Edit deposit', icon: 'pi pi-building-columns', run: () => this.openDeposit(p) });
    if (p.canConfirmMatch && r.st !== 'cnc') x.push({ label: 'Confirm match', icon: 'pi pi-verified', run: () => this.openConfirm(p) });
    return x;
  }

  ngOnInit() {
    this.load();
    if (this.auth.isAdmin() || this.auth.hasPermission('pdc-tag')) this.api.lodSettings().subscribe(res => this.lodDays = res.data?.settleDays ?? 5);
    this.toolbar.set({
      title: 'PDC Monitor',
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
        pdf: () => this.exportSvc.exportPdf('Post-dated Checks', EXPORT_COLS, this.exportRows()),
        excel: () => this.exportSvc.exportExcel('Post-dated Checks', EXPORT_COLS, this.exportRows()),
        csv: () => this.exportSvc.exportCsv('Post-dated Checks', EXPORT_COLS, this.exportRows()),
      },
    });
  }

  ngOnDestroy() { this.toolbar.clear(); clearTimeout(this.toastTimer); }

  load(after?: () => void) {
    this.loading.set(true);
    this.api.pdcList(null, 90).subscribe({
      next: res => { this.all.set(res.data ?? []); this.loading.set(false); after?.(); },
      error: () => this.loading.set(false)
    });
  }

  exportList() { this.exportSvc.exportExcel('Post-dated Checks', EXPORT_COLS, this.exportRows()); }

  private exportRows() {
    return this.list().map(r => ({ ...r.p, status: r.meta.label, nextStep: r.action?.label ?? r.note, settleBy: r.p.dishonor?.lodDueDate ?? '' })) as unknown as Record<string, unknown>[];
  }

  open(p: PdcItemDto) { if (this.canOpen) this.router.navigate(['/collections'], { state: { collectionId: p.id } }); }

  // ── drawer: details + history; endorsing an overdue bounced PDC to legal ──

  selId = signal<number | null>(null);
  sel = computed(() => this.rowsAll().find(r => r.p.id === this.selId()) ?? null);
  history = signal<{ text: string; at: string }[]>([]);
  historyLoading = signal(false);
  confirmLegal = signal(false);
  endorseTo = 'In-house legal';
  legalNote = '';
  legalToast = signal<{ text: string; id: number } | null>(null);
  private toastTimer?: ReturnType<typeof setTimeout>;

  openDrawer(r: Row, confirm = false) {
    const changed = this.selId() !== r.p.id;
    this.selId.set(r.p.id);
    this.confirmLegal.set(confirm);
    if (confirm) { this.endorseTo = this.legalTargets[0]; this.legalNote = ''; }
    if (changed || confirm) this.loadHistory(r.p.id);
  }

  closeDrawer() { this.selId.set(null); this.confirmLegal.set(false); }

  private loadHistory(id: number) {
    this.history.set([]);
    this.historyLoading.set(true);
    this.api.pdcHistory(id).subscribe({
      next: res => {
        this.historyLoading.set(false);
        if (this.selId() !== id) return;
        this.history.set((res.data ?? []).map(h => ({
          text: h.action + (h.reason ? ' · ' + h.reason : '') + (h.by ? ` (${h.by})` : ''), at: h.at,
        })));
      },
      error: () => this.historyLoading.set(false)
    });
  }

  private startEndorse(p: PdcItemDto) {
    const r = this.rowsAll().find(x => x.p.id === p.id);
    if (r) this.openDrawer(r, true);
  }

  confirmEndorse(r: Row) {
    const to = this.endorseTo, name = r.p.custName || r.p.custKey;
    this.busy.set(true);
    this.api.endorseLegal(r.p.id, this.iso(new Date())!, this.legalNote.trim() || null, to).subscribe({
      next: () => {
        this.busy.set(false);
        if (this.view() === 'split') this.confirmLegal.set(false); else this.closeDrawer();
        clearTimeout(this.toastTimer);
        this.legalToast.set({ text: `${name} endorsed to ${to}.`, id: r.p.id });
        this.toastTimer = setTimeout(() => this.legalToast.set(null), 6000);
        this.load();
      },
      error: err => { this.busy.set(false); this.toast.add({ severity: 'error', summary: 'Bounced PDC', detail: err?.error?.message ?? 'Please try again.' }); }
    });
  }

  viewEndorsed(id: number) {
    this.legalToast.set(null);
    this.stage.set('exceptions');
    this.status.set('endorsed_legal');
    this.actionOnly.set(false);
    const r = this.rowsAll().find(x => x.p.id === id);
    if (r) this.openDrawer(r);
  }

  // ── dialogs: confirm match (CNC), record deposit, tag outcome ──

  confirming = signal<PdcItemDto | null>(null);
  confirmVisible = false;
  rejecting = false;
  rejectReason = '';
  depositing = signal<PdcItemDto | null>(null);
  depositVisible = false;
  depAccounts = signal<BankAccountOptionDto[]>([]);
  dep = { accountId: null as number | null, slip: '', date: null as Date | null };
  tagging = signal<PdcItemDto | null>(null);
  tagVisible = false;
  tag = { outcome: 'Succeeded' as 'Succeeded' | 'Bounced' | 'None', date: null as Date | null, reason: '' };

  outcomes = computed(() => [
    { label: 'Succeeded (cleared)', value: 'Succeeded' },
    { label: 'Bounced', value: 'Bounced' },
    ...(this.tagging()?.outcome ? [{ label: 'Clear the tag (wrong entry)', value: 'None' }] : []),
  ]);

  openConfirm(p: PdcItemDto) {
    this.confirming.set(p);
    this.rejecting = false;
    this.rejectReason = '';
    this.confirmVisible = true;
  }

  doConfirm() {
    const p = this.confirming();
    if (!p) return;
    this.act(this.api.confirmPdcMatch(p.id), `${p.receiptType} ${p.receiptNo} confirmed — Deposited, JV queued for HOMS`, () => this.confirmVisible = false);
  }

  doReject() {
    const p = this.confirming();
    if (!p) return;
    this.act(this.api.rejectPdcMatch(p.id, this.rejectReason.trim()), `${p.receiptType} ${p.receiptNo}: match rejected — back to awaiting the bank credit`,
      () => this.confirmVisible = false);
  }

  openDeposit(p: PdcItemDto) {
    this.depositing.set(p);
    this.dep = { accountId: null, slip: '', date: new Date() };
    this.depAccounts.set([]);
    this.api.pdcDepositAccounts(p.siteId).subscribe(res => {
      const list = res.data ?? [];
      this.depAccounts.set(list);
      if (list.length === 1) this.dep.accountId = list[0].id;
    });
    this.depositVisible = true;
  }

  saveDeposit() {
    const p = this.depositing();
    if (!p) return;
    this.act(this.api.recordPdcDeposit(p.id, { bankAccountId: this.dep.accountId, slipRefNo: this.dep.slip.trim(), depositDate: this.iso(this.dep.date)! }),
      `${p.receiptType} ${p.receiptNo}: deposit recorded`, () => this.depositVisible = false);
  }

  /** Run a dialog's action, then refresh the list (and the drawer's history if it's open on that check). */
  private act(call: Observable<unknown>, done: string, close: () => void) {
    this.busy.set(true);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        close();
        this.toast.add({ severity: 'success', summary: 'PDC', detail: done });
        this.load(() => { const id = this.selId(); if (id) this.loadHistory(id); });
      },
      error: err => { this.busy.set(false); this.toast.add({ severity: 'error', summary: 'PDC', detail: err?.error?.message ?? 'Please try again.' }); }
    });
  }

  openTag(p: PdcItemDto) {
    this.tagging.set(p);
    this.tag = { outcome: p.outcome === 'Succeeded' ? 'Bounced' : 'Succeeded', date: new Date(), reason: '' };
    this.tagVisible = true;
  }

  tagValid() {
    const t = this.tag;
    return t.outcome === 'Succeeded' ? !!t.date : t.outcome === 'Bounced' ? !!t.date && !!t.reason.trim() : !!t.reason.trim();
  }

  saveTag() {
    const p = this.tagging();
    if (!p) return;
    const date = this.tag.outcome === 'None' ? null : this.iso(this.tag.date);
    this.act(this.api.tagPdc(p.id, this.tag.outcome, date, this.tag.reason.trim() || null),
      `${p.receiptType} ${p.receiptNo}: ${this.tag.outcome === 'None' ? 'tag cleared' : this.tag.outcome}`, () => this.tagVisible = false);
  }

  // ── bounced PDC follow-up: LOD -> received -> settled by a new CR, or endorsed for legal action ──

  following = signal<PdcItemDto | null>(null);
  followVisible = false;
  candidates = signal<(SettlementCandidateDto & { label: string })[]>([]);
  private lodDays = 5;
  fu = this.blankFollowUp(5);

  private blankFollowUp(days: number) {
    const today = new Date();
    return { lodDate: today as Date | null, days: days as number | null, rcvDate: today as Date | null, file: null as File | null,
      crId: null as number | null, unsettleReason: '', legalDate: today as Date | null, legalRemarks: '', legalTo: 'In-house legal' };
  }

  /** The LOD can be (re)issued until the customer has received it, and not once the case is closed. */
  canIssue(d: PdcDishonorDto) { return !d.lodReceivedDate && !d.settledByCollectionId && !d.legalEndorsedDate; }

  openFollowUp(p: PdcItemDto) {
    this.following.set(p);
    this.fu = this.blankFollowUp(p.dishonor?.lodSettleDays ?? this.lodDays);
    this.candidates.set([]);
    if (!p.dishonor?.settledByCollectionId)
      this.api.settlementCandidates(p.id).subscribe(res => this.candidates.set((res.data ?? []).map(c => ({
        ...c, label: `${c.receiptType} ${c.receiptNo} · ${c.receiptDate} · ${c.amount.toLocaleString('en-PH', { minimumFractionDigits: 2 })} · ${c.siteCode}`,
      }))));
    this.followVisible = true;
  }

  /** Run a follow-up step, then refresh the list and the open dialog. */
  private followAct(call: Observable<unknown>, done: string) {
    const id = this.following()?.id;
    this.busy.set(true);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.add({ severity: 'success', summary: 'Bounced PDC', detail: done });
        this.load(() => { const p = this.all().find(x => x.id === id); if (p && this.followVisible) this.openFollowUp(p); if (this.selId() === id) this.loadHistory(id!); });
      },
      error: err => { this.busy.set(false); this.toast.add({ severity: 'error', summary: 'Bounced PDC', detail: err?.error?.message ?? 'Please try again.' }); }
    });
  }

  issueLod() {
    const p = this.following();
    if (!p || !this.fu.lodDate || !this.fu.days) return;
    this.followAct(this.api.issueLod(p.id, this.iso(this.fu.lodDate)!, this.fu.days), 'LOD issued — print it and have the customer sign the received copy');
  }

  printLod() {
    const p = this.following();
    if (!p) return;
    const w = window.open('', '_blank');   // opened on the click, or the popup blocker stops it
    if (!w) { this.toast.add({ severity: 'warn', summary: 'Bounced PDC', detail: 'Allow pop-ups for HOMSys to print the letter.' }); return; }
    this.api.lodLetter(p.id).subscribe({
      next: res => printLodLetter(w, res.data!),
      error: err => { w.close(); this.toast.add({ severity: 'error', summary: 'Bounced PDC', detail: err?.error?.message ?? 'Please try again.' }); }
    });
  }

  recordReceived() {
    const p = this.following();
    if (!p || !this.fu.rcvDate || !this.fu.file) return;
    this.followAct(this.api.lodReceived(p.id, this.iso(this.fu.rcvDate)!, this.fu.file), 'LOD receipt recorded — settlement deadline set');
  }

  viewLodCopy() {
    const p = this.following();
    if (!p) return;
    this.api.lodCopy(p.id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    });
  }

  settle() {
    const p = this.following();
    if (!p || !this.fu.crId) return;
    this.followAct(this.api.settleBounced(p.id, this.fu.crId), `${p.receiptType} ${p.receiptNo}: settled`);
  }

  unsettle() {
    const p = this.following();
    if (!p || !this.fu.unsettleReason.trim()) return;
    this.followAct(this.api.unsettleBounced(p.id, this.fu.unsettleReason.trim()), `${p.receiptType} ${p.receiptNo}: settlement removed`);
  }

  endorseLegal() {
    const p = this.following();
    if (!p || !this.fu.legalDate) return;
    this.followAct(this.api.endorseLegal(p.id, this.iso(this.fu.legalDate)!, this.fu.legalRemarks.trim() || null, this.fu.legalTo),
      `${p.receiptType} ${p.receiptNo}: endorsed for legal action`);
  }

  private iso(d: Date | null): string | null {
    if (!d) return null;
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
  }
}
