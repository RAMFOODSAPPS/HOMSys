import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { NavigationEnd, Router } from '@angular/router';
import { from, interval, Subscription } from 'rxjs';
import { concatMap, filter, switchMap, take } from 'rxjs/operators';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { ToastModule } from 'primeng/toast';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { CollectionService } from '../../../core/services/collection.service';
import { AuthService } from '../../../core/services/auth.service';
import { BankStatementService } from '../../../core/services/bank-statement.service';
import {
  BankAccountOptionDto, CollectionDto, CollectionSiteDto, DEDUCTION_TYPES, DeductionType, InvoiceSyncRequestStatusDto, OpenInvoiceDto,
  PAYMENT_MODES, PaymentMode, RECEIPT_TYPES, ReceiptType, SaveCollectionDto, statusLabel, statusSeverity
} from '../../../core/models/collection.model';
import { CollectionStep as Step, collectionSteps } from '../collection-steps';

/** One invoice the collection is applied to, as edited in the grid. */
interface Line {
  postedInvoiceId: number; docNo: number; docDate: string; custKey: string; custName: string;
  total: number; rfcTotal: number; available: number;
  applied: number | null; deds: Ded[];
}

/** One deduction (EWT, discount…) on an invoice line; an invoice may carry several. */
interface Ded { type: DeductionType | null; amount: number | null; ref: string; }



const NEW_KEY = '/collections';

@Component({
  selector: 'app-collection-page',
  standalone: true,
  providers: [MessageService],
  imports: [
    ReactiveFormsModule, FormsModule, DatePipe, DecimalPipe, NgTemplateOutlet, InputTextModule, InputNumberModule, DatePickerModule,
    SelectModule, ButtonModule, MessageModule, ToastModule, TagModule, DialogModule, TextareaModule, TooltipModule, CheckboxModule
  ],
  template: `
    <p-toast position="top-right" />

    <div class="rv">
      <header class="rv-header">
        <div class="rv-header-in">
          <div class="rv-titles">
            <div class="crumb">Collections / Receipts</div>
            <div class="rv-title-row">
              @if (col(); as c) {
                <h1>{{ c.receiptType }} {{ c.receiptNo }} <span class="dot-sep">·</span> {{ c.siteName }}</h1>
                <div class="pills">
                  <span [class]="'pill pill-' + sev(c.verificationStatus)">{{ statusLabel(c.verificationStatus) }}</span>
                  <span [class]="'pill pill-' + sev(c.paymentStatus)">{{ c.paymentStatus }}</span>
                  @if (c.pdcState) { <span [class]="'pill pill-' + sev(c.pdcState)">PDC: {{ c.pdcState }}</span> }
                </div>
              } @else {
                <h1>New collection</h1>
                @if (canEncodeRa) {
                  <div class="pills">
                    <span class="pill pill-contrast">CR</span>
                    <a class="pill ra-pick" (click)="router.navigate(['/collections/remittance'])" pTooltip="NKA chain payment with the chain's RA Excel(s)">Remittance Advice</a>
                  </div>
                }
              }
            </div>
          </div>
          @if (col(); as c) {
            <div class="rv-actions">
              @if (c.canVoid) { <button type="button" class="btn btn-danger" (click)="voidVisible = true">Void</button> }
              @if (c.canPulloutPdc) { <button type="button" class="btn btn-danger" (click)="pulloutVisible = true">Pull out / Replace</button> }
              @if (c.canRecordDeposit) { <button type="button" class="btn" (click)="openDeposit()">Record PDC deposit</button> }
              @if (c.canSubmit) {
                <span [pTooltip]="c.missing.length ? 'Still a draft: ' + c.missing.join(' ') : undefined" tooltipPosition="bottom">
                  <button type="button" class="btn btn-primary" [disabled]="busy() || c.missing.length > 0" (click)="submit()">Submit for verification</button>
                </span>
              }
              @if (c.canConfirmMatch) {
                <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="matchAction(false)">Reject bank match</button>
                <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="matchAction(true)">Approve bank match</button>
              }
              @if (c.canVerify) {
                <button type="button" class="btn btn-danger" (click)="returnVisible = true">Return for correction</button>
                <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="doVerify()">Mark as verified</button>
              }
            </div>
          }
        </div>
      </header>

      <main class="rv-main">
        @if (col(); as c) {
          <section class="card steps-card">
            <div class="steps" [style.grid-template-columns]="'repeat(' + steps().length + ', minmax(80px, 1fr))'">
              @for (s of steps(); track $index) {
                <div class="step" [class.done]="s.state === 'done'" [class.bad]="s.state === 'bad'" [class.current]="s.current" [class.linked]="s.linked" [pTooltip]="s.tip ?? undefined">
                  @if ($index > 0) { <div class="line"></div> }
                  <div class="dot">
                    @switch (s.state) {
                      @case ('done') { ✓ }
                      @case ('bad') { ✕ }
                      @default { {{ $index + 1 }} }
                    }
                  </div>
                  <div class="lbl">{{ s.label }}</div>
                  <div class="meta">
                    @if (s.at) {
                      <span>{{ s.at | date:'MM/dd/yy' }}</span>
                      @if (!s.dateOnly) { <span>{{ s.at | date:'hh:mm a' }}</span> }
                    }
                    @if (s.by) { <span>{{ s.by }}</span> }
                  </div>
                </div>
              }
            </div>
          </section>
          @if (c.remittanceId) {
            <p-message severity="info" styleClass="w-full">
              Part of Remittance Advice #{{ c.remittanceId }} — its invoices come from the RA Excel, and it's submitted, verified and voided with the RA.
              <button type="button" class="btn ml-2" (click)="router.navigate(['/collections/remittance'], { state: { remittanceId: c.remittanceId } })">Open RA #{{ c.remittanceId }}</button>
            </p-message>
          }
          @if (c.verificationStatus === 'Returned' && c.returnReason) {
            <p-message severity="warn" styleClass="w-full">Returned by finance: {{ c.returnReason }}</p-message>
          }
          @if (c.missing.length) {
            <p-message severity="warn" styleClass="w-full">Draft — complete it to submit: {{ c.missing.join(' ') }}</p-message>
          }
          @if (c.bankLine && !c.bankLine.confirmed) {
            <p-message severity="info" styleClass="w-full">
              Bank match found: {{ c.bankLine.account }} · {{ c.bankLine.txnDate | date:'MM/dd/yyyy' }} · credit {{ c.bankLine.credit | number:'1.2-2' }}
              @if (c.bankLine.reference) { · ref {{ c.bankLine.reference }} } —
              {{ c.paymentMode === 'PostDatedCheck' ? 'awaiting the CNC Verifier\'s confirmation on the PDC Monitor (posts the JV).'
                 : c.canVerify ? 'verifying approves it.' : c.canConfirmMatch ? 'approve or reject it.' : 'awaiting the Finance Verifier.' }}
            </p-message>
          }
          @if (c.verificationStatus === 'Voided') {
            <p-message severity="error" styleClass="w-full">Voided: {{ c.voidReason }}</p-message>
          } @else if (c.pdcPulloutRequestedAt) {
            <p-message severity="warn" styleClass="w-full">
              {{ c.pdcPullout }} ({{ c.pdcPulloutRequestedBy }}, {{ c.pdcPulloutRequestedAt | date:'MM/dd/yy hh:mm a' }}): {{ c.pdcPulloutReason }} —
              cancelling its RPDC in HOMS; the PDC is voided once that's done.
            </p-message>
          }
          @for (p of c.homsPostings; track p.id) {
            @if (p.status !== 'Cancelled') {
              <p-message [severity]="homsSev(p.status)" styleClass="w-full">
                <span>HOMS {{ p.kind }}{{ p.docNo ? ' ' + p.docNo : '' }}:
                  @switch (p.status) {
                    @case ('Done') { posted@if (p.file) { in {{ p.file }} } {{ p.completedAt | date:'MM/dd/yyyy hh:mm a' }}@if (p.message) { — {{ p.message }} } }
                    @case ('Held') { on hold — {{ p.message }} }
                    @case ('Failed') { failed — {{ p.message }} }
                    @default { queued — the HO watcher writes it within a minute@if (p.message) { ({{ p.message }}) } }
                  }
                </span>
                @if (c.canRetryHoms && (p.status === 'Held' || p.status === 'Failed')) {
                  <button type="button" class="btn ml-2" [disabled]="busy()" (click)="retryHoms()">Retry</button>
                }
              </p-message>
            }
          }
          @if (c.bms; as b) {
            <p-message [severity]="b.bounced ? 'error' : 'success'" styleClass="w-full">
              Found in HOMS: OR {{ b.docNo }} dated {{ b.docDate | date:'MM/dd/yyyy' }}, posted {{ b.posted | date:'MM/dd/yyyy' }} —
              collected {{ b.received | number:'1.2-2' }}@if (b.ewt) { + CEWT {{ b.ewt | number:'1.2-2' }} }
              <span class="muted">(matched by {{ b.linkMethod }})</span>@if (b.bounced) { · <b>HOMS flags this check as bounced</b> }
            </p-message>
          }
        } @else if (!canEncode) {
          <p-message severity="info" styleClass="w-full">Open a collection from the Collections list or the Verification Queue.</p-message>
        }
        @if (apiError()) {
          <p-message severity="error" styleClass="w-full">{{ apiError() }}</p-message>
        }

        <div class="rv-grid">
          <div class="rv-col">
            <form [formGroup]="form" autocomplete="off" class="card">
              <div class="card-h">Receipt details</div>
              <div class="fields three">
                <div class="field">
                  <span>Receipt type</span>
                  <p-select formControlName="receiptType" [options]="receiptTypes" optionLabel="label" optionValue="value"
                    styleClass="w-full" (onChange)="onReceiptTypeChange()" />
                </div>
                <div class="field">
                  <span>Receipt no.</span>
                  <input pInputText formControlName="receiptNo" maxlength="20" placeholder="As printed on the receipt" class="mono" />
                </div>
                <div class="field">
                  <span>Receipt date</span>
                  <p-datepicker formControlName="receiptDate" dateFormat="mm/dd/yy" appendTo="body" [showIcon]="!readOnly()" styleClass="w-full" inputStyleClass="mono" />
                </div>
                <div class="field">
                  <span>Payment mode</span>
                  <p-select formControlName="paymentMode" [options]="modeOptions()" optionLabel="label" optionValue="value" styleClass="w-full" />
                </div>
                <div class="field">
                  <span>Branch</span>
                  <p-select formControlName="siteId" [options]="sites()" optionLabel="name" optionValue="id" placeholder="Select branch" styleClass="w-full" />
                </div>
                <div class="field">
                  <span>Customer</span>
                  <div class="inline nowrap">
                    <input pInputText formControlName="custKey" maxlength="7" placeholder="Filled from the invoices" class="mono grow" />
                    @if (!readOnly()) {
                      <p-button icon="pi pi-search" [text]="true" pTooltip="Show this customer's synced open invoices"
                        [disabled]="!raw().custKey" (onClick)="loadCustomerInvoices()" />
                    }
                  </div>
                  @if (custName()) { <small class="muted">{{ custName() }}@if (customersOnReceipt().length > 1) { + {{ customersOnReceipt().length - 1 }} more (chain payment) }</small> }
                </div>
              </div>

              @if (isCheck()) {
                <div class="sub-box">
                  <div class="sub-title">Check</div>
                  <div class="fields auto">
                    <div class="field"><span>Bank (drawee)</span><input pInputText formControlName="bank" maxlength="50" /></div>
                    <div class="field"><span>Check no.</span><input pInputText formControlName="checkNo" maxlength="20" class="mono" /></div>
                    <div class="field"><span>Check date</span>
                      <p-datepicker formControlName="checkDate" dateFormat="mm/dd/yy" appendTo="body" [showIcon]="!readOnly()" styleClass="w-full" inputStyleClass="mono" /></div>
                    <ng-container *ngTemplateOutlet="amountField" />
                  </div>
                </div>
              }

              @if (raw().receiptType === 'CR') {
                <div class="sub-box">
                  <div class="sub-title">Deposit <span class="muted">· bank statement as of {{ asOf() ? (asOf() | date:'MM/dd/yyyy') : '—' }}</span></div>
                  <div class="fields auto">
                    <div class="field"><span>Deposited into</span>
                      <p-select formControlName="depAccountId" [options]="depositAccounts()" optionLabel="label" optionValue="id"
                        [showClear]="!readOnly()" placeholder="Company account" styleClass="w-full" appendTo="body" /></div>
                    <div class="field"><span>Slip / transfer ref. no. <span class="muted">(optional)</span></span><input pInputText formControlName="depSlip" maxlength="30" class="mono" /></div>
                    <div class="field"><span>Deposit date</span>
                      <p-datepicker formControlName="depDate" dateFormat="mm/dd/yy" appendTo="body" [showIcon]="!readOnly()" styleClass="w-full" inputStyleClass="mono" /></div>
                    @if (!isCheck()) { <ng-container *ngTemplateOutlet="amountField" /> }
                  </div>
                </div>
              } @else if (col()?.deposit) {
                <div class="sub-box">
                  <div class="sub-title">Deposit</div>
                  <div class="fields auto">
                    <div class="field"><span>Deposited into</span><div class="ro">{{ col()!.deposit!.bank }}</div></div>
                    <div class="field"><span>Slip ref. no.</span><div class="ro mono">{{ col()!.deposit!.slipRefNo || '—' }}</div></div>
                    <div class="field"><span>Deposit date</span><div class="ro mono">{{ col()!.deposit!.depositDate | date:'MM/dd/yyyy' }}</div></div>
                    <div class="field"><span>Bank statement</span><div class="ro">{{ bankNote() }}</div></div>
                  </div>
                </div>
              }
              @if (!isCheck() && raw().receiptType !== 'CR') {
                <div class="fields three"><ng-container *ngTemplateOutlet="amountField" /></div>
              }
              <ng-template #amountField>
                <div class="field">
                  <span>Amount received</span>
                  <p-inputnumber formControlName="amount" mode="decimal" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" styleClass="w-full" inputStyleClass="mono num-in" />
                </div>
              </ng-template>
            </form>

            <section class="card clip">
              <div class="card-h between"><span>Applied to invoices</span><span class="count">{{ lines().length }} invoice{{ lines().length === 1 ? '' : 's' }}</span></div>
              @if (!readOnly() || multiCustomer()) {
                <label class="multi-cust">
                  <p-checkbox [ngModel]="multiCustomer()" (ngModelChange)="setMultiCustomer($event)" [binary]="true" [disabled]="readOnly()" />
                  <span>Multiple customer payment <span class="muted">(chain accounts, e.g. SM paying for its branches) — invoices of different customers on this one receipt</span></span>
                </label>
              }
              @if (!readOnly()) {
                <div class="fetch-bar">
                  <p-inputnumber [(ngModel)]="fetchNo" [useGrouping]="false" placeholder="Invoice no." [disabled]="fetching() || !!fetched()"
                    (onBlur)="autoFetch()" (keydown.enter)="autoFetch()" />
                  <p-datepicker [(ngModel)]="fetchDate" dateFormat="mm/dd/yy" appendTo="body" placeholder="Invoice date" [showIcon]="true" [disabled]="fetching() || !!fetched()"
                    (onSelect)="autoFetch()" (onBlur)="autoFetch()" />
                  @if (fetched(); as fi) {
                    <p-button label="Add" icon="pi pi-plus" [disabled]="hasLine(fi.id) || otherCustomer(fi)" (onClick)="addFetched()" />
                  } @else {
                    <p-button label="Fetch" icon="pi pi-cloud-download" [loading]="fetching()" [disabled]="!raw().siteId || !fetchNo || !fetchDate" (onClick)="fetch()" pTooltip="Runs by itself once the invoice no. and date are in" />
                  }
                  <p-button label="Clear" [text]="true" severity="secondary" (onClick)="clearFetch()" />
                  <span class="muted fetch-note">
                    @if (fetched(); as fi) {
                      {{ fi.docNo }} · {{ fi.custName || fi.custKey }} · open balance <b class="mono">{{ fi.balance | number:'1.2-2' }}</b>@if (hasLine(fi.id)) { · already added }
                      @else if (otherCustomer(fi)) { · <span class="bad">different customer — one receipt covers one customer only</span> }
                    } @else if (fetching()) { Waiting for HO… (about a minute) }
                  </span>
                </div>
                @if (custInvoices().length) {
                  <div class="cust-inv">
                    <div class="sub-title">Synced open invoices of {{ raw().custKey }}</div>
                    <table class="tbl">
                      <thead><tr><th>Invoice</th><th>Date</th><th class="num">Balance</th><th></th></tr></thead>
                      <tbody class="mono">
                        @for (i of custInvoices(); track i.id) {
                          <tr>
                            <td>{{ i.docNo }}</td><td>{{ i.docDate | date:'MM/dd/yyyy' }}</td>
                            <td class="num">{{ i.balance | number:'1.2-2' }}</td>
                            <td><p-button label="Add" [text]="true" size="small" [disabled]="hasLine(i.id)" (onClick)="addInvoice(i)" /></td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }
              }
              <div class="tbl-wrap">
                <table class="tbl">
                  <thead>
                    <tr>
                      <th>Invoice</th><th>Date</th><th>Customer</th><th class="num">Total</th><th class="num">RFC</th>
                      <th class="num">Open<br>balance</th><th class="num">Applied</th><th class="num">Deduction</th><th class="num">Amount</th><th>Ref.</th>
                      <th class="num" [pTooltip]="balanceHint()" tooltipPosition="top">{{ balanceLabel() }}</th>
                      @if (!readOnly()) { <th></th> }
                    </tr>
                  </thead>
                  <tbody class="mono">
                    @for (l of lines(); track l.postedInvoiceId) {
                      <tr>
                        <td>{{ l.docNo }}</td>
                        <td>{{ l.docDate | date:'MM/dd/yyyy' }}</td>
                        <td [pTooltip]="l.custName">{{ l.custKey }}</td>
                        <td class="num">{{ l.total | number:'1.2-2' }}</td>
                        <td class="num" [class.neg]="l.rfcTotal < 0">{{ l.rfcTotal | number:'1.2-2' }}</td>
                        <td class="num">{{ l.available | number:'1.2-2' }}</td>
                        <td class="num strong">
                          @if (readOnly()) { {{ (l.applied ?? 0) | number:'1.2-2' }} }
                          @else { <p-inputnumber [(ngModel)]="l.applied" (ngModelChange)="touchLines()" mode="decimal" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="col-num mono" /> }
                        </td>
                        @if (l.deds[0]; as d) {
                          <ng-container *ngTemplateOutlet="dedCells; context: { $implicit: d, l: l }" />
                        } @else if (readOnly()) {
                          <td class="num nil">—</td><td class="num nil">—</td><td class="nil">—</td>
                        } @else {
                          <td colspan="3" class="sans"><p-button label="Add deduction" icon="pi pi-plus" [text]="true" size="small" (onClick)="addDed(l)" /></td>
                        }
                        <td class="num strong">{{ balanceAfter(l) | number:'1.2-2' }}</td>
                        @if (!readOnly()) { <td><p-button icon="pi pi-trash" [text]="true" severity="danger" pTooltip="Remove invoice" (onClick)="removeLine(l)" /></td> }
                      </tr>
                      @for (d of l.deds.slice(1); track $index) {
                        <tr class="ded-sub"><td colspan="7"></td><ng-container *ngTemplateOutlet="dedCells; context: { $implicit: d, l: l }" /><td></td>@if (!readOnly()) { <td></td> }</tr>
                      }
                      @if (l.deds.length && !readOnly()) {
                        <tr class="ded-sub"><td colspan="7"></td><td colspan="5" class="sans"><p-button label="Add deduction" icon="pi pi-plus" [text]="true" size="small" (onClick)="addDed(l)" /></td></tr>
                      }
                    } @empty {
                      <tr><td colspan="12" class="sans muted">No invoices yet — fetch them above. A collection with no invoice is kept as on-account.</td></tr>
                    }
                  </tbody>
                </table>
              </div>
              <ng-template #dedCells let-d let-l="l">
                @if (readOnly()) {
                  <td class="sans num">{{ dedLabel(d.type) }}</td>
                  <td class="num">{{ (d.amount ?? 0) | number:'1.2-2' }}</td>
                  <td>{{ d.ref || '—' }}</td>
                } @else {
                  <td class="sans num"><p-select [(ngModel)]="d.type" (ngModelChange)="touchLines()" [options]="deductionTypes" optionLabel="label" optionValue="value" placeholder="Type" appendTo="body" /></td>
                  <td class="num"><p-inputnumber [(ngModel)]="d.amount" (ngModelChange)="touchLines()" mode="decimal" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="col-num mono" /></td>
                  <td class="ded-ref"><input pInputText [(ngModel)]="d.ref" maxlength="50" class="col-ref" />
                    <p-button icon="pi pi-times" [text]="true" size="small" severity="secondary" pTooltip="Remove deduction" (onClick)="removeDed(l, d)" /></td>
                }
              </ng-template>
            </section>

            <section class="card remarks">
              <label>Remarks <span class="muted">optional</span></label>
              <textarea pTextarea [formControl]="form.controls.remarks" rows="3" maxlength="500" placeholder="Add a note for the custodian or audit trail"></textarea>
            </section>
          </div>

          <aside class="rv-side">
            <section class="card">
              <div class="card-h">Allocation</div>
              <div class="alloc">
                <div><span>Received</span><span class="mono">{{ raw().amount ?? 0 | number:'1.2-2' }}</span></div>
                <div><span>Applied</span><span class="mono">{{ appliedTotal() | number:'1.2-2' }}</span></div>
                <div><span>Deductions</span><span class="mono">{{ deductionTotal() | number:'1.2-2' }}</span></div>
                <div class="total" [class.bad]="unapplied() < 0"><span>Unapplied (on-account)</span><span class="mono">{{ unapplied() | number:'1.2-2' }}</span></div>
              </div>
              @if (raw().amount) {
                @if (unapplied() === 0) {
                  <div class="note ok"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M5 12l5 5 9-10"></path></svg>Fully allocated</div>
                } @else if (unapplied() > 0) {
                  <div class="note info">{{ unapplied() | number:'1.2-2' }} kept on-account</div>
                } @else {
                  <div class="note err">Applied more than received</div>
                }
              }
            </section>

            <section class="card">
              <div class="card-h between"><span>Receipt soft copy</span><span class="hint">PDF, JPG, PNG · 10 MB</span></div>
              <div class="files">
                @if (col(); as c) {
                  @for (a of c.attachments; track a.id) {
                    <div class="file">
                      <div class="file-ic"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 3H6a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8z"></path><path d="M14 3v5h5"></path></svg></div>
                      <div class="file-txt">
                        <a href="" (click)="$event.preventDefault(); view(a.id)">{{ a.fileName }}</a>
                        <span>{{ a.size / 1024 | number:'1.0-0' }} KB · {{ a.uploadedBy }}</span>
                      </div>
                      @if (c.canEdit) { <p-button icon="pi pi-trash" [text]="true" severity="danger" (onClick)="removeAttachment(a.id)" /> }
                    </div>
                  } @empty { <div class="muted">No file attached yet.</div> }
                  @if (c.canEdit) {
                    <input #file type="file" accept=".pdf,.jpg,.jpeg,.png" multiple hidden (change)="upload(file)" />
                    <button type="button" class="upload" [disabled]="uploading()" (click)="file.click()">{{ uploading() ? 'Uploading…' : 'Upload file' }}</button>
                  }
                } @else if (canEncode) {
                  <!-- New collection: files wait here and are uploaded right after the first Save. -->
                  @for (f of pendingFiles(); track $index) {
                    <div class="file">
                      <div class="file-ic"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 3H6a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8z"></path><path d="M14 3v5h5"></path></svg></div>
                      <div class="file-txt"><span class="strong">{{ f.name }}</span><span>{{ f.size / 1024 | number:'1.0-0' }} KB · attached when you save</span></div>
                      <p-button icon="pi pi-trash" [text]="true" severity="danger" (onClick)="removePending($index)" />
                    </div>
                  } @empty { <div class="muted">No file attached yet.</div> }
                  <input #newFile type="file" accept=".pdf,.jpg,.jpeg,.png" multiple hidden (change)="upload(newFile)" />
                  <button type="button" class="upload" (click)="newFile.click()">Upload file</button>
                }
              </div>
            </section>
          </aside>
        </div>
      </main>
    </div>

    <p-dialog header="Void collection" [(visible)]="voidVisible" [modal]="true" [style]="{ width: '420px' }">
      <textarea pTextarea [(ngModel)]="voidReason" rows="3" class="w-full" placeholder="Reason (required)"></textarea>
      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" (onClick)="voidVisible = false" />
        <p-button label="Void" severity="danger" [disabled]="!voidReason.trim()" [loading]="busy()" (onClick)="doVoid()" />
      </ng-template>
    </p-dialog>

    <p-dialog header="Pull out / Replace PDC" [(visible)]="pulloutVisible" [modal]="true" [style]="{ width: '440px' }">
      <div class="flex gap-3 mb-2">
        <label class="flex align-items-center gap-1"><input type="radio" name="pullout" value="PulledOut" [(ngModel)]="pulloutType" /> Pulled out</label>
        <label class="flex align-items-center gap-1"><input type="radio" name="pullout" value="Replaced" [(ngModel)]="pulloutType" /> Replaced</label>
      </div>
      <textarea pTextarea [(ngModel)]="pulloutReason" rows="3" class="w-full"
        [placeholder]="pulloutType === 'Replaced' ? 'Reason and the replacing check / receipt (required)' : 'Reason (required)'"></textarea>
      <small class="block mt-2 text-color-secondary">
        Its RPDC is cancelled in HOMS too; the PDC is voided once that's done. Encode a replacing check or payment as its own collection.
      </small>
      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" (onClick)="pulloutVisible = false" />
        <p-button [label]="pulloutType === 'Replaced' ? 'Replace' : 'Pull out'" severity="danger" [disabled]="!pulloutReason.trim()" [loading]="busy()" (onClick)="doPullout()" />
      </ng-template>
    </p-dialog>

    <p-dialog header="Return to branch" [(visible)]="returnVisible" [modal]="true" [style]="{ width: '420px' }">
      <textarea pTextarea [(ngModel)]="returnReason" rows="3" class="w-full" placeholder="What the custodian must fix (required)"></textarea>
      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" (onClick)="returnVisible = false" />
        <p-button label="Return" severity="warn" [disabled]="!returnReason.trim()" [loading]="busy()" (onClick)="doReturn()" />
      </ng-template>
    </p-dialog>

    <p-dialog header="Record PDC deposit" [(visible)]="depositVisible" [modal]="true" [style]="{ width: '420px' }">
      <div class="field"><label>Deposited into (company account)</label>
        <p-select [(ngModel)]="dep.accountId" [options]="depositAccounts()" optionLabel="label" optionValue="id" placeholder="Select account" appendTo="body" /></div>
      <div class="field"><label>Deposit slip ref. no. <span class="muted">(optional)</span></label><input pInputText [(ngModel)]="dep.slip" maxlength="30" /></div>
      <div class="field"><label>Deposit date</label><p-datepicker [(ngModel)]="dep.date" dateFormat="mm/dd/yy" appendTo="body" [showIcon]="true" /></div>
      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" (onClick)="depositVisible = false" />
        <p-button label="Save deposit" [disabled]="!dep.accountId || !dep.date" [loading]="busy()" (onClick)="doDeposit()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    /* Page styles live in styles.scss under .rv (Receipt Verification design). */
    :host { display: block; }
    .ra-pick { cursor: pointer; }
    .ra-pick:hover { background: #e2e8f0; }
  `]
})
export class CollectionPageComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  protected router = inject(Router);
  private api = inject(CollectionService);
  private toolbar = inject(GlobalToolbarService);
  private tabBar = inject(TabBarService);
  private toast = inject(MessageService);
  private auth = inject(AuthService);
  private bank = inject(BankStatementService);

  protected readonly receiptTypes = RECEIPT_TYPES;
  // RFC/BO/RTV only comes from a Remittance Advice Excel.
  protected readonly deductionTypes = DEDUCTION_TYPES.filter(d => d.value !== 'RfcBoRtv');
  protected readonly statusLabel = statusLabel;
  protected readonly sev = statusSeverity;
  protected dedLabel(t: DeductionType | null) { return DEDUCTION_TYPES.find(d => d.value === t)?.label ?? '—'; }

  form = this.fb.group({
    siteId: [null as number | null],
    receiptType: ['CR' as ReceiptType],
    receiptNo: [''],
    receiptDate: [new Date() as Date | null],
    paymentMode: ['BankTransfer' as PaymentMode],
    amount: [null as number | null],
    custKey: [''],
    bank: [''],
    checkNo: [''],
    checkDate: [null as Date | null],
    depAccountId: [null as number | null],
    depSlip: [''],
    depDate: [null as Date | null],
    remarks: [''],
  });

  sites = signal<CollectionSiteDto[]>([]);
  col = signal<CollectionDto | null>(null);
  lines = signal<Line[]>([]);
  /** Fetch bar above the invoice table: one invoice at a time; once HO has synced it, Add replaces Fetch. */
  fetchNo: number | null = null;
  fetchDate: Date | null = null;
  fetched = signal<OpenInvoiceDto | null>(null);
  /** Soft copies picked before the first Save; uploaded right after it. */
  pendingFiles = signal<File[]>([]);
  /** Chain account paying several customers on this receipt: invoices of different customers allowed. */
  multiCustomer = signal(false);
  fetchStatus = signal<InvoiceSyncRequestStatusDto | null>(null);
  custInvoices = signal<OpenInvoiceDto[]>([]);
  apiError = signal<string | null>(null);
  saving = signal(false);
  busy = signal(false);
  fetching = signal(false);
  uploading = signal(false);
  refreshing = signal(false);
  private formTick = signal(0);   // bumps on form/grid edits so computed totals refresh

  /** Finance (verify / manage only) opens saved collections here but never creates one. */
  protected readonly canEncode = this.auth.isAdmin() || this.auth.hasPermission('collections-encode');
  /** Custodian Verifier: may start a Remittance Advice instead of a CR. */
  protected readonly canEncodeRa = this.auth.isAdmin() || this.auth.hasPermission('collections-ra-encode');
  protected readOnly = computed(() => this.col() ? !this.col()!.canEdit : !this.canEncode);
  /** Raw form values (includes disabled controls, which form.value drops in read-only mode). */
  protected raw = computed(() => { this.formTick(); return this.form.getRawValue(); });
  protected modeOptions = computed(() => {
    this.formTick();
    const rt = this.form.controls.receiptType.value;
    return PAYMENT_MODES.filter(m => (rt === 'CR') === (m.value !== 'PostDatedCheck'));
  });
  protected isCheck = computed(() => { this.formTick(); return ['Check', 'PostDatedCheck'].includes(this.form.controls.paymentMode.value ?? ''); });
  protected custName = computed(() => {
    this.formTick();
    const key = this.form.controls.custKey.value;
    return this.lines().find(l => l.custKey === key)?.custName ?? (this.col()?.custKey === key ? this.col()!.custName : '');
  });
  protected appliedTotal = computed(() => { this.formTick(); return this.round(this.lines().reduce((s, l) => s + (l.applied ?? 0), 0)); });
  /** Status tracker (shared with the Collections list preview): see collectionSteps. */
  protected steps = computed<Step[]>(() => { const c = this.col(); return c ? collectionSteps(c) : []; });

  /** Invoice balance after this receipt (live while editing): open balance − applied − deductions. A voided receipt
   *  or bounced PDC doesn't count, so its invoices keep their open balance. */
  protected balanceAfter(l: { available: number; applied: number | null; deds: { type: DeductionType | null; amount: number | null }[] }) {
    const c = this.col();
    if (c && (c.verificationStatus === 'Voided' || c.pdcOutcome === 'Bounced')) return l.available;
    return this.round(l.available - (l.applied ?? 0) - l.deds.reduce((t, d) => t + (d.type ? d.amount ?? 0 : 0), 0));
  }
  /** "Current Balance" once the receipt is Deposited (on the bank statement), else "Balance To Be". */
  protected balanceLabel() { return this.col()?.paymentStatus === 'Deposited' ? 'Current Balance' : 'Balance To Be'; }
  protected balanceHint() {
    return this.col()?.paymentStatus === 'Deposited'
      ? 'Invoice balance after this receipt — its deposit is on the bank statement'
      : 'Invoice balance once this receipt goes through — not yet deposited';
  }

  protected deductionTotal = computed(() => { this.formTick(); return this.round(this.lines().reduce((s, l) => s + l.deds.reduce((t, d) => t + (d.type ? d.amount ?? 0 : 0), 0), 0)); });
  protected unapplied = computed(() => { this.formTick(); return this.round((this.form.controls.amount.value ?? 0) - this.appliedTotal()); });

  voidVisible = false;
  voidReason = '';
  pulloutVisible = false;
  pulloutType: 'PulledOut' | 'Replaced' = 'PulledOut';
  pulloutReason = '';
  returnVisible = false;
  returnReason = '';
  depositVisible = false;
  dep = { accountId: null as number | null, slip: '', date: null as Date | null };
  depositAccounts = signal<BankAccountOptionDto[]>([]);
  asOf = signal<string | null>(null);
  private accountsSite: number | null = null;

  private currentTabKey = NEW_KEY;
  private navSub?: Subscription;
  private formSub?: Subscription;
  private pollSub?: Subscription;

  ngOnInit() {
    this.api.mySites().subscribe(res => {
      this.sites.set(res.data ?? []);
      if (!this.form.value.siteId && this.sites().length === 1) this.form.patchValue({ siteId: this.sites()[0].id });
    });
    this.formSub = this.form.valueChanges.subscribe(() => {
      this.formTick.update(n => n + 1);
      this.loadAccounts(this.form.getRawValue().siteId);
    });
    this.bank.asOf().subscribe(res => this.asOf.set(res.data?.asOfDate ?? null));
    this.handleNavState();
    this.navSub = this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => this.handleNavState());
    this.tabBar.registerDirtyChecker(NEW_KEY, () => this.isNewDirty());
    this.syncToolbar();
  }

  ngOnDestroy() {
    if (!this.col() && this.isNewDirty()) {
      this.tabBar.updateTabState(NEW_KEY, { draftForm: this.form.getRawValue(), draftLines: this.lines() });
    }
    this.tabBar.unregisterDirtyChecker(NEW_KEY);
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.toolbar.clear();
    this.navSub?.unsubscribe();
    this.formSub?.unsubscribe();
    this.pollSub?.unsubscribe();
  }

  private isNewDirty() {
    return !this.col() && (!!this.form.value.receiptNo?.trim() || !!this.form.value.amount || this.lines().length > 0 || this.pendingFiles().length > 0);
  }

  private handleNavState() {
    const state = history.state as Record<string, unknown>;
    const id = state?.['collectionId'] as number | undefined;
    if (id) {
      if (id !== this.col()?.id) this.load(id);
      return;
    }
    if (this.col()) this.resetForm();
    const draft = state?.['draftForm'] as Record<string, unknown> | undefined;
    if (draft) {
      // Tab state round-trips through localStorage, so dates come back as strings.
      for (const k of ['receiptDate', 'checkDate', 'depDate']) if (draft[k]) draft[k] = new Date(draft[k] as string);
      this.form.patchValue(draft);
      this.lines.set((state['draftLines'] as Line[]) ?? []);
      this.tabBar.updateTabState(NEW_KEY, undefined);
    }
  }

  private load(id: number) {
    this.refreshing.set(true);
    this.api.get(id).subscribe({
      next: res => { this.refreshing.set(false); if (res.data) this.show(res.data); },
      error: err => { this.refreshing.set(false); this.apiError.set(err?.error?.message ?? 'Could not load the collection.'); }
    });
  }

  /** Puts a saved collection on the form and opens/updates its edit tab. */
  private show(c: CollectionDto) {
    this.col.set(c);
    this.apiError.set(null);
    this.custInvoices.set([]);
    this.form.patchValue({
      siteId: c.siteId, receiptType: c.receiptType, receiptNo: c.receiptNo, receiptDate: this.toDate(c.receiptDate),
      paymentMode: c.paymentMode, amount: c.amount, custKey: c.custKey,
      bank: c.bank ?? '', checkNo: c.checkNo ?? '', checkDate: this.toDate(c.checkDate),
      depAccountId: c.deposit?.bankAccountId ?? null, depSlip: c.deposit?.slipRefNo ?? '', depDate: this.toDate(c.deposit?.depositDate ?? null),
      remarks: c.remarks ?? '',
    }, { emitEvent: true });
    this.multiCustomer.set(c.multiCustomer);
    this.lines.set(c.invoices.map(i => {
      const ded = c.deductions.filter(d => d.postedInvoiceId === i.postedInvoiceId);
      return {
        postedInvoiceId: i.postedInvoiceId, docNo: i.docNo, docDate: i.docDate, custKey: i.custKey, custName: i.custName,
        total: i.total, rfcTotal: i.rfcTotal, available: i.availableBalance, applied: i.amountApplied,
        deds: ded.map(d => ({ type: d.type, amount: d.amount, ref: d.reference ?? '' })),
      };
    }));
    // Branch and type are fixed once saved; everything is fixed once submitted.
    this.form.controls.siteId.disable({ emitEvent: false });
    if (c.canEdit) this.form.enable({ emitEvent: false }); else this.form.disable({ emitEvent: false });
    this.form.controls.siteId.disable({ emitEvent: false });
    this.form.markAsPristine();

    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = `/collections#${c.id}`;
    this.tabBar.openTab({ key: this.currentTabKey, label: `${c.receiptType} ${c.receiptNo}`, route: '/collections', icon: 'pi-wallet', state: { collectionId: c.id } });
    this.tabBar.registerDirtyChecker(this.currentTabKey, () => this.form.dirty);
    this.syncToolbar();
  }

  private resetForm() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.col.set(null);
    this.apiError.set(null);
    this.form.enable({ emitEvent: false });
    this.form.reset({ siteId: this.sites().length === 1 ? this.sites()[0].id : null, receiptType: 'CR', paymentMode: 'BankTransfer', receiptDate: new Date() });
    this.lines.set([]);
    this.clearFetch();
    this.pendingFiles.set([]);
    this.multiCustomer.set(false);
    this.custInvoices.set([]);
    this.pollSub?.unsubscribe();
    this.currentTabKey = NEW_KEY;
    this.tabBar.registerDirtyChecker(NEW_KEY, () => this.isNewDirty());
    this.syncToolbar();
  }

  private syncToolbar() {
    const c = this.col();
    this.toolbar.set({
      title: c ? `Collection — ${c.receiptType} ${c.receiptNo}` : 'New Collection',
      save: this.canEncode ? { onClick: () => this.save(), loading: this.saving, disabled: this.readOnly } : undefined,
      add: this.canEncode ? { onClick: () => this.newCollection() } : undefined,
      refresh: { onClick: () => c ? this.load(c.id) : this.resetForm(), loading: this.refreshing },
      list: { onClick: () => this.router.navigate(['/collections/list']) },
    });
  }

  private newCollection() {
    if (this.col()) this.tabBar.switchToDefaultTab(NEW_KEY);
    else this.resetForm();
  }

  /** Deposit-account picklist of the branch (refetched only when the branch changes). */
  private loadAccounts(siteId: number | null) {
    if (siteId === this.accountsSite) return;
    this.accountsSite = siteId;
    if (!siteId) { this.depositAccounts.set([]); return; }
    this.bank.depositAccounts(siteId).subscribe(res => {
      const list = res.data ?? [];
      // A saved slip keeps showing its account even if it was since unlinked or deactivated.
      const d = this.col()?.deposit;
      if (d?.bankAccountId && !list.some(a => a.id === d.bankAccountId)) list.push({ id: d.bankAccountId, label: d.bank ?? '', accountName: '' });
      this.depositAccounts.set(list);
    });
  }

  onReceiptTypeChange() {
    const rt = this.form.controls.receiptType.value;
    this.form.patchValue({ paymentMode: rt === 'CR' ? 'BankTransfer' : 'PostDatedCheck' });
  }

  // ── fetch invoices (queued for HO) ─────────────────────────────────────

  /** Clear: empty the fetch bar, drop a fetched-but-not-added invoice and stop waiting for HO. */
  clearFetch() {
    this.pollSub?.unsubscribe();
    this.fetching.set(false);
    this.fetchStatus.set(null);
    this.fetched.set(null);
    this.fetchNo = null;
    this.fetchDate = null;
  }

  /** Add: put the fetched invoice on the table, then the bar is ready for the next one. */
  addFetched() {
    const i = this.fetched();
    if (i) this.addInvoice(i);
    this.clearFetch();
  }

  /** Runs by itself once both invoice no. and date are in (and on Fetch): HOMSys first, else HO. */
  autoFetch() {
    if (this.fetchNo && this.fetchDate && !this.fetching() && !this.fetched() && this.form.getRawValue().siteId) this.fetch();
  }

  /**
   * An invoice HOMSys already holds shows at once. Otherwise HO is asked: its watcher reads the SL file and saves the
   * invoice (and its RFCs) into HOMSys, so the next lookup of it is instant.
   */
  fetch() {
    const siteId = this.form.getRawValue().siteId;
    const items = this.fetchNo && this.fetchDate ? [{ invoiceNo: this.fetchNo, invoiceDate: this.iso(this.fetchDate)! }] : [];
    if (!siteId || !items.length) {
      this.toast.add({ severity: 'warn', summary: 'Fetch', detail: 'Choose the branch and enter the invoice no. with its date.' });
      return;
    }
    this.fetched.set(null);
    this.fetching.set(true);
    this.api.invoices(siteId, { docNos: [items[0].invoiceNo] }).subscribe({
      next: res => {
        const known = res.data?.[0];
        if (known) { this.fetching.set(false); this.fetched.set(known); }
        else this.askHo(siteId, items);
      },
      error: () => this.askHo(siteId, items)
    });
  }

  private askHo(siteId: number, items: { invoiceNo: number; invoiceDate: string }[]) {
    this.api.requestInvoices(siteId, items).subscribe({
      next: res => { this.fetchStatus.set(res.data ?? null); this.poll(siteId, res.data!.id); },
      error: err => { this.fetching.set(false); this.toast.add({ severity: 'error', summary: 'Fetch', detail: err?.error?.message ?? 'Request failed.' }); }
    });
  }

  /** Polls every 5 s (max ~5 min) until HO's watcher has answered, then offers the invoice for Add. */
  private poll(siteId: number, requestId: number) {
    this.pollSub?.unsubscribe();
    this.pollSub = interval(5000).pipe(take(60), switchMap(() => this.api.invoiceRequest(requestId))).subscribe({
      next: res => {
        const s = res.data!;
        this.fetchStatus.set(s);
        if (s.status === 'Done' || s.status === 'Failed') {
          this.pollSub?.unsubscribe();
          this.fetching.set(false);
          const found = s.items.filter(i => i.found).map(i => i.invoiceNo);
          if (found.length) this.offerFetched(siteId, found[0]);
          else this.toast.add({ severity: 'warn', summary: 'Fetch', detail: s.message || `Invoice ${s.items[0]?.invoiceNo ?? ''} wasn't found in BMS for that date — check the no. and date.` });
        }
      },
      complete: () => {
        if (this.fetching()) {
          this.fetching.set(false);
          this.toast.add({ severity: 'warn', summary: 'Fetch', detail: 'HO has not answered yet — try Fetch again in a few minutes.' });
        }
      }
    });
  }

  private offerFetched(siteId: number, docNo: number) {
    this.api.invoices(siteId, { docNos: [docNo] }).subscribe(res => this.fetched.set(res.data?.[0] ?? null));
  }

  loadCustomerInvoices() {
    const v = this.form.getRawValue();
    if (!v.siteId || !v.custKey) return;
    this.api.invoices(v.siteId, { custKey: v.custKey.trim(), openOnly: true }).subscribe({
      next: res => {
        this.custInvoices.set(res.data ?? []);
        if (!res.data?.length) this.toast.add({ severity: 'info', summary: 'Invoices', detail: 'No synced open invoices for this customer — fetch them by invoice no.' });
      },
      error: err => this.toast.add({ severity: 'error', summary: 'Invoices', detail: err?.error?.message ?? 'Lookup failed.' })
    });
  }

  hasLine(id: number) { return this.lines().some(l => l.postedInvoiceId === id); }

  /** Adds an invoice, pre-filling Applied with what is left of the amount received (capped at its balance). */
  addInvoice(i: OpenInvoiceDto) {
    if (this.hasLine(i.id)) return;
    if (this.otherCustomer(i)) {
      this.toast.add({ severity: 'warn', summary: 'Invoice', detail: `Invoice ${i.docNo} is for customer ${i.custKey} — one receipt covers one customer only (${this.lines()[0].custKey}). For a chain account paying several, tick Multiple customer payment.` });
      return;
    }
    if (i.balance <= 0) {
      this.toast.add({ severity: 'info', summary: 'Invoice', detail: `Invoice ${i.docNo} has no open balance.` });
      return;
    }
    const left = Math.max(0, this.unapplied());
    this.lines.update(ls => [...ls, {
      postedInvoiceId: i.id, docNo: i.docNo, docDate: i.docDate, custKey: i.custKey, custName: i.custName,
      total: i.total, rfcTotal: i.rfcTotal, available: i.balance,
      applied: this.round(Math.min(left, i.balance)) || null, deds: [],
    }]);
    // The receipt's customer follows its invoices (the first one sets it; a chain payment keeps the first).
    if (this.lines().length === 1 || !this.multiCustomer()) { if (this.form.controls.custKey.value !== i.custKey) this.form.patchValue({ custKey: i.custKey }); }
    this.form.markAsDirty();
  }

  /** One receipt = one customer: true when the receipt already holds another customer's invoices. */
  otherCustomer(i: OpenInvoiceDto) { const first = this.lines()[0]; return !this.multiCustomer() && !!first && first.custKey !== i.custKey; }

  /** Customers on the receipt so far (distinct). */
  customersOnReceipt = computed(() => [...new Set(this.lines().map(l => l.custKey))]);

  setMultiCustomer(on: boolean) {
    if (!on && this.customersOnReceipt().length > 1) {
      this.toast.add({ severity: 'warn', summary: 'Customers', detail: 'This receipt already has invoices of several customers — remove them first.' });
      this.multiCustomer.set(true);
      return;
    }
    this.multiCustomer.set(on);
    this.form.markAsDirty();
  }

  addDed(l: Line) { l.deds = [...l.deds, { type: null, amount: null, ref: '' }]; this.lines.update(ls => [...ls]); this.touchLines(); }
  removeDed(l: Line, d: Ded) { l.deds = l.deds.filter(x => x !== d); this.lines.update(ls => [...ls]); this.touchLines(); }

  removeLine(l: Line) { this.lines.update(ls => ls.filter(x => x !== l)); this.form.markAsDirty(); }
  touchLines() { this.formTick.update(n => n + 1); this.form.markAsDirty(); }

  // ── save / workflow ─────────────────────────────────────────────────────

  save() {
    const v = this.form.getRawValue();
    if (!v.siteId || !v.receiptNo?.trim() || !v.receiptDate || !v.amount || !v.custKey?.trim()) {
      this.toast.add({ severity: 'warn', summary: 'Validation', detail: 'Branch, receipt no., date, amount and customer key are required.' });
      return;
    }
    const hasDeposit = !!(v.depAccountId || v.depSlip?.trim());
    const dto: SaveCollectionDto = {
      siteId: v.siteId, receiptType: v.receiptType!, receiptNo: v.receiptNo.trim(), receiptDate: this.iso(v.receiptDate)!,
      custKey: v.custKey.trim(), paymentMode: v.paymentMode!, amount: v.amount,
      bank: this.isCheck() ? v.bank?.trim() || null : null,
      checkNo: this.isCheck() ? v.checkNo?.trim() || null : null,
      checkDate: this.isCheck() ? this.iso(v.checkDate) : null,
      // A PDC's deposit is recorded separately (Record PDC deposit), keep what is stored.
      deposit: v.receiptType === 'CR'
        ? (hasDeposit ? { bankAccountId: v.depAccountId, slipRefNo: v.depSlip?.trim() ?? '', depositDate: this.iso(v.depDate) ?? '' } : null)
        : this.col()?.deposit ?? null,
      remarks: v.remarks?.trim() || null,
      invoices: this.lines().map(l => ({ postedInvoiceId: l.postedInvoiceId, amountApplied: l.applied ?? 0 })),
      deductions: this.lines().flatMap(l => l.deds.filter(d => d.type && d.amount)
        .map(d => ({ postedInvoiceId: l.postedInvoiceId, type: d.type!, amount: d.amount!, reference: d.ref?.trim() || null }))),
      multiCustomer: this.multiCustomer(),
    };
    if (dto.deposit && (!dto.deposit.bankAccountId || !dto.deposit.depositDate)) {
      this.toast.add({ severity: 'warn', summary: 'Validation', detail: 'Complete the deposit: bank account and date (the slip ref. no. is optional).' });
      return;
    }
    const c = this.col();
    this.saving.set(true);
    this.apiError.set(null);
    (c ? this.api.update(c.id, dto) : this.api.create(dto)).subscribe({
      next: res => {
        this.saving.set(false);
        const pending = c ? [] : this.pendingFiles();
        this.toast.add({ severity: 'success', summary: 'Saved', detail: c ? 'Collection updated.'
          : pending.length ? `Draft saved — attaching ${pending.length} file${pending.length === 1 ? '' : 's'}; submit when ready.` : 'Draft saved — attach the receipt, then submit.' });
        if (!c) this.tabBar.updateTabState(NEW_KEY, undefined);
        this.show(res.data!);
        if (pending.length) { this.pendingFiles.set([]); this.uploadAll(res.data!.id, pending); }
      },
      error: err => this.fail(err, () => this.saving.set(false))
    });
  }

  submit() {
    const c = this.col();
    if (!c) return;
    if (this.form.dirty) {
      this.toast.add({ severity: 'warn', summary: 'Submit', detail: 'Save your changes first.' });
      return;
    }
    this.busy.set(true);
    this.api.submit(c.id).subscribe({
      next: res => { this.busy.set(false); this.toast.add({ severity: 'success', summary: 'Submitted', detail: 'Sent to finance for verification.' }); this.show(res.data!); },
      error: err => this.fail(err, () => this.busy.set(false))
    });
  }

  doVoid() {
    const c = this.col();
    if (!c) return;
    this.busy.set(true);
    this.api.void(c.id, this.voidReason.trim()).subscribe({
      next: res => { this.busy.set(false); this.voidVisible = false; this.voidReason = ''; this.show(res.data!); },
      error: err => this.fail(err, () => this.busy.set(false))
    });
  }

  doPullout() {
    const c = this.col();
    if (!c) return;
    this.busy.set(true);
    this.api.pulloutPdc(c.id, this.pulloutType, this.pulloutReason.trim()).subscribe({
      next: res => {
        this.busy.set(false); this.pulloutVisible = false; this.pulloutReason = '';
        const d = res.data!;
        this.toast.add({ severity: 'success', summary: d.pdcPullout ?? 'PDC',
          detail: d.verificationStatus === 'Voided' ? 'Voided — it was not in HOMS.' : 'Its RPDC is being cancelled in HOMS.' });
        this.show(d);
      },
      error: err => this.fail(err, () => this.busy.set(false))
    });
  }

  /** Matched / match found (awaiting the verifier) / not yet matched — for the deposit box. */
  protected bankNote = computed(() => {
    const c = this.col(), l = c?.bankLine;
    if (!c) return '';
    if (!l) return c.verificationStatus === 'Draft' || c.verificationStatus === 'Returned' ? 'Not matched — matched once submitted' : 'Not yet matched';
    const what = `${l.txnDate.slice(5, 7)}/${l.txnDate.slice(8, 10)} credit ${l.credit.toLocaleString('en-US', { minimumFractionDigits: 2 })}`;
    return l.confirmed ? `Matched — ${what} (approved by ${l.confirmedBy})` : `Match found — ${what}, awaiting the verifier`;
  });

  protected homsSev(s: string) { return s === 'Done' ? 'success' : s === 'Held' ? 'warn' : s === 'Failed' ? 'error' : 'info'; }

  retryHoms() {
    const c = this.col();
    if (!c) return;
    this.busy.set(true);
    this.api.retryHoms(c.id).subscribe({
      next: res => { this.busy.set(false); this.toast.add({ severity: 'success', summary: 'HOMS', detail: 'Sent again.' }); this.show(res.data!); },
      error: err => this.fail(err, () => this.busy.set(false))
    });
  }

  matchAction(approve: boolean) {
    const c = this.col();
    if (!c) return;
    this.busy.set(true);
    (approve ? this.api.confirmMatch(c.id) : this.api.rejectMatch(c.id, null)).subscribe({
      next: res => {
        this.busy.set(false);
        this.toast.add({ severity: 'success', summary: 'Bank match', detail: approve
          ? 'Approved — the collection is Deposited' + (c.paymentMode === 'PostDatedCheck' ? '; its JV is queued for HOMS.' : '.')
          : 'Rejected — that bank line is kept out of automatic matching.' });
        this.show(res.data!);
      },
      error: err => this.fail(err, () => this.busy.set(false))
    });
  }

  doVerify() {
    const c = this.col();
    if (!c) return;
    this.busy.set(true);
    this.api.verify(c.id).subscribe({
      next: res => { this.busy.set(false); this.toast.add({ severity: 'success', summary: 'Verified', detail: `${c.receiptType} ${c.receiptNo} verified — queued for HOMS posting.` }); this.show(res.data!); },
      error: err => this.fail(err, () => this.busy.set(false))
    });
  }

  doReturn() {
    const c = this.col();
    if (!c) return;
    this.busy.set(true);
    this.api.returnToBranch(c.id, this.returnReason.trim()).subscribe({
      next: res => { this.busy.set(false); this.returnVisible = false; this.returnReason = ''; this.show(res.data!); },
      error: err => this.fail(err, () => this.busy.set(false))
    });
  }

  openDeposit() {
    this.dep = { accountId: this.depositAccounts().length === 1 ? this.depositAccounts()[0].id : null, slip: '', date: new Date() };
    this.depositVisible = true;
  }

  doDeposit() {
    const c = this.col();
    if (!c || !this.dep.date) return;
    this.busy.set(true);
    this.api.recordDeposit(c.id, { bankAccountId: this.dep.accountId, slipRefNo: this.dep.slip.trim(), depositDate: this.iso(this.dep.date)! }).subscribe({
      next: res => { this.busy.set(false); this.depositVisible = false; this.show(res.data!); },
      error: err => this.fail(err, () => this.busy.set(false))
    });
  }

  // ── attachments ─────────────────────────────────────────────────────────

  /** Picked files: uploaded now on a saved collection, else kept until the first Save uploads them. */
  upload(input: HTMLInputElement) {
    const files = Array.from(input.files ?? []);
    input.value = '';
    const ok = files.filter(f => {
      const good = /\.(pdf|jpe?g|png)$/i.test(f.name) && f.size <= 10 * 1024 * 1024;
      if (!good) this.toast.add({ severity: 'warn', summary: 'Attachment', detail: `${f.name}: PDF, JPG or PNG up to 10 MB only.` });
      return good;
    });
    if (!ok.length) return;
    const c = this.col();
    if (c) this.uploadAll(c.id, ok);
    else this.pendingFiles.update(p => [...p, ...ok]);
  }

  removePending(i: number) { this.pendingFiles.update(p => p.filter((_, x) => x !== i)); }

  /** Uploads one file after another (each reply carries the refreshed collection). */
  private uploadAll(collectionId: number, files: File[]) {
    this.uploading.set(true);
    from(files).pipe(concatMap(f => this.api.addAttachment(collectionId, f))).subscribe({
      next: res => this.show(res.data!),
      complete: () => this.uploading.set(false),
      error: err => this.fail(err, () => this.uploading.set(false))
    });
  }

  removeAttachment(id: number) {
    const c = this.col();
    if (!c) return;
    this.api.deleteAttachment(c.id, id).subscribe({ next: res => this.show(res.data!), error: err => this.fail(err) });
  }

  view(id: number) {
    const c = this.col();
    if (!c) return;
    this.api.attachment(c.id, id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    });
  }

  // ── helpers ─────────────────────────────────────────────────────────────

  private fail(err: { error?: { message?: string }; status?: number }, done?: () => void) {
    done?.();
    const msg = err?.error?.message ?? `Error ${err?.status ?? ''}: please try again.`;
    this.apiError.set(msg);
    this.toast.add({ severity: 'error', summary: 'Error', detail: msg });
  }

  private round(n: number) { return Math.round(n * 100) / 100; }

  /** Local calendar date -> 'yyyy-MM-dd' (no UTC shift). */
  private iso(d: Date | null | undefined): string | null {
    if (!d) return null;
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
  }

  private toDate(s: string | null | undefined): Date | null {
    if (!s) return null;
    const [y, m, d] = s.split('-').map(Number);
    return new Date(y, m - 1, d);
  }
}
