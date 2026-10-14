import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { DrawerModule } from 'primeng/drawer';
import { MessageModule } from 'primeng/message';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { ExportColumn, ExportService } from '../../core/services/export.service';
import { PriceHistoryRow, ProductCatalogItem, ProductCatalogService } from '../../core/services/product-catalog.service';
import { utc } from '../../core/utils/utc-date';

type StatusView = 'active' | 'inactive' | 'all';

const STATUS_SEVERITY: Record<string, 'success' | 'warn' | 'danger'> = {
  'Active': 'success', 'Phased Out': 'warn', 'Delisted': 'danger',
};

const money = (v: unknown) => v == null ? '' : Number(v).toFixed(2);

const EXPORT_COLS: ExportColumn[] = [
  { header: 'ProdNo',        field: 'cProdNo' },
  { header: 'Description',   field: 'prodDesc' },
  { header: 'Pack Size',     field: 'packSize' },
  { header: 'Pcs/Case',      field: 'pieces' },
  { header: 'LSP/Case',      field: 'newPrice', formatter: money },
  { header: 'SRP',           field: 'srp', formatter: money },
  { header: 'Price From',    field: 'priceFrom' },
  { header: 'Product Category', field: 'productCategory' },
  { header: 'Category',      field: 'category' },
  { header: 'Segment',       field: 'groupDesc' },
  { header: 'Sub-Category',  field: 'subCat' },
  { header: 'Brand',         field: 'brand' },
  { header: 'Status',        field: 'status' },
];

/** Products — read-only view of the product master (BMS PROD4WIN, synced). */
@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [FormsModule, DatePipe, DecimalPipe, TableModule, TagModule, SelectModule, SelectButtonModule, DrawerModule, MessageModule],
  template: `
    <div class="filters">
      <p-select [options]="groups()" [ngModel]="group()" (ngModelChange)="group.set($event)"
        placeholder="All segments" [showClear]="true" [filter]="true" styleClass="group-sel" />
      <p-selectButton [options]="statusViews" [ngModel]="statusView()" (ngModelChange)="statusView.set($event)"
        optionLabel="label" optionValue="value" [allowEmpty]="false" />
      <span class="count">{{ filtered().length }} of {{ all().length }} items</span>
    </div>

    <p-table [value]="filtered()" [loading]="loading()" [paginator]="true" [rows]="25"
      [rowsPerPageOptions]="[25, 50, 100, 500]" selectionMode="single" [(selection)]="selected" dataKey="id"
      styleClass="p-datatable-sm" [scrollable]="true">
      <ng-template pTemplate="header">
        <tr>
          <th pSortableColumn="prodNo" style="width:6rem">ProdNo <p-sortIcon field="prodNo" /></th>
          <th pSortableColumn="prodDesc">Description <p-sortIcon field="prodDesc" /></th>
          <th pSortableColumn="packSize">Pack Size <p-sortIcon field="packSize" /></th>
          <th pSortableColumn="pieces" class="num">Pcs/Case <p-sortIcon field="pieces" /></th>
          <th pSortableColumn="newPrice" class="num">LSP/Case <p-sortIcon field="newPrice" /></th>
          <th pSortableColumn="srp" class="num">SRP <p-sortIcon field="srp" /></th>
          <th pSortableColumn="priceFrom">Price From <p-sortIcon field="priceFrom" /></th>
          <th pSortableColumn="productCategory">Product Category <p-sortIcon field="productCategory" /></th>
          <th pSortableColumn="category" style="width:6rem">Category <p-sortIcon field="category" /></th>
          <th pSortableColumn="groupDesc">Segment <p-sortIcon field="groupDesc" /></th>
          <th pSortableColumn="subCat">Sub-Category <p-sortIcon field="subCat" /></th>
          <th pSortableColumn="brand">Brand <p-sortIcon field="brand" /></th>
          <th pSortableColumn="status">Status <p-sortIcon field="status" /></th>
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-p>
        <tr [pSelectableRow]="p" (click)="open(p)" class="clickable">
          <td>{{ p.cProdNo }}</td>
          <td>{{ p.prodDesc }}</td>
          <td>{{ p.packSize }}</td>
          <td class="num">{{ p.pieces }}</td>
          <td class="num">{{ p.newPrice | number:'1.2-2' }}</td>
          <td class="num">{{ p.srp | number:'1.2-2' }}</td>
          <td>{{ p.priceFrom | date:'MM/dd/yyyy' }}</td>
          <td>{{ p.productCategory || '—' }}</td>
          <td>{{ p.category || '—' }}</td>
          <td>{{ p.groupDesc || '—' }}</td>
          <td>{{ p.subCat || '—' }}</td>
          <td>{{ p.brand || '—' }}</td>
          <td><p-tag [value]="p.status" [severity]="sev(p.status)" /></td>
        </tr>
      </ng-template>
      <ng-template pTemplate="emptymessage">
        <tr><td colspan="13" class="text-center">No products match.</td></tr>
      </ng-template>
    </p-table>

    <p-drawer [(visible)]="drawerVisible" position="right" [style]="{ width: '420px' }"
      [header]="current() ? current()!.cProdNo + ' — ' + current()!.prodDesc : ''">
      @if (current(); as p) {
        <dl class="details">
          <dt>Status</dt><dd><p-tag [value]="p.status" [severity]="sev(p.status)" /></dd>
          <dt>Pack Size</dt><dd>{{ p.packSize }} @if (p.um) { ({{ p.um }}) }</dd>
          <dt>Pieces / Case</dt><dd>{{ p.pieces }}</dd>
          <dt>LSP / Case</dt><dd>{{ p.newPrice | number:'1.2-2' }} <span class="sub">ex-VAT</span></dd>
          <dt>Price From</dt><dd>{{ (p.priceFrom | date:'MMM d, yyyy') || '—' }}</dd>
          <dt>Previous Price</dt><dd>{{ (p.oldPrice1 | number:'1.2-2') || '—' }}</dd>
          <dt>SRP</dt><dd>{{ (p.srp | number:'1.2-2') || '—' }}</dd>
          <dt>Tax Rate</dt><dd>{{ p.taxRate }}%</dd>
          <dt>Product Category</dt><dd>{{ p.productCategory || '—' }}</dd>
          <dt>Segment</dt><dd>{{ p.groupDesc || '—' }}</dd>
          <dt>Sub-category</dt><dd>{{ p.subCat || '—' }} <span class="sub">({{ p.category }})</span></dd>
          <dt>Brand</dt><dd>{{ p.brand || '—' }}@if (p.sBrand) { / {{ p.sBrand }} }</dd>
          <dt>Barcode</dt><dd>{{ p.barcode || '—' }}</dd>
          <dt>Case Barcode</dt><dd>{{ p.caseBarcode || '—' }}</dd>
          <dt>Last Synced</dt><dd>{{ utc(p.importedAt) | date:'MMM d, yyyy h:mm a' }}</dd>
        </dl>

        <h4>Price History <span class="sub">(with VAT)</span></h4>
        @if (historyLoading()) {
          <div class="sub">Loading…</div>
        } @else if (!history().length) {
          <div class="sub">No price history on file.</div>
        } @else {
          <table class="hist">
            <thead><tr><th>Effective</th><th class="num">Price w/ VAT</th></tr></thead>
            <tbody>
              @for (h of history(); track $index) {
                <tr><td>{{ (h.effective | date:'MMM d, yyyy') || '—' }}</td><td class="num">{{ h.npAfVat | number:'1.2-2' }}</td></tr>
              }
            </tbody>
          </table>
        }
        <p-message severity="secondary" styleClass="w-full mt-3"
          text="Read only — products are maintained in BMS and synced into HOMSys." />
      }
    </p-drawer>
  `,
  styles: [`
    :host { display: block; }
    .filters { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 0.6rem; }
    :host ::ng-deep .group-sel { min-width: 240px; }
    .count { margin-left: auto; font-size: 0.8rem; color: var(--p-text-muted-color); }
    .num { text-align: right; }
    .clickable { cursor: pointer; }
    .sub { font-size: 0.75rem; color: var(--p-text-muted-color); }
    .details { display: grid; grid-template-columns: 8.5rem 1fr; gap: 0.35rem 0.75rem; margin: 0 0 1rem; font-size: 0.85rem; }
    .details dt { color: var(--p-text-muted-color); }
    .details dd { margin: 0; }
    h4 { margin: 0.5rem 0; font-size: 0.9rem; color: #800000; }
    .hist { width: 100%; border-collapse: collapse; font-size: 0.83rem; }
    .hist th { text-align: left; border-bottom: 1px solid var(--p-surface-border); padding: 0.3rem; }
    .hist td { padding: 0.25rem 0.3rem; border-bottom: 1px solid var(--p-surface-border); }
  `]
})
export class ProductsPageComponent implements OnInit, OnDestroy {
  private api       = inject(ProductCatalogService);
  private toolbar   = inject(GlobalToolbarService);
  private exportSvc = inject(ExportService);

  protected readonly utc = utc;
  protected readonly statusViews = [
    { label: 'Active', value: 'active' },
    { label: 'Phased Out / Delisted', value: 'inactive' },
    { label: 'All', value: 'all' },
  ];

  protected all            = signal<ProductCatalogItem[]>([]);
  protected loading        = signal(true);
  protected searchTerm     = signal('');
  protected group          = signal<string | null>(null);
  protected statusView     = signal<StatusView>('active');
  protected current        = signal<ProductCatalogItem | null>(null);
  protected history        = signal<PriceHistoryRow[]>([]);
  protected historyLoading = signal(false);
  protected selected: ProductCatalogItem | null = null;
  protected drawerVisible = false;

  protected groups = computed(() =>
    [...new Set(this.all().map(p => p.groupDesc).filter((g): g is string => !!g))].sort());

  protected filtered = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const tokens = term ? term.split(/\s+/) : [];
    const g = this.group();
    const v = this.statusView();
    return this.all().filter(p =>
      (v === 'all' || (v === 'active') === (p.status === 'Active')) &&
      (!g || p.groupDesc === g) &&
      tokens.every(t =>
        p.cProdNo.toLowerCase().includes(t) || p.prodDesc.toLowerCase().includes(t) ||
        p.packSize.toLowerCase().includes(t) || p.brand.toLowerCase().includes(t) ||
        (p.groupDesc ?? '').toLowerCase().includes(t) || (p.subCat ?? '').toLowerCase().includes(t) || p.category.toLowerCase().includes(t) || (p.productCategory ?? '').toLowerCase().includes(t) || p.barcode.includes(t)));
  });

  ngOnInit() {
    this.load();
    this.toolbar.set({
      title:   'Products',
      refresh: { onClick: () => this.load(), loading: this.loading },
      search:  term => this.searchTerm.set(term),
      print:   { onClick: () => window.print() },
      export:  { pdf: () => this.export('pdf'), excel: () => this.export('excel'), csv: () => this.export('csv') },
    });
  }

  ngOnDestroy() {
    this.toolbar.clear();
  }

  private load() {
    this.loading.set(true);
    this.api.getAll().subscribe({
      next: res => { this.all.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  sev(status: string) { return STATUS_SEVERITY[status] ?? 'info'; }

  open(p: ProductCatalogItem) {
    this.current.set(p);
    this.drawerVisible = true;
    this.history.set([]);
    this.historyLoading.set(true);
    this.api.getPriceHistory(p.prodNo).subscribe({
      next: res => { this.history.set(res.data ?? []); this.historyLoading.set(false); },
      error: () => this.historyLoading.set(false)
    });
  }

  private export(kind: 'pdf' | 'excel' | 'csv') {
    const rows = this.filtered() as unknown as Record<string, unknown>[];
    if (kind === 'pdf') this.exportSvc.exportPdf('Products', EXPORT_COLS, rows);
    else if (kind === 'excel') this.exportSvc.exportExcel('Products', EXPORT_COLS, rows);
    else this.exportSvc.exportCsv('Products', EXPORT_COLS, rows);
  }
}
