import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AutoCompleteModule, AutoCompleteCompleteEvent } from 'primeng/autocomplete';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { SalesOrderService } from '../../core/services/sales-order.service';
import { PricelistService, PricelistPreviewResult } from '../../core/services/pricelist.service';
import { CustomerSuggestionDto } from '../../core/models/sales-order.model';

@Component({
  selector: 'app-pricelist-export-page',
  standalone: true,
  imports: [FormsModule, AutoCompleteModule, CheckboxModule, DatePickerModule, InputNumberModule, ButtonModule, MessageModule, TableModule, InputTextModule],
  template: `
    <div class="report-card">
      @if (apiError()) {
        <p-message severity="error" styleClass="w-full mb-3">
          <span>{{ apiError() }}</span>
        </p-message>
      }

      <div class="form-row" [hidden]="isFullscreen()">
        <div class="field customers-field" [class.expanded]="customersExpanded()" [class.collapsed]="customersCollapsed()">
          <label>Customers <span class="required-star">*</span>
            @if (selectedCustomers().length > 0) {
              <i class="pi collapse-toggle" [class.pi-chevron-down]="!customersCollapsed()" [class.pi-chevron-up]="customersCollapsed()"
                 (click)="toggleCustomersCollapsed()" [title]="customersCollapsed() ? 'Expand' : 'Collapse'"></i>
            }
          </label>
          <p-autoComplete [(ngModel)]="selectedCustomers" [suggestions]="customerSuggestions()"
                 field="cusName" [multiple]="true" [dropdown]="true" [minLength]="2"
                 [delay]="1000" [appendTo]="'body'" placeholder="Search customer key or name"
                 (completeMethod)="searchCustomers($event)" (onChange)="clearPreview()">
            <ng-template let-item pTemplate="item">
              <div class="autocomplete-item" (click)="toggleCustomer(item); $event.stopPropagation()">
                <p-checkbox [binary]="true" [ngModel]="isSelected(item)" (click)="$event.stopPropagation()" (onChange)="toggleCustomer(item)" />
                <span>{{ item.custKey }} - {{ item.cusName }}</span>
              </div>
            </ng-template>
            <ng-template let-item pTemplate="selectedItem">
              <span class="cust-chip" [style.background]="chipColor(item).bg" [style.border-color]="chipColor(item).border"
                    [style.color]="chipColor(item).text" [title]="item.custKey + ' - ' + item.cusName">
                <span class="cust-chip-label">{{ item.custKey }} - {{ item.cusName }}</span>
                <i class="pi pi-times-circle cust-chip-remove" (click)="toggleCustomer(item); $event.stopPropagation()"></i>
              </span>
            </ng-template>
          </p-autoComplete>
        </div>

        <div class="field date-field">
          <label>Effectivity Date <span class="required-star">*</span></label>
          <p-datepicker [(ngModel)]="effectivityDate" dateFormat="mm/dd/yy" [appendTo]="'body'" (onSelect)="clearPreview()" />
        </div>

        <div class="field srp-field">
          <label>SRP Markup %</label>
          <p-inputnumber [(ngModel)]="srpMarkupPercent" mode="decimal" [minFractionDigits]="0" [maxFractionDigits]="2" [min]="0"
                 (onInput)="clearPreview()" />
        </div>

        <div class="field">
          <label>&nbsp;</label>
          <p-button label="Generate" icon="pi pi-table" [loading]="generating()"
                 [disabled]="selectedCustomers().length === 0 || !effectivityDate()"
                 (onClick)="generate()" />
        </div>
      </div>

      @if (previewResult(); as result) {
        <div class="results-section" [class.fullscreen]="isFullscreen()">
          <div class="table-toolbar">
            <span class="p-input-icon-left filter-field">
              <i class="pi pi-search"></i>
              <input type="text" pInputText placeholder="Filter by SKU or description"
                     [ngModel]="filterText()" (ngModelChange)="filterText.set($event)" />
            </span>
            <i class="pi fullscreen-toggle" [class.pi-window-maximize]="!isFullscreen()" [class.pi-window-minimize]="isFullscreen()"
               (click)="toggleFullscreen()" [title]="isFullscreen() ? 'Exit full screen' : 'Full screen'"></i>
          </div>

          <p-table [value]="filteredRows()" styleClass="p-datatable-sm pricelist-table"
                 [scrollable]="true" [scrollHeight]="isFullscreen() ? 'calc(100vh - 7rem)' : '65vh'">
            <ng-template pTemplate="header">
              <tr>
                <th>SKU</th>
                <th>Description</th>
                <th>Category</th>
                <th>Packing</th>
                <th>Pieces</th>
                <th>Case Barcode</th>
                <th>Barcode</th>
                @for (cust of result.customers; track cust.custKey) {
                  <th colspan="3" class="cust-header">({{ cust.custKey }}) {{ cust.cusName }}</th>
                }
              </tr>
              <tr>
                <th colspan="7"></th>
                @for (cust of result.customers; track cust.custKey) {
                  <th>Case w/ VAT</th>
                  <th>Unit w/ VAT</th>
                  <th>SRP</th>
                }
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-row>
              <tr>
                <td>{{ row.cProdNo }}</td>
                <td>{{ row.prodDesc }}</td>
                <td>{{ row.category }}</td>
                <td>{{ row.packSize }}</td>
                <td>{{ row.pieces }}</td>
                <td>{{ row.caseBarcode }}</td>
                <td>{{ row.barcode }}</td>
                @for (cust of result.customers; track cust.custKey) {
                  <td>{{ formatValue(row.byCustKey[cust.custKey]?.casePriceWithVat) }}</td>
                  <td>{{ formatValue(row.byCustKey[cust.custKey]?.unitPriceWithVat) }}</td>
                  <td>{{ formatValue(row.byCustKey[cust.custKey]?.srp) }}</td>
                }
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr><td [attr.colspan]="7 + result.customers.length * 3">No products found.</td></tr>
            </ng-template>
          </p-table>
        </div>
      }
    </div>
  `,
  styles: [`
    .report-card { font-size: 0.85rem; }
    .form-row { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: flex-end; }
    .field { display: flex; flex-direction: column; gap: 0.2rem; min-width: 220px; }
    .field label { font-weight: 500; font-size: 0.75rem; }
    .required-star { color: var(--p-red-500, #ef4444); margin-left: 2px; }

    .customers-field { max-width: 500px; transition: max-width 0.15s ease; }
    .customers-field.expanded { flex: 1 1 100%; max-width: 100%; }
    .customers-field ::ng-deep .p-autocomplete { width: 100%; }
    .customers-field ::ng-deep .p-autocomplete-input-multiple { flex-wrap: wrap; padding: 0.25rem; }
    .customers-field ::ng-deep .p-autocomplete-chip-item { display: inline-flex; max-width: 220px; margin: 2px; }
    .cust-chip {
      display: inline-flex; align-items: center; gap: 0.4rem; max-width: 220px;
      padding: 0.2rem 0.6rem; border-radius: 1rem; border: 1px solid; font-size: 0.78rem;
    }
    .cust-chip-label { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .cust-chip-remove { cursor: pointer; font-size: 0.75rem; flex-shrink: 0; }

    .customers-field .collapse-toggle { cursor: pointer; margin-left: 0.35rem; color: var(--p-text-muted-color, #888); font-size: 0.75rem; }
    .customers-field.collapsed ::ng-deep .p-autocomplete-input-multiple { max-height: 2.6rem; overflow-y: auto; }

    .report-card { min-width: 0; }
    .results-section { min-width: 0; width: 100%; }
    ::ng-deep .pricelist-table.p-datatable { display: block; width: 100%; min-width: 0; }
    ::ng-deep .pricelist-table .p-datatable-table-container { width: 100%; overflow-x: auto; }

    .results-section.fullscreen {
      position: fixed; inset: 0; z-index: 1000;
      background: var(--p-content-background, #fff);
      padding: 1rem 1.25rem; display: flex; flex-direction: column; overflow: auto;
    }

    .date-field { min-width: 0; }
    .date-field ::ng-deep .p-datepicker-input { width: 7rem; font-size: 0.85rem; }
    .srp-field { min-width: 0; }
    .srp-field ::ng-deep .p-inputnumber-input { width: 3.5rem; font-size: 0.85rem; }

    .autocomplete-item { display: flex; align-items: center; gap: 0.5rem; max-width: 320px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; cursor: pointer; }
    .table-toolbar { display: flex; align-items: center; gap: 0.75rem; margin-top: 1rem; margin-bottom: 0.5rem; }
    .fullscreen-toggle { cursor: pointer; margin-left: auto; font-size: 1rem; color: var(--p-text-muted-color, #888); }
    .filter-field { position: relative; display: inline-block; }
    .filter-field .pi-search { position: absolute; left: 0.6rem; top: 50%; transform: translateY(-50%); color: var(--p-text-muted-color, #888); font-size: 0.75rem; }
    .filter-field input { padding-left: 1.9rem; width: 300px; font-size: 0.8rem; }
    ::ng-deep .pricelist-table .p-datatable-table { min-width: 900px; font-size: 0.78rem; }
    ::ng-deep .pricelist-table .p-datatable-thead > tr > th { position: sticky; top: 0; z-index: 1; padding: 0.4rem 0.5rem; }
    ::ng-deep .pricelist-table .p-datatable-tbody > tr > td { padding: 0.3rem 0.5rem; }
    .cust-header { text-align: center; }
  `]
})
export class PricelistExportPageComponent implements OnInit, OnDestroy {
  private api = inject(SalesOrderService);
  private pricelistApi = inject(PricelistService);
  private toolbar = inject(GlobalToolbarService);

  customerSuggestions = signal<CustomerSuggestionDto[]>([]);
  selectedCustomers = signal<CustomerSuggestionDto[]>([]);
  customersExpanded = computed(() => this.selectedCustomers().length >= 2);
  customersCollapsed = signal(false);
  isFullscreen = signal(false);
  effectivityDate = signal<Date>(new Date());
  srpMarkupPercent = signal<number>(3);
  generating = signal(false);
  exporting = signal(false);
  apiError = signal<string | null>(null);
  previewResult = signal<PricelistPreviewResult | null>(null);
  filterText = signal('');
  private flatRows = computed(() => {
    const result = this.previewResult();
    if (!result) return [];
    return result.groups.flatMap(group =>
      group.rows.map(row => ({ ...row, category: group.header ?? '' })));
  });
  filteredRows = computed(() => {
    const term = this.filterText().trim().toLowerCase();
    const rows = this.flatRows();
    if (!term) return rows;
    return rows.filter(row =>
      row.cProdNo?.toLowerCase().includes(term) ||
      row.prodDesc?.toLowerCase().includes(term) ||
      row.category?.toLowerCase().includes(term));
  });

  private exportDisabled = computed(() => !this.previewResult() || this.exporting());

  ngOnInit(): void {
    this.toolbar.set({
      title: 'Pricelist Export',
      export: {
        items: [
          { label: 'Export Per Comparison', icon: 'pi pi-file-excel', command: () => this.exportToExcel() },
          { label: 'Export Per Account',    icon: 'pi pi-file-excel', command: () => this.exportPerAccount() }
        ],
        disabled: this.exportDisabled
      }
    });
  }

  ngOnDestroy(): void {
    this.toolbar.clear();
  }

  // Keyed by sorted lowercase keywords so "Puregold Isabela" and "Isabela Puregold"
  // share a cache entry, and repeated/backspaced-then-retyped terms skip the API call.
  private customerSearchCache = new Map<string, CustomerSuggestionDto[]>();

  private cacheKey(term: string): string {
    return term.toLowerCase().split(' ').filter(Boolean).sort().join(' ');
  }

  // Chips are colored by which search term found the customer: the first search
  // ("puregold") gets a color, and re-searching that same term later reuses it.
  private lastSearchTerm = '';
  private custKeyTerm = new Map<string, string>();
  private termColors = new Map<string, { bg: string; border: string; text: string }>();
  private static readonly CHIP_PALETTE = [
    { bg: '#e8f5e9', border: '#81c784', text: '#1b5e20' },
    { bg: '#e3f2fd', border: '#64b5f6', text: '#0d47a1' },
    { bg: '#fff3e0', border: '#ffb74d', text: '#e65100' },
    { bg: '#f3e5f5', border: '#ba68c8', text: '#4a148c' },
    { bg: '#fce4ec', border: '#f06292', text: '#880e4f' },
    { bg: '#e0f7fa', border: '#4dd0e1', text: '#006064' },
    { bg: '#fff9c4', border: '#dce775', text: '#827717' },
  ];
  private readonly defaultChipColor = { bg: '#eceff1', border: '#b0bec5', text: '#37474f' };

  chipColor(item: CustomerSuggestionDto): { bg: string; border: string; text: string } {
    const term = this.custKeyTerm.get(item.custKey);
    if (!term) return this.defaultChipColor;
    return this.termColors.get(term) ?? this.defaultChipColor;
  }

  searchCustomers(event: AutoCompleteCompleteEvent): void {
    const term = (event.query ?? '').trim();
    if (!term) { this.customerSuggestions.set([]); return; }
    const key = this.cacheKey(term);
    this.lastSearchTerm = key;
    if (!this.termColors.has(key)) {
      this.termColors.set(key, PricelistExportPageComponent.CHIP_PALETTE[this.termColors.size % PricelistExportPageComponent.CHIP_PALETTE.length]);
    }
    const cached = this.customerSearchCache.get(key);
    if (cached) { this.customerSuggestions.set(cached); return; }
    this.api.searchCustomers(term).subscribe({
      next: res => {
        const data = res.data ?? [];
        this.customerSearchCache.set(key, data);
        this.customerSuggestions.set(data);
      },
      error: () => this.customerSuggestions.set([])
    });
  }

  isSelected(item: CustomerSuggestionDto): boolean {
    return this.selectedCustomers().some(c => c.custKey === item.custKey);
  }

  toggleCustomersCollapsed(): void {
    this.customersCollapsed.update(v => !v);
  }

  toggleFullscreen(): void {
    this.isFullscreen.update(v => !v);
  }

  toggleCustomer(item: CustomerSuggestionDto): void {
    const current = this.selectedCustomers();
    if (this.isSelected(item)) {
      this.selectedCustomers.set(current.filter(c => c.custKey !== item.custKey));
    } else {
      if (this.lastSearchTerm) this.custKeyTerm.set(item.custKey, this.lastSearchTerm);
      this.selectedCustomers.set([...current, item]);
    }
    this.clearPreview();
  }

  clearPreview(): void {
    this.previewResult.set(null);
    this.filterText.set('');
  }

  formatValue(value: number | null | undefined): string {
    return value == null ? '—' : value.toFixed(2);
  }

  generate(): void {
    const customers = this.selectedCustomers();
    const date = this.effectivityDate();
    if (customers.length === 0 || !date) return;

    this.apiError.set(null);
    this.previewResult.set(null);
    this.generating.set(true);

    this.pricelistApi.preview(this.buildRequest(customers, date)).subscribe({
      next: res => {
        this.previewResult.set(res.data);
        this.generating.set(false);
      },
      error: () => {
        this.apiError.set('Failed to generate the pricelist preview.');
        this.generating.set(false);
      }
    });
  }

  exportToExcel(): void {
    const customers = this.selectedCustomers();
    const date = this.effectivityDate();
    if (customers.length === 0 || !date) return;

    this.apiError.set(null);
    this.exporting.set(true);

    const effectivityDate = this.toIsoDate(date);
    this.pricelistApi.export(this.buildRequest(customers, date)).subscribe({
      next: res => {
        this.pricelistApi.download(res, `Pricelist_${effectivityDate.replace(/-/g, '')}.xlsx`);
        this.exporting.set(false);
      },
      error: () => {
        this.apiError.set('Failed to generate the pricelist export.');
        this.exporting.set(false);
      }
    });
  }

  exportPerAccount(): void {
    const customers = this.selectedCustomers();
    const date = this.effectivityDate();
    if (customers.length === 0 || !date) return;

    this.apiError.set(null);
    this.exporting.set(true);

    const effectivityDate = this.toIsoDate(date);
    this.downloadNextAccount(customers, 0, effectivityDate);
  }

  private downloadNextAccount(customers: CustomerSuggestionDto[], index: number, effectivityDate: string): void {
    if (index >= customers.length) {
      this.exporting.set(false);
      return;
    }

    const customer = customers[index];
    this.pricelistApi.export({
      custKeys: [customer.custKey],
      effectivityDate,
      srpMarkupPercent: this.srpMarkupPercent() ?? 3
    }).subscribe({
      next: res => {
        this.pricelistApi.download(res, `Pricelist_${customer.custKey}_${effectivityDate.replace(/-/g, '')}.xlsx`);
        this.downloadNextAccount(customers, index + 1, effectivityDate);
      },
      error: () => {
        this.apiError.set(`Failed to generate the export for ${customer.custKey}.`);
        this.downloadNextAccount(customers, index + 1, effectivityDate);
      }
    });
  }

  private buildRequest(customers: CustomerSuggestionDto[], date: Date) {
    return {
      custKeys: customers.map(c => c.custKey),
      effectivityDate: this.toIsoDate(date),
      srpMarkupPercent: this.srpMarkupPercent() ?? 3
    };
  }

  private toIsoDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
