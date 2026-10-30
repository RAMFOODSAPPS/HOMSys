import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { DrawerModule } from 'primeng/drawer';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { ExportColumn, ExportService } from '../../core/services/export.service';
import {
  CustomerBranchCount, CustomerMaster, CustomerMasterService, CustomerQuery
} from '../../core/services/customer-master.service';
import { utc } from '../../core/utils/utc-date';

type StatusView = 'active' | 'inactive' | 'all';
const PAGE_SIZE = 25;
const EXPORT_MAX = 20000;

const yesNo = (v: unknown) => v ? 'Yes' : '';
const EXPORT_COLS: ExportColumn[] = [
  { header: 'Branch',      field: 'branch' },
  { header: 'Cust Key',    field: 'custKey' },
  { header: 'Name',        field: 'cusName' },
  { header: 'Address 1',   field: 'addrLn1' },
  { header: 'Address 2',   field: 'addrLn2' },
  { header: 'Region',      field: 'region' },
  { header: 'Sales Group', field: 'salesGroup' },
  { header: 'Customer/Chain/Group', field: 'chainGroup' },
  { header: 'Servicing Branch', field: 'servicingBranch' },
  { header: 'BMS Sales Group (NSLGRP)', field: 'nSlGrp' },
  { header: 'BMS Region (NREGION)', field: 'nRegion' },
  { header: 'Zone',        field: 'cZone' },
  { header: 'Term Days',   field: 'termDays' },
  { header: 'Salesman',    field: 'salesman' },
  { header: 'TIN',         field: 'tin' },
  { header: 'First Order', field: 'firstOrder' },
  { header: 'Inv Blocked', field: 'blockInv', formatter: yesNo },
  { header: 'Inactive',    field: 'inactive', formatter: yesNo },
];

/** Customers — read-only view of the customer master (each branch's BMS CUST4WIN, synced). */
@Component({
  selector: 'app-customers-page',
  standalone: true,
  providers: [MessageService],
  imports: [FormsModule, DatePipe, DecimalPipe, TableModule, TagModule, SelectModule, SelectButtonModule,
    DrawerModule, MessageModule, ToastModule],
  template: `
    <p-toast position="top-right" />

    <div class="filters">
      <p-select [options]="branchOptions()" optionLabel="label" optionValue="value" [ngModel]="branch()"
        (ngModelChange)="branch.set($event); reload()" placeholder="All branches" [showClear]="true" styleClass="branch-sel" />
      <p-selectButton [options]="statusViews" [ngModel]="status()" (ngModelChange)="status.set($event); reload()"
        optionLabel="label" optionValue="value" [allowEmpty]="false" />
      <span class="count">{{ total() | number }} customers</span>
    </div>

    <p-table [value]="rows()" [lazy]="true" (onLazyLoad)="onLazy($event)" [loading]="loading()"
      [paginator]="true" [rows]="pageSize" [first]="first()" [totalRecords]="total()" [rowsPerPageOptions]="[25, 50, 100]"
      sortField="custKey" [sortOrder]="1" dataKey="id" styleClass="p-datatable-sm">
      <ng-template pTemplate="header">
        <tr>
          <th pSortableColumn="branch" style="width:6rem">Branch <p-sortIcon field="branch" /></th>
          <th pSortableColumn="custKey" style="width:7rem">Cust Key <p-sortIcon field="custKey" /></th>
          <th pSortableColumn="cusName">Name <p-sortIcon field="cusName" /></th>
          <th>Address</th>
          <th pSortableColumn="region">Region <p-sortIcon field="region" /></th>
          <th pSortableColumn="salesGroup">Sales Group <p-sortIcon field="salesGroup" /></th>
          <th pSortableColumn="chainGroup">Customer/Chain/Group <p-sortIcon field="chainGroup" /></th>
          <th pSortableColumn="servicingBranch">Servicing Branch <p-sortIcon field="servicingBranch" /></th>
          <th pSortableColumn="cZone">Zone <p-sortIcon field="cZone" /></th>
          <th pSortableColumn="termDays" class="num">Terms <p-sortIcon field="termDays" /></th>
          <th pSortableColumn="firstOrder">First Order <p-sortIcon field="firstOrder" /></th>
          <th>Status</th>
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-c>
        <tr class="clickable" (click)="open(c)">
          <td>{{ c.branch }}</td>
          <td>{{ c.custKey }}</td>
          <td>{{ c.cusName }}</td>
          <td class="addr">{{ c.addrLn1 }}@if (c.addrLn2) { <span class="sub">{{ c.addrLn2 }}</span> }</td>
          <td>{{ c.region || '—' }}</td>
          <td>{{ c.salesGroup || '—' }}</td>
          <td>{{ c.chainGroup || '—' }}</td>
          <td>{{ c.servicingBranch || '—' }}</td>
          <td>{{ c.cZone || '—' }}</td>
          <td class="num">{{ c.term === 0 ? 'Cash' : c.termDays + ' days' }}</td>
          <td>{{ (c.firstOrder | date:'MM/dd/yyyy') || '—' }}</td>
          <td>
            <p-tag [value]="c.inactive ? 'Inactive' : 'Active'" [severity]="c.inactive ? 'danger' : 'success'" />
            @if (c.blockInv) { <p-tag value="Inv Blocked" severity="warn" styleClass="ml-1" /> }
          </td>
        </tr>
      </ng-template>
      <ng-template pTemplate="emptymessage">
        <tr><td colspan="12" class="text-center">No customers match.</td></tr>
      </ng-template>
    </p-table>

    <p-drawer [(visible)]="drawerVisible" position="right" [style]="{ width: '440px' }"
      [header]="current() ? current()!.custKey + ' — ' + current()!.cusName : ''">
      @if (current(); as c) {
        <dl class="details">
          <dt>Status</dt>
          <dd>
            <p-tag [value]="c.inactive ? 'Inactive' : 'Active'" [severity]="c.inactive ? 'danger' : 'success'" />
            @if (c.blockInv) { <p-tag value="Invoice Blocked" severity="warn" styleClass="ml-1" /> }
          </dd>
          <dt>Branch Folder</dt><dd>{{ c.branch }}</dd>
          <dt>Address</dt><dd>{{ c.addrLn1 }}<br />{{ c.addrLn2 }}</dd>
          <dt>Delivery Address</dt><dd>@if (c.delAddrLn1 || c.delAddrLn2) { {{ c.delAddrLn1 }}<br />{{ c.delAddrLn2 }} } @else { Same as address }</dd>
          <dt>Delivery Area</dt><dd>{{ c.delArea || '—' }}</dd>
          <dt>Region</dt><dd>{{ c.region || '—' }}</dd>
          <dt>Sales Group</dt><dd>{{ c.salesGroup || '—' }}</dd>
          <dt>Customer/Chain/Group</dt><dd>{{ c.chainGroup || '—' }}</dd>
          <dt>Servicing Branch</dt><dd>{{ c.servicingBranch || '—' }}</dd>
          <dt>BMS Sales Group (NSLGRP)</dt><dd>{{ c.nSlGrp || '—' }}</dd>
          <dt>BMS Region (NREGION)</dt><dd>{{ c.nRegion || '—' }}</dd>
          <dt>Zone (CZone)</dt><dd>{{ c.cZone || '—' }}</dd>
          <dt>Terms</dt><dd>{{ c.term === 0 ? 'Cash' : c.termDays + ' days' }}</dd>
          <dt>Salesman</dt><dd>{{ c.salesman || '—' }} @if (c.csMan) { <span class="sub">({{ c.csMan }})</span> }</dd>
          <dt>Warehouse</dt><dd>{{ c.whseNo }} · serves {{ c.serveWh || c.whseNo }} · delivers {{ c.delWhse || '—' }}</dd>
          <dt>TIN</dt><dd>{{ c.tin || '—' }}</dd>
          <dt>VAT ID</dt><dd>{{ c.vatId || '—' }}</dd>
          <dt>Subdivision</dt><dd>{{ c.subd || '—' }}</dd>
          <dt>First Order</dt><dd>{{ (c.firstOrder | date:'MMM d, yyyy') || '—' }}</dd>
          <dt>Flags</dt>
          <dd>{{ flags(c) || '—' }}</dd>
          <dt>Last Synced</dt><dd>{{ utc(c.importedAt) | date:'MMM d, yyyy h:mm a' }}</dd>
        </dl>
        <p-message severity="secondary" styleClass="w-full"
          text="Read only — customers are maintained in each branch's BMS and synced into HOMSys." />
      }
    </p-drawer>
  `,
  styles: [`
    :host { display: block; }
    .filters { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 0.6rem; }
    :host ::ng-deep .branch-sel { min-width: 220px; }
    .count { margin-left: auto; font-size: 0.8rem; color: var(--p-text-muted-color); }
    .num { text-align: right; }
    .clickable { cursor: pointer; }
    .addr { max-width: 340px; }
    .sub { display: block; font-size: 0.75rem; color: var(--p-text-muted-color); }
    :host ::ng-deep .ml-1 { margin-left: 0.25rem; }
    .details { display: grid; grid-template-columns: 8.5rem 1fr; gap: 0.35rem 0.75rem; margin: 0 0 1rem; font-size: 0.85rem; }
    .details dt { color: var(--p-text-muted-color); }
    .details dd { margin: 0; }
  `]
})
export class CustomersPageComponent implements OnInit, OnDestroy {
  private api        = inject(CustomerMasterService);
  private toolbar    = inject(GlobalToolbarService);
  private exportSvc  = inject(ExportService);
  private messageSvc = inject(MessageService);

  protected readonly utc = utc;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly statusViews = [
    { label: 'Active', value: 'active' },
    { label: 'Inactive', value: 'inactive' },
    { label: 'All', value: 'all' },
  ];

  protected rows      = signal<CustomerMaster[]>([]);
  protected total     = signal(0);
  protected first     = signal(0);
  protected loading   = signal(false);
  protected exporting = signal(false);
  protected branches  = signal<CustomerBranchCount[]>([]);
  protected branch    = signal<string | null>(null);
  protected status    = signal<StatusView>('active');
  protected current   = signal<CustomerMaster | null>(null);
  protected drawerVisible = false;

  private term = '';
  private searchTimer?: ReturnType<typeof setTimeout>;
  private last: CustomerQuery = { page: 1, pageSize: PAGE_SIZE, sortField: 'custKey', sortDesc: false };

  protected branchOptions = computed(() =>
    this.branches().map(b => ({ label: `${b.branch} (${b.active.toLocaleString()} active)`, value: b.branch })));

  ngOnInit() {
    this.api.getBranches().subscribe(res => this.branches.set(res.data ?? []));
    this.toolbar.set({
      title:   'Customers',
      refresh: { onClick: () => this.fetch(), loading: this.loading },
      search:  term => this.onSearch(term),
      print:   { onClick: () => window.print() },
      export:  { pdf: () => this.export('pdf'), excel: () => this.export('excel'), csv: () => this.export('csv') },
    });
  }

  ngOnDestroy() {
    clearTimeout(this.searchTimer);
    this.toolbar.clear();
  }

  /** PrimeNG lazy table: page / sort changes. */
  onLazy(e: TableLazyLoadEvent) {
    const rows = e.rows ?? PAGE_SIZE;
    this.first.set(e.first ?? 0);
    this.last = {
      ...this.last,
      page: Math.floor((e.first ?? 0) / rows) + 1,
      pageSize: rows,
      sortField: (e.sortField as string) || 'custKey',
      sortDesc: e.sortOrder === -1
    };
    this.fetch();
  }

  private onSearch(term: string) {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => { this.term = term.trim(); this.reload(); }, 400);
  }

  /** Filters changed — back to page 1. */
  reload() {
    this.first.set(0);
    this.last = { ...this.last, page: 1 };
    this.fetch();
  }

  private query(): CustomerQuery {
    return { ...this.last, term: this.term, branch: this.branch(), status: this.status() };
  }

  private fetch() {
    this.loading.set(true);
    this.api.getPage(this.query()).subscribe({
      next: res => {
        this.rows.set(res.data?.rows ?? []);
        this.total.set(res.data?.total ?? 0);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  open(c: CustomerMaster) {
    this.current.set(c);
    this.drawerVisible = true;
  }

  flags(c: CustomerMaster) {
    return [c.tpc && 'TPC', c.offshore && 'Offshore', c.exBranch && 'Ex-branch'].filter(Boolean).join(', ');
  }

  /** Exports every row matching the current filters (up to 20,000), not just the visible page. */
  private export(kind: 'pdf' | 'excel' | 'csv') {
    if (this.total() > EXPORT_MAX) {
      this.messageSvc.add({ severity: 'warn', summary: 'Too many rows',
        detail: `${this.total().toLocaleString()} customers match — narrow it down (branch, status or search) to ${EXPORT_MAX.toLocaleString()} or fewer to export.` });
      return;
    }
    this.exporting.set(true);
    this.api.getPage({ ...this.query(), page: 1, pageSize: EXPORT_MAX }).subscribe({
      next: res => {
        this.exporting.set(false);
        const rows = (res.data?.rows ?? []) as unknown as Record<string, unknown>[];
        if (kind === 'pdf') this.exportSvc.exportPdf('Customers', EXPORT_COLS, rows);
        else if (kind === 'excel') this.exportSvc.exportExcel('Customers', EXPORT_COLS, rows);
        else this.exportSvc.exportCsv('Customers', EXPORT_COLS, rows);
      },
      error: () => this.exporting.set(false)
    });
  }
}
