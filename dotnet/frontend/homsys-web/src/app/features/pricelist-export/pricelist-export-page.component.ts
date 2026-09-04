import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AutoCompleteModule, AutoCompleteCompleteEvent } from 'primeng/autocomplete';
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
  imports: [FormsModule, AutoCompleteModule, DatePickerModule, InputNumberModule, ButtonModule, MessageModule, TableModule, InputTextModule],
  template: `
    <div class="report-card">
      @if (apiError()) {
        <p-message severity="error" styleClass="w-full mb-3">
          <span>{{ apiError() }}</span>
        </p-message>
      }

      <div class="form-row">
        <div class="field">
          <label>Customers <span class="required-star">*</span></label>
          <p-autoComplete [(ngModel)]="selectedCustomers" [suggestions]="customerSuggestions()"
                 field="cusName" [multiple]="true" [dropdown]="true" [minLength]="2"
                 [appendTo]="'body'" placeholder="Search customer key or name"
                 (completeMethod)="searchCustomers($event)" (onChange)="clearPreview()">
            <ng-template let-item pTemplate="item">
              <div class="autocomplete-item">{{ item.custKey }} - {{ item.cusName }}</div>
            </ng-template>
          </p-autoComplete>
        </div>

        <div class="field">
          <label>Effectivity Date <span class="required-star">*</span></label>
          <p-datepicker [(ngModel)]="effectivityDate" dateFormat="mm/dd/yy" [appendTo]="'body'" (onSelect)="clearPreview()" />
        </div>

        <div class="field">
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
        <div class="table-toolbar">
          <span class="p-input-icon-left filter-field">
            <i class="pi pi-search"></i>
            <input type="text" pInputText placeholder="Filter by SKU or description"
                   [ngModel]="filterText()" (ngModelChange)="filterText.set($event)" />
          </span>
        </div>

        <p-table [value]="filteredRows()" styleClass="p-datatable-sm pricelist-table"
               [scrollable]="true" scrollHeight="65vh">
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
      }
    </div>
  `,
  styles: [`
    .form-row { display: flex; flex-wrap: wrap; gap: 1rem; align-items: flex-end; }
    .field { display: flex; flex-direction: column; gap: 0.25rem; min-width: 220px; }
    .field label { font-weight: 500; font-size: 0.8rem; }
    .required-star { color: var(--p-red-500, #ef4444); margin-left: 2px; }
    .autocomplete-item { max-width: 320px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .table-toolbar { margin-top: 1.25rem; margin-bottom: 0.75rem; }
    .filter-field { position: relative; display: inline-block; }
    .filter-field .pi-search { position: absolute; left: 0.6rem; top: 50%; transform: translateY(-50%); color: var(--p-text-muted-color, #888); font-size: 0.8rem; }
    .filter-field input { padding-left: 1.9rem; width: 300px; }
    ::ng-deep .pricelist-table .p-datatable-table { min-width: 900px; }
    ::ng-deep .pricelist-table .p-datatable-thead > tr > th { position: sticky; top: 0; z-index: 1; }
    .cust-header { text-align: center; }
  `]
})
export class PricelistExportPageComponent implements OnInit, OnDestroy {
  private api = inject(SalesOrderService);
  private pricelistApi = inject(PricelistService);
  private toolbar = inject(GlobalToolbarService);

  customerSuggestions = signal<CustomerSuggestionDto[]>([]);
  selectedCustomers = signal<CustomerSuggestionDto[]>([]);
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

  searchCustomers(event: AutoCompleteCompleteEvent): void {
    const term = (event.query ?? '').trim();
    if (!term) { this.customerSuggestions.set([]); return; }
    this.api.searchCustomers(term).subscribe({
      next: res => this.customerSuggestions.set(res.data ?? []),
      error: () => this.customerSuggestions.set([])
    });
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
