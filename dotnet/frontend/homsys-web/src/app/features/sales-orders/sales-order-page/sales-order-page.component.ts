import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { FormBuilder, ReactiveFormsModule, FormsModule, Validators } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DatePickerModule } from 'primeng/datepicker';
import { AutoCompleteModule, AutoCompleteCompleteEvent, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { ToastModule } from 'primeng/toast';
import { DividerModule } from 'primeng/divider';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { SelectModule } from 'primeng/select';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { SalesOrderService } from '../../../core/services/sales-order.service';
import {
  CustomerLookupDto,
  CustomerSuggestionDto,
  ProductSuggestionDto,
  EncodeLine,
  CreateSalesOrderDto,
  SalesOrderDto,
  DocClassDto,
  ImportedOrderDraft
} from '../../../core/models/sales-order.model';

/**
 * Sales order encoding. Mirrors the legacy VFP form a11102.SCX:
 *
 *   encode customer -> encode PO# -> encode prodno -> save
 *
 * Behaviour deliberately carried over:
 *  - a duplicate PO number WARNS and keeps the value (never blocks)
 *  - lines with a blank product code are dropped on save, not rejected
 *  - O.R. fields appear only for cash customers (term = 0)
 *
 * Deliberately NOT carried over: order limits, AR aging, blockinv/TIN gates,
 * blocked-SKU removal, GetMax. See legacy/vfp/ANALYSIS.md.
 */
@Component({
  selector: 'app-sales-order-page',
  standalone: true,
  providers: [MessageService, ConfirmationService],
  imports: [
    ReactiveFormsModule, FormsModule, InputTextModule, InputNumberModule, DatePickerModule,
    AutoCompleteModule, ButtonModule, MessageModule, ToastModule, DividerModule, ConfirmDialogModule, SelectModule,
    DecimalPipe
  ],
  template: `
    <p-toast position="top-right" />
    <p-confirmDialog />

    <div class="form-card">
      @if (apiError()) {
        <p-message severity="error" styleClass="w-full mb-3">
          <span>{{ apiError() }}</span>
        </p-message>
      }

      <form [formGroup]="form" autocomplete="off">
      <div class="two-col">
      <div class="left-panel">

        <!-- ── encode customer ─────────────────────────────────────────── -->
        <div class="field-row" style="grid-template-columns: 85px 1fr 48px;">
          <div class="field" style="max-width: 85px">
            <label>Customer Key <span class="required-star">*</span></label>
            <p-autoComplete formControlName="custKey" styleClass="w-full" [inputStyle]="{ width: '100%' }"
                   [suggestions]="customerSuggestions()" field="custKey" [dropdown]="true" [minLength]="2"
                   [appendTo]="'body'" placeholder="e.g. 0100123"
                   (completeMethod)="searchCustomers($event)"
                   (onSelect)="onCustomerSelect($event)"
                   (onBlur)="onCustomerKeyBlur()"
                   (keydown.enter)="$event.preventDefault(); lookupCustomer()">
              <ng-template let-item pTemplate="item">
                <div class="autocomplete-item">{{ item.custKey }} - {{ item.cusName }}</div>
              </ng-template>
            </p-autoComplete>
            @if (customerNotFound()) {
              <small class="field-error">Customer not found.</small>
            }
          </div>
          <div class="field" style="min-width: 0;">
            <label>
              Customer Name
              @if (customer(); as c) {
                <span class="term-badge" [class.term-cash]="c.isCash">{{ c.isCash ? 'CASH' : 'TERM' }}</span>
              }
            </label>
            <p-autoComplete [ngModel]="customer()?.cusName ?? ''" [ngModelOptions]="{ standalone: true }"
                   styleClass="w-full" [inputStyle]="{ width: '100%' }"
                   [suggestions]="customerSuggestions()" field="cusName" [dropdown]="true" [minLength]="2"
                   [appendTo]="'body'" [disabled]="viewOnly()"
                   (completeMethod)="searchCustomers($event)"
                   (onSelect)="onCustomerSelect($event)">
              <ng-template let-item pTemplate="item">
                <div class="autocomplete-item">{{ item.custKey }} - {{ item.cusName }}</div>
              </ng-template>
            </p-autoComplete>
          </div>
          <div class="field">
            <label>Term</label>
            <input pInputText type="number" class="w-full" [disabled]="true"
                   [value]="customer() ? customer()!.term : ''" />
          </div>
        </div>

        @if (customer(); as c) {
          <div class="field">
            <label>Ship To</label>
            <input pInputText class="w-full" [disabled]="true"
                   [value]="(c.shipToLn1 + ' ' + c.shipToLn2).trim()" />
          </div>
        }

        <p-divider />

        <!-- ── encode PO# ──────────────────────────────────────────────── -->
        <div class="field-row" style="grid-template-columns: 1fr 1fr 1fr;">
          <div class="field">
            <label>PO No.</label>
            <input pInputText formControlName="poNum" class="w-full" maxlength="15"
                   (blur)="checkPoNumber()" />
            @if (poWarning()) {
              <small class="field-warning">{{ poWarning() }}</small>
            }
          </div>
          <div class="field">
            <label>PO Date</label>
            <p-datepicker formControlName="poDate" dateFormat="mm/dd/yy" appendTo="body"
                          [showIcon]="true" styleClass="w-full" [inputStyle]="{ width: '100%' }" />
          </div>
          <div class="field">
            <label>Cancel Date</label>
            <p-datepicker formControlName="cancelDate" dateFormat="mm/dd/yy" appendTo="body"
                          [showIcon]="true" styleClass="w-full" [inputStyle]="{ width: '100%' }" />
          </div>
        </div>

        <div class="field-row" style="grid-template-columns: 1fr;">
          <div class="field">
            <label>Remarks</label>
            <input pInputText formControlName="invRem" class="w-full" maxlength="100" />
          </div>
        </div>

        <p-divider />

        <!-- ── Payment ──────────────────────────────────────────────────── -->
        <label class="section-label">Payment</label>
        <div class="field-row">
          <div class="field" style="max-width: 260px">
            <label>Document Classification</label>
            <p-select formControlName="docClass" [options]="docClasses()" optionLabel="description"
                      optionValue="code" [showClear]="true" placeholder="Regular Transaction"
                      styleClass="w-full" />
          </div>
        </div>

        @if (orDetailsVisible()) {
          <label class="section-label">O.R. Details</label>
          <div class="field-row">
            <div class="field">
              <label>OR Date</label>
              <p-datepicker formControlName="chkDate" dateFormat="mm/dd/yy" appendTo="body" [showIcon]="true" styleClass="w-full" />
            </div>
            <div class="field">
              <label>OR Number</label>
              <input pInputText type="number" formControlName="orNo" class="w-full" />
            </div>
          </div>
          <div class="field-row">
            <div class="field" style="max-width: 220px">
              <label>OR Amount</label>
              <p-inputNumber formControlName="orAmt" mode="decimal" [minFractionDigits]="2" [maxFractionDigits]="2"
                             styleClass="w-full" [inputStyle]="{ width: '100%' }" />
            </div>
          </div>
        }

      </div>

      <div class="right-panel">
        <!-- ── encode prodno ───────────────────────────────────────────── -->
        <label class="section-label">Products</label>
        <div class="products-h-scroll">
        <div class="products-scroll">
        <table class="so-grid">
          <thead>
            <tr>
              <th style="width: 70px">Prodno</th>
              <th>Product Description</th>
              <th style="width: 72px">Pack</th>
              <th style="width: 60px">Price/Cs</th>
              <th style="width: 52px">Qty CS</th>
              @if (invoiced()) {
                <th style="width: 52px">INV CS</th>
              }
              <th style="width: 62px">LP/VAT</th>
              <th style="width: 26px"></th>
            </tr>
          </thead>
          <tbody>
            @for (line of lines(); track $index) {
              <tr [class.line-missing]="line.notFound">
                <td>
                  <p-autoComplete [ngModel]="line.cProdNo" [ngModelOptions]="{ standalone: true }"
                         styleClass="w-full" [inputStyle]="{ width: '100%' }" appendTo="body"
                         [suggestions]="productSuggestions()" field="cProdNo" [dropdown]="true" [minLength]="0"
                         [disabled]="viewOnly() || !productsEnabled()"
                         (ngModelChange)="onProdNoTyped($index, $event)"
                         (completeMethod)="searchProducts($event)"
                         (onSelect)="onProductSelect($index, $event)"
                         (onBlur)="lookupProduct($index, line.cProdNo)"
                         (keydown.enter)="$event.preventDefault(); lookupProduct($index, line.cProdNo)">
                    <ng-template let-item pTemplate="item">
                      <div>{{ item.cProdNo }} - {{ item.prodDesc }} ({{ item.pieces }} x {{ item.packSize }})</div>
                    </ng-template>
                  </p-autoComplete>
                </td>
                <td [class.text-red]="!line.priceList && !!line.cProdNo">
                  <p-autoComplete [ngModel]="line.notFound ? 'Product not found' : line.prodDesc"
                         [ngModelOptions]="{ standalone: true }"
                         styleClass="w-full" [inputStyle]="{ width: '100%' }" appendTo="body"
                         [suggestions]="productSuggestions()" field="prodDesc" [dropdown]="true" [minLength]="0"
                         [disabled]="viewOnly() || !productsEnabled()"
                         (completeMethod)="searchProducts($event)"
                         (onSelect)="onProductSelect($index, $event)">
                    <ng-template let-item pTemplate="item">
                      <div>{{ item.cProdNo }} - {{ item.prodDesc }} ({{ item.pieces }} x {{ item.packSize }})</div>
                    </ng-template>
                  </p-autoComplete>
                </td>
                <td>{{ line.pieces }} x {{ line.packSize }}</td>
                <td class="text-right">
                  {{ line.pricePerCase !== null ? (line.pricePerCase | number: '1.2-2') : '' }}
                </td>
                <td>
                  <input pInputText type="number" min="0" class="w-full" [value]="line.qtyCs"
                         [disabled]="viewOnly() || !productsEnabled()"
                         (input)="setQty($index, $any($event.target).value)" />
                </td>
                @if (invoiced()) {
                  <td class="text-right">{{ line.allocatedQtyCs }}</td>
                }
                <td class="text-right">
                  {{ lpWithVat(line) !== null ? (lpWithVat(line) | number: '1.2-2') : '' }}
                </td>
                <td>
                  @if (!viewOnly() && productsEnabled()) {
                    <p-button icon="pi pi-times" severity="danger" [text]="true"
                              (onClick)="removeLine($index)" />
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
        </div>

        @if (!viewOnly() && productsEnabled()) {
          <p-button label="Add Line" icon="pi pi-plus" [text]="true" styleClass="add-line-btn" (onClick)="addLine()" />
        }

        <div class="order-summary">
          <div class="summary-line">
            <span class="summary-cell summary-title" style="flex: 1">Totals:</span>
            <span class="summary-cell" style="width: 72px"></span>
            <span class="summary-cell" style="width: 60px"></span>
            <span class="summary-cell summary-col-left" style="width: 52px">
              <span class="summary-value">{{ totalCasesEncoded() }}</span>
              @if (!invoiced()) {
                <small class="summary-note">** Discounts not included **</small>
              }
            </span>
            @if (invoiced()) {
              <span class="summary-cell summary-col-left" style="width: 52px">
                <span class="summary-value">{{ totalAllocatedQtyCs() }}</span>
                <small class="summary-note summary-note-invoiced">** Actual price in invoice **</small>
              </span>
            }
            <span class="summary-cell summary-col-left" style="width: 62px">
              <span class="summary-value">{{ totalAmountLpWithVat() | number: '1.2-2' }}</span>
            </span>
            <span class="summary-cell" style="width: 26px"></span>
          </div>
        </div>
        </div>
      </div>
      </div>
      </form>
    </div>
  `,
  styles: [`
    :host { display: block; height: 100%; min-height: 0; }
    .form-card { background: var(--p-surface-card); border-radius: 8px; padding: 0.3rem 0.75rem; width: 100%; margin: 0 auto; height: 100%; min-height: 0; display: flex; flex-direction: column; }
    form { display: flex; flex-direction: column; flex: 1 1 auto; min-height: 0; }
    .two-col { display: grid; grid-template-columns: minmax(320px, 420px) minmax(0, 1fr); grid-template-rows: minmax(0, 1fr); gap: 0.75rem; align-items: stretch; flex: 1 1 auto; min-height: 0; margin-bottom: 0.4rem; }
    .left-panel { min-width: 0; min-height: 0; max-width: 640px; overflow-y: auto; border: 1px solid var(--p-content-border-color); border-radius: 6px; padding: 0.5rem; }
    .right-panel { min-width: 0; min-height: 0; max-width: 100%; height: 100%; display: flex; flex-direction: column; border: 1px solid var(--p-content-border-color); border-radius: 6px; padding: 0.5rem; }
    .products-h-scroll { min-width: 0; }

    /* ── Mobile — stack the two-column layout, scroll the products table sideways ── */
    @media (max-width: 840px) {
      .form-card { overflow-y: auto; }
      .two-col { grid-template-columns: 1fr; grid-template-rows: auto; }
      .left-panel, .right-panel { max-width: 100%; height: auto; overflow-y: visible; }
      .products-scroll { max-height: 45vh; overflow-x: hidden; }
      .products-h-scroll { overflow-x: auto; }
      .products-h-scroll .so-grid,
      .products-h-scroll .summary-line { min-width: 640px; }
    }
    ::ng-deep .form-card .p-inputtext,
    ::ng-deep .form-card .p-autocomplete-input,
    ::ng-deep .form-card .p-select-label,
    ::ng-deep .form-card .p-datepicker-input,
    ::ng-deep .form-card .p-inputnumber-input {
      padding: 0.18rem 0.4rem;
      font-size: 0.7rem;
      min-height: 1.6rem;
    }
    ::ng-deep .form-card .p-select-dropdown,
    ::ng-deep .form-card .p-autocomplete-dropdown,
    ::ng-deep .form-card .p-datepicker-input-icon-container {
      width: 1.6rem;
    }
    .autocomplete-item {
      max-width: 320px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    ::ng-deep .so-grid .p-autocomplete,
    ::ng-deep .so-grid .p-autocomplete-input,
    .so-grid input { min-width: 0; width: 100%; box-sizing: border-box; }
    .products-scroll { flex: 1 1 auto; min-height: 0; overflow-y: auto; overflow-x: hidden; scrollbar-gutter: stable; border: 1px solid var(--p-content-border-color); border-radius: 6px; margin-bottom: .3rem; }
    .products-scroll .so-grid { margin-bottom: 0; }
    .products-scroll thead th { position: sticky; top: 0; background: #ffffff; z-index: 1; }
    .field { display: flex; flex-direction: column; gap: 0.1rem; margin-bottom: 0.3rem; }
    .field label { font-weight: 500; font-size: 0.7rem; white-space: nowrap; }
    .field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.4rem; }
    .required-star { color: var(--p-red-500, #ef4444); margin-left: 2px; }
    .field-error { color: var(--p-red-500, #ef4444); font-size: 0.65rem; }
    .so-grid { width: 100%; table-layout: fixed; border-collapse: separate; border-spacing: 0; margin-bottom: .3rem; }
    .so-grid th, .so-grid td { padding: .1rem .2rem; border-bottom: 1px solid var(--p-content-border-color); text-align: left; font-size: .68rem; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
    .so-grid th { font-weight: 600; }
    .so-grid td > * { max-width: 100%; }
    .text-red { color: #d32f2f; }
    .text-right { text-align: right; }
    .line-missing { background: #fff4f4; }
    .field-warning { color: #b26a00; font-size: .65rem; }
    .section-label { font-weight: 600; font-size: .76rem; display: block; margin-bottom: .2rem; }
    .order-summary {
      flex: 0 0 auto;
      margin-top: .3rem;
      padding-top: .3rem;
      padding-right: 17px;
      border-top: 1px solid var(--p-content-border-color);
      display: flex;
      flex-direction: column;
      gap: .15rem;
    }
    .summary-line { display: flex; align-items: baseline; gap: 0; font-size: .7rem; }
    .summary-cell { flex: 0 0 auto; min-width: 0; box-sizing: border-box; padding: .1rem .25rem; }
    .summary-title { font-weight: 600; text-align: left; }
    .summary-col { display: flex; flex-direction: column; align-items: flex-end; text-align: right; }
    .summary-col-left { display: flex; flex-direction: column; align-items: flex-start; text-align: left; padding-left: .5rem; }
    .summary-value { font-weight: 600; }
    .summary-note { display: block; color: #b26a00; font-size: .64rem; white-space: nowrap; padding-top: 10px; }
    .summary-note-invoiced { color: #2e7d32; }

    ::ng-deep .add-line-btn.p-button-text {
      height: auto;
      min-height: 0;
      padding: .1rem .4rem;
      font-size: .7rem;
      gap: .25rem;
    }
    ::ng-deep .add-line-btn .p-button-icon { font-size: .68rem; }
    ::ng-deep .add-line-btn .p-button-label { font-size: .7rem; }
    .term-badge {
      display: inline-block; margin-left: .2rem; padding: 1px 4px; border-radius: 3px;
      font-size: .56rem; font-weight: 700; letter-spacing: .1px;
      background: #eef1f5; color: #55606b;
    }
    .term-badge.term-cash { background: #e3f4e9; color: #1e7d3c; }
  `]
})
export class SalesOrderPageComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private api = inject(SalesOrderService);
  private toolbar = inject(GlobalToolbarService);
  private tabBar = inject(TabBarService);
  private toast = inject(MessageService);
  private confirm = inject(ConfirmationService);

  form = this.fb.group({
    custKey: ['', Validators.required],
    poNum: [{ value: '', disabled: true }],
    poDate: [{ value: null as Date | null, disabled: true }],
    cancelDate: [{ value: null as Date | null, disabled: true }],
    invRem: [{ value: '', disabled: true }],
    docClass: [{ value: null as string | null, disabled: true }],
    orNo: [{ value: null as number | null, disabled: true }],
    chkDate: [{ value: null as Date | null, disabled: true }],
    orAmt: [{ value: null as number | null, disabled: true }]
  });

  docClasses = signal<DocClassDto[]>([]);
  customer = signal<CustomerLookupDto | null>(null);
  customerNotFound = signal(false);
  poWarning = signal<string | null>(null);
  apiError = signal<string | null>(null);
  saving = signal(false);
  lines = signal<EncodeLine[]>([this.blankLine()]);

  /** Raw form values (including disabled controls) — used to gate the products section. */
  protected formRaw = signal(this.form.getRawValue());

  /** PO No. and PO Date must both be supplied before the products section unlocks. */
  productsEnabled = computed(() => {
    const raw = this.formRaw();
    return !!this.customer() && !!(raw.poNum ?? '').trim() && !!raw.poDate;
  });

  /** null while encoding a new order; set once loaded for View/Edit. */
  soId = signal<number | null>(null);
  viewOnly = signal(false);

  /** null until loaded for View/Edit and the order has been invoiced (order.invNo). */
  invNo = signal<number | null>(null);
  /** True once the order has a BMS INV# — switches Qty CS/LP w/ VAT to the real invoiced values. */
  invoiced = computed(() => this.invNo() !== null);

  /** set while a staged (not-yet-saved) draft opened from an Excel import is active. */
  draftKey = signal<string | null>(null);

  private currentTabKey = '/sales-orders';
  private navSub?: Subscription;
  private originalPoNum = '';
  /** Carried from an import draft so Save can record it on the persisted SalesOrder. Null for manually encoded orders. */
  private sourceFileHash: string | null = null;
  private sourceFileName: string | null = null;

  /** Start of the encoding session — the legacy form records this for its speed metric. */
  private startedAt = new Date();

  canSave = computed(() =>
    !this.viewOnly() && !!this.customer() && this.lines().some(l => l.cProdNo.trim().length > 0));

  private saveDisabled = computed(() => !this.canSave() || this.saving());

  totalCasesEncoded = computed(() => this.lines().reduce((sum, l) => sum + (l.qtyCs || 0), 0));

  totalAllocatedQtyCs = computed(() => this.lines().reduce((sum, l) => sum + (l.allocatedQtyCs || 0), 0));

  totalAmountLpWithVat = computed(() =>
    this.lines().reduce((sum, l) => sum + (this.lpWithVat(l) ?? 0), 0));

  /** O.R. Details apply only to cash customers with a non-"Regular Transaction" doc classification. */
  orDetailsVisible = computed(() => !!this.customer()?.isCash && !!this.formRaw().docClass);

  private toolbarTitle = computed(() => {
    if (this.viewOnly()) return `View Sales Order — ${this.soId()}`;
    if (this.soId() !== null) return `Edit Sales Order — ${this.soId()}`;
    return 'Sales Order Encoding';
  });

  constructor() {
    /** PO No. / PO Date / Remarks / Document Classification unlock once a valid customer is on the order. */
    effect(() => {
      if (this.viewOnly()) return;
      const ctrls = [
        this.form.controls.poNum, this.form.controls.poDate, this.form.controls.cancelDate,
        this.form.controls.invRem, this.form.controls.docClass
      ];
      if (this.customer()) {
        ctrls.forEach(c => c.enable({ emitEvent: false }));
      } else {
        ctrls.forEach(c => c.disable({ emitEvent: false }));
      }

      // O.R. Details only apply to cash customers with a non-"Regular Transaction" doc classification.
      const orCtrls = [this.form.controls.orNo, this.form.controls.chkDate, this.form.controls.orAmt];
      if (this.orDetailsVisible()) {
        orCtrls.forEach(c => c.enable({ emitEvent: false }));
      } else {
        orCtrls.forEach(c => {
          c.disable({ emitEvent: false });
          c.setValue(null, { emitEvent: false });
        });
      }
    });
  }

  ngOnInit(): void {
    this.handleNavState();
    this.navSub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe(() => this.handleNavState());
    this.form.valueChanges.subscribe(() => this.formRaw.set(this.form.getRawValue()));
    this.syncToolbar();
    this.tabBar.registerDirtyChecker('/sales-orders', () => this.isNewFormDirty());

    this.api.getDocClasses().subscribe({
      next: res => this.docClasses.set(res.data ?? []),
      error: () => this.docClasses.set([])
    });
  }

  private isNewFormDirty(): boolean {
    if (this.soId() !== null || this.draftKey() !== null) return false;
    const v = this.form.value;
    return !!((v.custKey ?? '').trim() || (v.poNum ?? '').trim() ||
      this.lines().some(l => l.cProdNo.trim().length > 0));
  }

  ngOnDestroy(): void {
    this.toolbar.clear();
    this.navSub?.unsubscribe();
    this.tabBar.unregisterDirtyChecker('/sales-orders');
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
  }

  private syncToolbar(): void {
    this.toolbar.set({
      title: this.toolbarTitle(),
      save: this.viewOnly() ? undefined : { onClick: () => this.save(), disabled: this.saveDisabled, loading: this.saving },
      cancel: this.viewOnly() ? undefined : { onClick: () => this.cancel() },
      list: { onClick: () => this.router.navigate(['/sales-orders/list']) }
    });
  }

  private handleNavState(): void {
    const state = history.state as Record<string, unknown>;
    const soId = state?.['soId'] as number | undefined;
    const mode = state?.['mode'] as 'view' | 'edit' | undefined;
    const draftKey = state?.['draftKey'] as string | undefined;
    const draftOrder = state?.['draftOrder'] as ImportedOrderDraft | undefined;

    if (soId) {
      if (soId !== this.soId()) {
        this.loadOrder(soId, mode === 'view');
      }
    } else if (draftKey && draftOrder) {
      if (draftKey !== this.draftKey()) {
        this.applyDraft(draftKey, draftOrder);
      }
    } else if (this.soId() !== null || this.draftKey() !== null) {
      this.reset();
    }
  }

  private loadOrder(soId: number, viewOnly: boolean): void {
    this.api.getById(soId).subscribe({
      next: res => {
        const order = res.data;
        if (!order) return;
        this.applyOrder(order, viewOnly);
      }
    });
  }

  private applyOrder(order: SalesOrderDto, viewOnly: boolean): void {
    viewOnly = viewOnly || !!order.invNo || !!order.isLocked;
    this.soId.set(order.soId);
    this.viewOnly.set(viewOnly);
    this.invNo.set(order.invNo ?? null);
    this.originalPoNum = order.poNum;

    this.form.reset({
      custKey: order.custKey,
      poNum: order.poNum,
      poDate: order.poDate ? new Date(order.poDate) : null,
      cancelDate: order.cancelDate ? new Date(order.cancelDate) : null,
      invRem: order.invRem,
      docClass: order.docClass ?? null,
      orNo: order.orNo ?? null,
      chkDate: order.chkDate ? new Date(order.chkDate) : null,
      orAmt: order.orAmt ?? null
    });
    viewOnly ? this.form.disable() : this.form.enable();
    this.formRaw.set(this.form.getRawValue());

    this.lines.set(
      order.lines.length
        ? order.lines.map(l => ({
            cProdNo: l.cProdNo, prodDesc: l.prodDesc, packSize: l.packSize, um: l.um,
            pieces: l.pieces, qtyCs: l.qtyCs, qtyPc: l.qtyPc,
            freeGoods: l.freeGoods, priceList: l.priceList, notFound: false, pricePerCase: null,
            allocatedQtyCs: l.allocatedQtyCs ?? null, invNetAmt: l.invNetAmt ?? null
          }))
        : [this.blankLine()]
    );

    this.lines().forEach((l, i) => { if (l.cProdNo) this.fetchQuote(i, l.cProdNo); });

    this.customerNotFound.set(false);
    this.poWarning.set(null);
    this.apiError.set(null);

    this.api.lookupCustomer(order.custKey).subscribe({
      next: res => this.customer.set(res.data ?? null),
      error: () => this.customer.set(null)
    });

    const soLabel = `SO# ${order.soNo ?? order.soId}`;
    const invLabel = order.invNo ? ` | INV# ${order.invNo}` : '';
    this.tabBar.openTab({
      key: `/sales-orders#${order.soId}`,
      label: `${viewOnly ? 'View' : 'Edit'} — ${soLabel}${invLabel}`,
      route: '/sales-orders',
      icon: 'pi-file-edit',
      state: { soId: order.soId, mode: viewOnly ? 'view' : 'edit' }
    });
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = `/sales-orders#${order.soId}`;
    if (!viewOnly) this.tabBar.registerDirtyChecker(this.currentTabKey, () => this.form.dirty);
    this.syncToolbar();
  }

  /** Pre-fills a new (unsaved) order from an Excel-imported draft. Save still calls api.create(). */
  private applyDraft(draftKey: string, draftOrder: ImportedOrderDraft): void {
    this.soId.set(null);
    this.invNo.set(null);
    this.draftKey.set(draftKey);
    this.viewOnly.set(false);
    this.originalPoNum = draftOrder.poNum;
    this.sourceFileHash = draftOrder.sourceFileHash ?? null;
    this.sourceFileName = draftOrder.sourceFileName ?? null;

    this.form.enable();
    this.form.reset({
      custKey: draftOrder.custKey,
      poNum: draftOrder.poNum,
      poDate: draftOrder.poDate ? new Date(draftOrder.poDate) : null,
      cancelDate: draftOrder.cancelDate ? new Date(draftOrder.cancelDate) : null,
      invRem: draftOrder.remarks ?? '',
      docClass: null,
      orNo: draftOrder.orNo ?? null,
      chkDate: draftOrder.chkDate ? new Date(draftOrder.chkDate) : null,
      orAmt: draftOrder.orAmt ?? null
    });
    this.formRaw.set(this.form.getRawValue());

    this.customerNotFound.set(false);
    this.poWarning.set(null);
    this.apiError.set(null);

    this.lines.set(
      draftOrder.lines.length
        ? draftOrder.lines.map(l => ({ ...this.blankLine(), cProdNo: l.cProdNo, qtyCs: l.qtyCs }))
        : [this.blankLine()]
    );

    this.lookupCustomer();
    this.lines().forEach((l, i) => { if (l.cProdNo) this.lookupProduct(i, l.cProdNo); });

    this.tabBar.openTab({
      key: `/sales-orders#${draftKey}`,
      label: `New — PO ${draftOrder.poNum}`,
      route: '/sales-orders',
      icon: 'pi-file-edit',
      state: { draftKey, draftOrder }
    });
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = `/sales-orders#${draftKey}`;
    // Imported drafts are always unsaved staged data — closing must always confirm discard.
    this.tabBar.registerDirtyChecker(this.currentTabKey, () => true);
    this.syncToolbar();
  }

  private blankLine(): EncodeLine {
    return {
      cProdNo: '', prodDesc: '', packSize: '', um: '',
      pieces: 0, qtyCs: 0, qtyPc: 0,
      freeGoods: false, priceList: true, notFound: false, pricePerCase: null,
      allocatedQtyCs: null, invNetAmt: null
    };
  }

  addLine(): void {
    this.lines.update(ls => [...ls, this.blankLine()]);
  }

  removeLine(index: number): void {
    this.lines.update(ls => ls.length === 1 ? [this.blankLine()] : ls.filter((_, i) => i !== index));
  }

  setQty(index: number, raw: string): void {
    const value = Math.max(0, Number(raw) || 0);
    this.lines.update(ls =>
      ls.map((l, i) => i === index ? { ...l, qtyCs: value } : l));
  }

  /** LP w/ VAT = Price Per Case * encoded Qty CS * 1.12 — recomputed live, no re-quote needed. */
  lpWithVat(line: EncodeLine): number | null {
    if (this.invoiced()) return line.invNetAmt;
    return line.pricePerCase !== null ? line.pricePerCase * line.qtyCs * 1.12 : null;
  }

  customerSuggestions = signal<CustomerSuggestionDto[]>([]);
  productSuggestions = signal<ProductSuggestionDto[]>([]);

  searchCustomers(event: AutoCompleteCompleteEvent): void {
    const term = (event.query ?? '').trim();
    if (!term) { this.customerSuggestions.set([]); return; }
    this.api.searchCustomers(term).subscribe({
      next: res => this.customerSuggestions.set(res.data ?? []),
      error: () => this.customerSuggestions.set([])
    });
  }

  private blurLookupTimer: ReturnType<typeof setTimeout> | null = null;

  onCustomerKeyBlur(): void {
    // clicking a suggestion blurs the input before the click/select event fires,
    // so an immediate lookup here would run against the stale typed text and
    // visibly flicker the panel; defer it and let onCustomerSelect cancel it.
    if (this.blurLookupTimer) { clearTimeout(this.blurLookupTimer); }
    this.blurLookupTimer = setTimeout(() => { this.blurLookupTimer = null; this.lookupCustomer(); }, 200);
  }

  onCustomerSelect(event: AutoCompleteSelectEvent): void {
    if (this.blurLookupTimer) { clearTimeout(this.blurLookupTimer); this.blurLookupTimer = null; }
    const c = event.value as CustomerSuggestionDto;
    this.form.controls.custKey.setValue(c.custKey);
    // optimistic cusName so the Customer Name field doesn't flash/revert while lookupCustomer() resolves
    this.customer.update(cur => ({ ...(cur ?? this.blankCustomer()), custKey: c.custKey, cusName: c.cusName }));
    this.lookupCustomer();
  }

  private blankCustomer(): CustomerLookupDto {
    return {
      custKey: '', cusName: '', cKey: '', whseNo: 0, custWhse: 0,
      term: 0, termDays: 0, salesman: 0, csMan: '', shipToLn1: '', shipToLn2: '',
      delArea: '', vatId: '', tpc: false, offshore: false, exBranch: false, cCode: 0, isCash: false
    };
  }

  searchProducts(event: AutoCompleteCompleteEvent): void {
    const term = (event.query ?? '').trim();
    this.api.searchProducts(term).subscribe({
      next: res => this.productSuggestions.set(res.data ?? []),
      error: () => this.productSuggestions.set([])
    });
  }

  onProdNoTyped(index: number, value: string): void {
    this.lines.update(ls => ls.map((l, i) => i === index ? { ...l, cProdNo: value } : l));
  }

  onProductSelect(index: number, event: AutoCompleteSelectEvent): void {
    const p = event.value as ProductSuggestionDto;
    this.lines.update(ls => ls.map((l, i) => i === index ? { ...l, cProdNo: p.cProdNo } : l));
    this.lookupProduct(index, p.cProdNo);
  }

  lookupCustomer(): void {
    const key = (this.form.value.custKey ?? '').trim();
    this.customerNotFound.set(false);

    if (!key) {
      this.customer.set(null);
      return;
    }

    this.api.lookupCustomer(key).subscribe({
      next: res => {
        this.customer.set(res.data ?? null);
        this.apiError.set(null);
        this.lines().forEach((l, i) => { if (l.cProdNo && !l.notFound) this.fetchQuote(i, l.cProdNo); });
      },
      error: () => {
        this.customer.set(null);
        this.customerNotFound.set(true);
      }
    });
  }

  /**
   * Legacy txtPonum.Valid: warn on a PO number already encoded, but keep the
   * value and let the operator continue. This is a messagebox with OK only —
   * not a confirmation, and never a blocking validation.
   */
  checkPoNumber(): void {
    const poNum = (this.form.value.poNum ?? '').trim();
    this.poWarning.set(null);
    if (!poNum) return;

    this.api.checkPo(poNum).subscribe({
      next: res => {
        const check = res.data;
        if (!check?.alreadyEncoded) return;

        this.poWarning.set(check.message);
        this.confirm.confirm({
          header: 'System Validation',
          message: check.message,
          icon: 'pi pi-exclamation-triangle',
          acceptLabel: 'OK',
          rejectVisible: false,
          accept: () => { /* value intentionally kept */ }
        });
      }
    });
  }

  lookupProduct(index: number, raw: string): void {
    const code = (raw ?? '').trim();

    if (!code) {
      this.lines.update(ls => ls.map((l, i) => i === index ? this.blankLine() : l));
      return;
    }

    this.api.lookupProduct(code).subscribe({
      next: res => {
        const p = res.data;
        this.lines.update(ls => ls.map((l, i) => i !== index ? l : {
          ...l,
          cProdNo: p?.cProdNo ?? code,
          prodDesc: p?.prodDesc ?? '',
          packSize: p?.packSize ?? '',
          um: p?.um ?? '',
          pieces: p?.pieces ?? 0,
          priceList: p?.priceList ?? true,
          notFound: false
        }));

        // Keep a blank row at the end so the operator can keep typing.
        if (index === this.lines().length - 1) this.addLine();

        if (p) this.fetchQuote(index, p.cProdNo);
      },
      error: () => {
        this.lines.update(ls => ls.map((l, i) => i !== index ? l : {
          ...this.blankLine(), cProdNo: code, notFound: true
        }));
      }
    });
  }

  /** Display-only Price Per Case — never sent on save. LP w/ VAT is derived from it client-side. */
  private fetchQuote(index: number, cProdNo: string): void {
    const custKey = (this.form.value.custKey ?? '').trim();

    this.api.getQuote(cProdNo, custKey).subscribe({
      next: res => {
        const pricePerCase = res.data?.hasPrice ? (res.data?.pricePerCase ?? null) : null;
        this.lines.update(ls => ls.map((l, i) => i === index ? { ...l, pricePerCase } : l));
      },
      error: () => {
        this.lines.update(ls => ls.map((l, i) => i === index ? { ...l, pricePerCase: null } : l));
      }
    });
  }

  save(): void {
    if (!this.canSave() || this.saving()) return;

    const v = this.form.value;

    const dto: CreateSalesOrderDto = {
      custKey: (v.custKey ?? '').trim(),
      poNum: (v.poNum ?? '').trim(),
      poDate: this.toDateOnly(v.poDate ?? null),
      cancelDate: this.toDateOnly(v.cancelDate ?? null),
      invRem: v.invRem ?? '',
      remarks: '',
      docClass: v.docClass || null,
      orNo: v.orNo ?? null,
      chkDate: this.toDateOnly(v.chkDate ?? null),
      orAmt: v.orAmt ?? null,
      soTymStart: this.startedAt.toISOString(),
      sourceFileHash: this.sourceFileHash,
      sourceFileName: this.sourceFileName,
      // Blank product codes are dropped server-side too; filter here so the
      // trailing empty row never travels.
      lines: this.lines()
        .filter(l => l.cProdNo.trim().length > 0 && !l.notFound)
        .map(l => ({
          cProdNo: l.cProdNo.trim(),
          qtyCs: l.qtyCs,
          qtyPc: l.qtyPc,
          freeGoods: l.freeGoods
        }))
    };

    if (dto.lines.length === 0) {
      this.apiError.set('Enter at least one valid product.');
      return;
    }

    this.saving.set(true);
    this.apiError.set(null);

    const soId = this.soId();
    const request = soId === null ? this.api.create(dto) : this.api.update(soId, dto);

    request.subscribe({
      next: res => {
        this.saving.set(false);
        const order = res.data!;

        if (soId === null) {
          this.toast.add({
            severity: 'success',
            summary: 'Saved',
            detail: `Sales order ${order.soId} has been saved.`
          });
          if (this.currentTabKey !== '/sales-orders') {
            this.tabBar.closeTab(this.currentTabKey);
          } else {
            this.reset();
          }
        } else {
          this.toast.add({
            severity: 'success',
            summary: 'Updated',
            detail: `Sales order ${order.soId} has been updated.`
          });
          this.tabBar.unregisterDirtyChecker(this.currentTabKey);
          this.tabBar.switchToDefaultTab('/sales-orders');
        }
      },
      error: err => {
        this.saving.set(false);
        this.apiError.set(err?.error?.message ?? 'Unable to save the sales order.');
      }
    });
  }

  /** Cancel button — same discard-confirmation as closing the tab via the tab strip. */
  protected cancel(): void {
    const tabKey = this.currentTabKey;
    const doCancel = () => tabKey === '/sales-orders' ? this.reset() : this.tabBar.closeTab(tabKey);

    if (this.tabBar.isTabDirty(tabKey)) {
      this.confirm.confirm({
        message: 'You have unsaved changes. Close this tab anyway?',
        header: 'Unsaved Changes',
        icon: 'pi pi-exclamation-triangle',
        acceptLabel: 'Close Tab',
        rejectLabel: 'Stay',
        acceptButtonStyleClass: 'p-button-danger',
        accept: doCancel
      });
    } else {
      doCancel();
    }
  }

  protected reset(): void {
    this.form.enable();
    this.form.reset({
      custKey: '', poNum: '', invRem: '',
      docClass: null, orNo: null, chkDate: null, orAmt: null
    });
    this.form.controls.poNum.disable({ emitEvent: false });
    this.form.controls.poDate.disable({ emitEvent: false });
    this.form.controls.cancelDate.disable({ emitEvent: false });
    this.form.controls.invRem.disable({ emitEvent: false });
    this.form.controls.docClass.disable({ emitEvent: false });
    this.form.controls.orNo.disable({ emitEvent: false });
    this.form.controls.chkDate.disable({ emitEvent: false });
    this.form.controls.orAmt.disable({ emitEvent: false });
    this.formRaw.set(this.form.getRawValue());
    this.customer.set(null);
    this.customerNotFound.set(false);
    this.poWarning.set(null);
    this.lines.set([this.blankLine()]);
    this.startedAt = new Date();
    this.soId.set(null);
    this.invNo.set(null);
    this.draftKey.set(null);
    this.sourceFileHash = null;
    this.sourceFileName = null;
    this.viewOnly.set(false);
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = '/sales-orders';
    this.tabBar.registerDirtyChecker('/sales-orders', () => this.isNewFormDirty());
    this.syncToolbar();
  }

  /** PrimeNG gives a Date; the API expects a DateOnly (yyyy-MM-dd). */
  private toDateOnly(d: Date | null): string | null {
    if (!d) return null;
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }
}
