import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { catchError, map, of, Observable } from 'rxjs';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { MessageModule } from 'primeng/message';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { MessageService, ConfirmationService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { SalesOrderService } from '../../../core/services/sales-order.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { SalesOrderDto, SalesOrderLineDto, ImportedOrderDraft } from '../../../core/models/sales-order.model';
import { ImportDialogComponent, ImportColumn, ImportEnrichColumn } from '../../../shared/import-dialog/import-dialog.component';
import { CustomerMappingDialogComponent } from '../customer-mapping-dialog/customer-mapping-dialog.component';

/** Original vs Allocated vs OOS for one SKU line, computed client-side — never stored. */
interface OosLineRow {
  cProdNo: string;
  prodDesc: string;
  origCs: number;
  origPc: number;
  allocCs: number;
  allocPc: number;
  oosCs: number;
  oosPc: number;
  amt: number | null;
}

const IMPORT_COLS: ImportColumn[] = [
  { header: 'Customer Code',    field: 'custKey',   required: true },
  { header: 'PO Number',        field: 'poNum',     required: true },
  { header: 'PO Date',          field: 'poDate',    required: true, dateField: true },
  { header: 'Cancel Date',      field: 'cancelDate', required: true, dateField: true },
  { header: 'OR Number',        field: 'orNo' },
  { header: 'OR Date',          field: 'chkDate', dateField: true },
  { header: 'OR Amount',        field: 'orAmt' },
  { header: 'Product Code',     field: 'cProdNo',   required: true },
  { header: 'Qty (Case)',       field: 'qtyCs',     required: true },
  { header: 'Remarks',          field: 'remarks' },
];

/** Required column order (A, B, C, ...) for the "Import" template. */
const IMPORT_EXPECTED_HEADERS = [
  'Customer Code', 'PO Number', 'PO Date', 'Cancel Date',
  'OR Number', 'OR Date', 'OR Amount', 'Product Code', 'Qty (Case)', 'Remarks',
];

/** "Import by Customer Name" — same shape as IMPORT_COLS, minus custKey, plus a free-text identifier. */
const PO_BY_NAME_IMPORT_COLS: ImportColumn[] = [
  { header: 'Customer Identifier', field: 'custIdentifier', required: true },
  { header: 'PO Number',        field: 'poNum',     required: true },
  { header: 'PO Date',          field: 'poDate',    required: true, dateField: true },
  { header: 'Cancel Date',      field: 'cancelDate', required: true, dateField: true },
  { header: 'OR Number',        field: 'orNo' },
  { header: 'OR Date',          field: 'chkDate', dateField: true },
  { header: 'OR Amount',        field: 'orAmt' },
  { header: 'Product Code',     field: 'cProdNo',   required: true },
  { header: 'Qty (Case)',       field: 'qtyCs',     required: true },
  { header: 'Remarks',          field: 'remarks' },
];

/** Required column order (A, B, C, ...) for the "Import by Customer Name" template. */
const PO_BY_NAME_EXPECTED_HEADERS = [
  'Customer IDENTIFIER', 'PO Number', 'PO Date', 'Cancel Date',
  'OR Number', 'OR Date', 'OR Amount', 'Product Code', 'Qty (Case)', 'Remarks',
];

@Component({
  selector: 'app-sales-order-list-page',
  standalone: true,
  imports: [TableModule, TagModule, ButtonModule, TooltipModule, MessageModule, ToastModule, ConfirmDialogModule, DialogModule, DatePipe, CurrencyPipe, ImportDialogComponent, CustomerMappingDialogComponent],
  providers: [MessageService, ConfirmationService],
  template: `
    <div class="list-card">
      <p-confirmDialog [style]="{ width: '30rem' }" />
      <p-toast position="top-right" />

      <app-import-dialog
        [(visible)]="importVisible"
        [columns]="importCols"
        [enrichColumns]="importEnrichCols"
        [expectedHeaders]="importExpectedHeaders"
        [validateRows]="checkPoNumsEarly"
        entityName="SO by Custkey"
        (fileMeta)="onImportFileMeta($event)"
        (importRows)="handleImportRows($event)" />

      <app-import-dialog
        [(visible)]="importByNameVisible"
        [columns]="importByNameCols"
        [skipMappedPreview]="true"
        [expectedHeaders]="importByNameExpectedHeaders"
        [validateRows]="checkPoNumsEarly"
        entityName="Sales Orders By Customer Name"
        (fileMeta)="onImportFileMeta($event)"
        (importRows)="handleImportRowsByCustomerName($event)" />

      <app-customer-mapping-dialog
        [(visible)]="mappingVisible"
        [identifiers]="pendingIdentifiers"
        (mapped)="onIdentifiersMapped($event)" />

      @if (apiError()) {
        <p-message severity="error" styleClass="w-full mb-3">
          <span>{{ apiError() }}</span>
        </p-message>
      }

      <div class="table-scroll">
      <p-table [value]="filtered()" [loading]="loading()" [paginator]="true" [rows]="25"
               [rowsPerPageOptions]="[25, 50, 100]" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th pSortableColumn="soId">SOID <p-sortIcon field="soId" /></th>
            <th pSortableColumn="soNo">BMS SO# <p-sortIcon field="soNo" /></th>
            <th pSortableColumn="orderDate">Order Date <p-sortIcon field="orderDate" /></th>
            <th pSortableColumn="custKey">CustKey <p-sortIcon field="custKey" /></th>
            <th pSortableColumn="cusName">Customer <p-sortIcon field="cusName" /></th>
            <th pSortableColumn="poNum">PO No. <p-sortIcon field="poNum" /></th>
            <th pSortableColumn="workflowStatus">Status <p-sortIcon field="workflowStatus" /></th>
            <th style="text-align: center">OOS</th>
            <th pSortableColumn="invNo">Invoice <p-sortIcon field="invNo" /></th>
            <th pSortableColumn="invDate">Invoice Date <p-sortIcon field="invDate" /></th>
            <th pSortableColumn="estAmt">Total Amt <p-sortIcon field="estAmt" /></th>
            <th pSortableColumn="createdBy">Encoded By <p-sortIcon field="createdBy" /></th>
            <th style="width: 90px"></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-o>
          <tr>
            <td>{{ o.soId }}</td>
            <td>
              @if (o.soNo) {
                <a href="javascript:void(0)" class="so-link" (click)="view(o)" pTooltip="View details">
                  <p-tag severity="success" [value]="o.soNo" />
                </a>
                @if (o.resyncFailed) {
                  <p-tag severity="danger" value="resync failed" pTooltip="BMS could not find this order to apply the edit — investigate the live record." />
                } @else if (o.needsResync) {
                  <p-tag severity="info" value="syncing to BMS…" pTooltip="Edited after deallocation — waiting for BMS to pick up the change." />
                }
              } @else {
                <p-tag severity="warn" value="not pushed" />
              }
            </td>
            <td>{{ o.orderDate | date: 'MM/dd/yyyy' }}</td>
            <td>{{ o.custKey }}</td>
            <td>{{ o.cusName }}</td>
            <td>{{ o.poNum }}</td>
            <td><p-tag [severity]="statusSeverity(o.workflowStatus)" [value]="o.workflowStatus || 'Entered'" /></td>
            <td style="text-align: center">
              @if (oosCasesTotal(o) > 0) {
                <a href="javascript:void(0)" class="so-link" (click)="viewOosDetails(o)" pTooltip="View OOS details">{{ oosCasesTotal(o) }}</a>
              } @else {
                0
              }
            </td>
            <td>{{ o.invNo ?? '—' }}</td>
            <td>{{ o.invDate ? (o.invDate | date: 'MM/dd/yyyy') : '—' }}</td>
            <td [pTooltip]="o.invAmt == null ? 'Estimate from current price quotes — not yet invoiced' : undefined">
              {{ (o.invAmt ?? o.estAmt) | currency: 'PHP' }}{{ o.invAmt == null ? ' *' : '' }}
            </td>
            <td>{{ o.createdBy }}</td>
            <td>
              <p-button icon="pi pi-eye" [text]="true" (onClick)="view(o)" pTooltip="View" />
              <p-button icon="pi pi-pencil" [text]="true" [disabled]="!!o.invNo || !!o.isLocked" (onClick)="edit(o)"
                        [class.edit-disabled]="!!o.invNo || !!o.isLocked"
                        [pTooltip]="o.invNo ? 'Already invoiced — cannot edit' : (o.isLocked ? (o.needsResync ? 'Locked — syncing edit to BMS' : 'Locked — pushed to BMS') : 'Edit')" />
            </td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage">
          <tr><td colspan="13">No sales orders encoded yet.</td></tr>
        </ng-template>
      </p-table>

      <p-dialog [(visible)]="oosDetailsVisible" [modal]="true" [style]="{ width: '55rem' }"
                [header]="'OOS Details — SO# ' + (selectedOosOrder()?.soNo ?? selectedOosOrder()?.soId)">
        @if (selectedOosOrder(); as o) {
          <table class="detail-table">
            <thead>
              <tr>
                <th>Prodno</th>
                <th>Description</th>
                <th>Original (Cs/Pc)</th>
                <th>Allocated (Cs/Pc)</th>
                <th>OOS (Cs/Pc)</th>
                <th>Amt</th>
              </tr>
            </thead>
            <tbody>
              @for (l of oosLines(o); track l.cProdNo) {
                <tr>
                  <td>{{ l.cProdNo }}</td>
                  <td>{{ l.prodDesc }}</td>
                  <td>{{ l.origCs }} / {{ l.origPc }}</td>
                  <td>{{ l.allocCs }} / {{ l.allocPc }}</td>
                  <td>{{ l.oosCs }} / {{ l.oosPc }}</td>
                  <td>{{ l.amt == null ? '—' : (l.amt | currency: 'PHP') }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </p-dialog>
      </div>
    </div>
  `,
  styles: [`
    .table-scroll { overflow-x: auto; }
    .table-scroll ::ng-deep .p-datatable-table { min-width: 900px; }
    .edit-disabled ::ng-deep .p-button-icon { color: #9e9e9e; }
    .so-link { cursor: pointer; }
    .detail-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    .detail-table th, .detail-table td { padding: 0.4rem 0.6rem; border-bottom: 1px solid #eee; text-align: left; }
    .detail-table thead th { background: #fafafa; font-weight: 600; }
  `]
})
export class SalesOrderListPageComponent implements OnInit, OnDestroy {
  private api = inject(SalesOrderService);
  private router = inject(Router);
  private toolbar = inject(GlobalToolbarService);
  private messageSvc = inject(MessageService);
  private tabBar = inject(TabBarService);
  private confirmSvc = inject(ConfirmationService);

  orders = signal<SalesOrderDto[]>([]);
  loading = signal(false);
  apiError = signal<string | null>(null);
  searchTerm = signal('');
  importVisible = false;
  importByNameVisible = false;
  mappingVisible = false;
  oosDetailsVisible = false;
  selectedOosOrder = signal<SalesOrderDto | null>(null);

  /** cProdNo|custKey -> price per case, ex-VAT. Fetched lazily when OOS details are opened. */
  private priceCache = new Map<string, number | null>();
  private priceVersion = signal(0);
  protected readonly importCols = IMPORT_COLS;
  protected readonly importByNameCols = PO_BY_NAME_IMPORT_COLS;
  protected readonly importExpectedHeaders = IMPORT_EXPECTED_HEADERS;
  protected readonly importByNameExpectedHeaders = PO_BY_NAME_EXPECTED_HEADERS;
  protected pendingIdentifiers: { identifier: string; poNums: string[] }[] = [];
  private pendingRows: Record<string, string>[] = [];
  private pendingFileHash = '';
  private pendingFileName = '';
  protected readonly importEnrichCols: ImportEnrichColumn[] = [
    {
      header: 'Customer Name',
      after: 'custKey',
      resolve: row => this.api.lookupCustomer(row['custKey']).pipe(
        map(res => res.data?.cusName ?? null),
        catchError(() => of(null)))
    },
    {
      header: 'Product Description',
      after: 'cProdNo',
      resolve: row => this.api.lookupProduct(row['cProdNo']).pipe(
        map(res => {
          const p = res.data;
          return p ? `${p.prodDesc} ${p.pieces} x ${p.packSize}` : null;
        }),
        catchError(() => of(null)))
    }
  ];

  filtered = computed(() => {
    const words = this.searchTerm().toLowerCase().trim().split(/\s+/).filter(Boolean);
    if (!words.length) return this.orders();
    return this.orders().filter(o => {
      const haystack = [o.custKey, o.cusName, o.poNum, String(o.soId), String(o.soNo ?? '')]
        .join(' ').toLowerCase();
      return words.every(w => haystack.includes(w));
    });
  });

  ngOnInit(): void {
    this.toolbar.set({
      title: 'Sales Orders',
      add: { onClick: () => this.router.navigate(['/sales-orders']) },
      refresh: { onClick: () => this.load() },
      search: (term: string) => this.searchTerm.set(term),
      import: { onClick: () => this.importVisible = true },
      importByName: { onClick: () => this.importByNameVisible = true }
    });
    this.load();
  }

  ngOnDestroy(): void {
    this.toolbar.clear();
  }

  /** Captures the just-selected file's identity, emitted synchronously before importRows. */
  onImportFileMeta(meta: { fileHash: string; fileName: string }): void {
    this.pendingFileHash = meta.fileHash;
    this.pendingFileName = meta.fileName;
  }

  /**
   * Import wizard's early hard-block gate (bound as [validateRows]), run
   * right after Next — before column mapping or customer resolution.
   * Matches purely on PO Number so it also covers the "Import by Customer
   * Name" flow, where CustKey isn't resolved yet at this point. This is
   * what catches a re-upload that dodges the file-hash check by having
   * different bytes (e.g. a renamed worksheet tab) but the same PO data.
   */
  protected checkPoNumsEarly = (rows: Record<string, string>[]): Observable<{ blocked: boolean; message?: string }> => {
    const poNums = Array.from(new Set(rows.map(r => (r['poNum'] || '').trim()).filter(Boolean)));
    if (poNums.length === 0) return of({ blocked: false });

    return this.api.checkImportPoNumbers(poNums).pipe(
      map(res => {
        const matches = res.data?.matches ?? [];
        if (matches.length === 0) return { blocked: false };
        const lines = matches.slice(0, 8).map(m =>
          `PO ${m.poNum} — ${m.cusName || m.custKey} on ${new Date(m.orderDate).toLocaleDateString()}${m.encodedBy ? ' by ' + m.encodedBy : ''}`);
        if (matches.length > 8) lines.push(`+${matches.length - 8} more`);
        this.messageSvc.add({
          severity: 'error',
          summary: 'Cannot Import',
          detail: `Already exists as Sales Order(s):\n${lines.join('\n')}`,
          sticky: true,
          closable: true
        });
        return { blocked: true };
      }),
      catchError(() => of({ blocked: false }))
    );
  };

  handleImportRows(rows: Record<string, string>[]): void {
    this.gateOnFileImported(() => this.buildAndOpenDrafts(rows, this.pendingFileHash, this.pendingFileName));
  }

  /** Extracts unique Customer Identifiers, then asks the encoder to resolve each to a CustKey. */
  handleImportRowsByCustomerName(rows: Record<string, string>[]): void {
    this.gateOnFileImported(() => {
      const byIdentifier = new Map<string, Set<string>>();
      rows.forEach(r => {
        const id = (r['custIdentifier'] || '').trim();
        if (!id) return;
        const poNums = byIdentifier.get(id) ?? new Set<string>();
        if (r['poNum']) poNums.add(r['poNum']);
        byIdentifier.set(id, poNums);
      });

      if (byIdentifier.size === 0) {
        this.messageSvc.add({ severity: 'error', summary: 'Invalid Excel', detail: 'No Customer Identifier found in the file.' });
        return;
      }

      this.pendingRows = rows;
      this.pendingIdentifiers = Array.from(byIdentifier.entries())
        .map(([identifier, poNums]) => ({ identifier, poNums: Array.from(poNums) }));
      this.mappingVisible = true;
    });
  }

  /** Substitutes each row's Customer Identifier with its resolved CustKey, then feeds the shared import logic. */
  onIdentifiersMapped(map: Map<string, string>): void {
    const rows = this.pendingRows.map(r => ({
      ...r,
      custKey: map.get((r['custIdentifier'] || '').trim()) ?? ''
    }));
    this.pendingRows = [];
    this.pendingIdentifiers = [];
    this.buildAndOpenDrafts(rows, this.pendingFileHash, this.pendingFileName);
  }

  /** Hard-blocks when this exact file already produced a real, saved Sales Order. */
  private gateOnFileImported(proceed: () => void): void {
    const fileHash = this.pendingFileHash;
    if (!fileHash) { proceed(); return; }

    this.api.checkImportFile(fileHash).subscribe({
      next: res => {
        const check = res.data;
        if (check?.alreadyProcessed) {
          const when = check.firstProcessedAt ? new Date(check.firstProcessedAt).toLocaleString() : 'an earlier date';
          this.confirmSvc.confirm({
            header: 'File Already Imported',
            message: `This exact file (${this.pendingFileName || 'the selected file'}) was already imported`
              + `${check.firstProcessedBy ? ' by ' + check.firstProcessedBy : ''} on ${when}. Re-importing it is not allowed.`,
            icon: 'pi pi-exclamation-triangle',
            acceptLabel: 'OK',
            rejectVisible: false,
            accept: () => {}
          });
          return;
        }
        proceed();
      },
      error: () => proceed()
    });
  }

  private buildAndOpenDrafts(rows: Record<string, string>[], fileHash: string, fileName: string): void {
    const REQUIRED_FIELDS: { field: string; label: string }[] = [
      { field: 'custKey',   label: 'Customer Code' },
      { field: 'poNum',     label: 'PO Number' },
      { field: 'poDate',    label: 'PO Date' },
      { field: 'cancelDate', label: 'Cancel Date' },
      { field: 'cProdNo',   label: 'Product Code' },
      { field: 'qtyCs',     label: 'Qty (Case)' },
    ];

    const issuesByRow = new Map<number, string[]>();
    const addIssue = (row: number, message: string) => {
      const list = issuesByRow.get(row) ?? [];
      list.push(message);
      issuesByRow.set(row, list);
    };

    rows.forEach((r, i) => {
      const missing = REQUIRED_FIELDS.filter(f => !r[f.field]).map(f => f.label);
      if (missing.length) addIssue(i + 1, `missing ${missing.join(', ')}`);
    });

    // Header-level fields must be identical across every line of the same PO —
    // e.g. OR Number filled on one line and blank on another line of the same PO.
    const CONSISTENCY_FIELDS: { field: string; label: string }[] = [
      { field: 'custKey',    label: 'Customer Code' },
      { field: 'poDate',     label: 'PO Date' },
      { field: 'cancelDate', label: 'Cancel Date' },
      { field: 'orNo',       label: 'OR Number' },
      { field: 'chkDate',    label: 'OR Date' },
      { field: 'orAmt',      label: 'OR Amount' },
      { field: 'remarks',    label: 'Remarks' },
    ];

    const byPo = new Map<string, { row: number; data: Record<string, string> }[]>();
    rows.forEach((r, i) => {
      if (!r['poNum']) return;
      const group = byPo.get(r['poNum']) ?? [];
      group.push({ row: i + 1, data: r });
      byPo.set(r['poNum'], group);
    });

    for (const [poNum, groupRows] of byPo) {
      if (groupRows.length < 2) continue;
      const inconsistentFields = CONSISTENCY_FIELDS.filter(f =>
        new Set(groupRows.map(g => (g.data[f.field] || '').trim())).size > 1
      );
      if (inconsistentFields.length) {
        const labels = inconsistentFields.map(f => f.label).join(', ');
        for (const g of groupRows) {
          addIssue(g.row, `${labels} must be the same for every line of PO ${poNum}`);
        }
      }
    }

    if (issuesByRow.size > 0) {
      const sortedRows = Array.from(issuesByRow.keys()).sort((a, b) => a - b);
      const lines = sortedRows.slice(0, 8)
        .map(row => `Row ${row}: ${issuesByRow.get(row)!.join('; ')}`);
      if (sortedRows.length > 8) lines.push(`+${sortedRows.length - 8} more row(s)`);
      this.messageSvc.add({
        severity: 'error',
        summary: 'Invalid Excel',
        detail: lines.join('\n'),
        life: 15000
      });
      return;
    }

    const toIntOrNull = (v: string) => v && !isNaN(+v) ? +v : null;
    const toFloatOrNull = (v: string) => v && !isNaN(+v) ? +v : null;

    const drafts: ImportedOrderDraft[] = Array.from(byPo.entries()).map(([poNum, groupRows]) => {
      const h = groupRows[0].data;
      return {
        custKey: h['custKey'].trim(),
        poNum,
        poDate: h['poDate'] || null,
        cancelDate: h['cancelDate'] || null,
        remarks: h['remarks'] || '',
        orNo: toIntOrNull(h['orNo']),
        chkDate: h['chkDate'] || null,
        orAmt: toFloatOrNull(h['orAmt']),
        sourceFileHash: fileHash || null,
        sourceFileName: fileName || null,
        lines: groupRows.map(l => ({
          cProdNo: l.data['cProdNo'].trim(),
          qtyCs: toIntOrNull(l.data['qtyCs']) ?? 0
        }))
      };
    });

    const checkRows = drafts.map(d => ({ custKey: d.custKey, poNum: d.poNum }));
    this.api.checkImportDuplicates(checkRows).subscribe({
      next: res => {
        const dupPos = (res.data?.duplicateRows ?? []).map(d => d.poNum);
        if (dupPos.length) {
          this.messageSvc.add({
            severity: 'warn',
            summary: 'Possible Duplicate',
            detail: `PO(s) already exist as Sales Orders: ${dupPos.join(', ')}`,
            life: 10000
          });
        }
        this.openDraftTabs(drafts);
      },
      error: () => this.openDraftTabs(drafts)
    });
  }

  private openDraftTabs(drafts: ImportedOrderDraft[]): void {
    const stamp = Date.now();
    let firstState: { draftKey: string; draftOrder: ImportedOrderDraft } | null = null;

    drafts.forEach((draftOrder, i) => {
      const draftKey = `draft-${stamp}-${i}`;
      const state = { draftKey, draftOrder };
      if (i === 0) firstState = state;
      this.tabBar.openTab({
        key: `/sales-orders#${draftKey}`,
        label: `New — PO ${draftOrder.poNum}`,
        route: '/sales-orders',
        icon: 'pi-file-edit',
        state
      });
    });

    this.messageSvc.add({
      severity: 'success',
      summary: 'Import Complete',
      detail: `${drafts.length} order(s) staged for review — open each tab and Save to create.`
    });

    if (firstState) this.router.navigate(['/sales-orders'], { state: firstState });
  }

  view(o: SalesOrderDto): void {
    this.router.navigate(['/sales-orders'], { state: { soId: o.soId, mode: 'view' } });
  }

  edit(o: SalesOrderDto): void {
    this.router.navigate(['/sales-orders'], { state: { soId: o.soId, mode: 'edit' } });
  }

  /** Number of cases out-of-stock across every line, summed. */
  oosCasesTotal(o: SalesOrderDto): number {
    return o.lines.reduce((sum, l) => sum + this.lineOosCs(l), 0);
  }

  oosLines(o: SalesOrderDto): OosLineRow[] {
    this.priceVersion();
    return o.lines
      .filter(l => this.lineOosTotal(l) > 0)
      .map(l => this.toOosRow(o, l));
  }

  viewOosDetails(o: SalesOrderDto): void {
    this.selectedOosOrder.set(o);
    this.oosDetailsVisible = true;
    for (const l of o.lines) {
      if (this.lineOosTotal(l) <= 0) continue;
      const key = `${l.cProdNo}|${o.custKey}`;
      if (this.priceCache.has(key)) continue;
      this.priceCache.set(key, null);
      this.api.getQuote(l.cProdNo, o.custKey).subscribe({
        next: res => {
          this.priceCache.set(key, res.data?.hasPrice ? (res.data.pricePerCase ?? null) : null);
          this.priceVersion.update(v => v + 1);
        },
        error: () => this.priceVersion.update(v => v + 1)
      });
    }
  }

  private lineOosTotal(l: SalesOrderLineDto): number {
    if (l.allocatedQtyCs == null || l.allocatedQtyPc == null) return 0;
    const pieces = l.pieces || 0;
    const orig = l.qtyCs * pieces + l.qtyPc;
    const alloc = l.allocatedQtyCs * pieces + l.allocatedQtyPc;
    return orig - alloc;
  }

  private lineOosCs(l: SalesOrderLineDto): number {
    const pieces = l.pieces || 0;
    const oosTotal = this.lineOosTotal(l);
    return pieces > 0 ? Math.floor(oosTotal / pieces) : oosTotal;
  }

  private toOosRow(o: SalesOrderDto, l: SalesOrderLineDto): OosLineRow {
    const pieces = l.pieces || 0;
    const oosTotal = this.lineOosTotal(l);
    const oosCs = pieces > 0 ? Math.floor(oosTotal / pieces) : 0;
    const oosPc = pieces > 0 ? oosTotal % pieces : oosTotal;

    const pricePerCase = this.priceCache.get(`${l.cProdNo}|${o.custKey}`) ?? null;
    const amt = pricePerCase != null && pieces > 0 ? (oosTotal / pieces) * pricePerCase : null;

    return {
      cProdNo: l.cProdNo,
      prodDesc: l.prodDesc,
      origCs: l.qtyCs,
      origPc: l.qtyPc,
      allocCs: l.allocatedQtyCs ?? 0,
      allocPc: l.allocatedQtyPc ?? 0,
      oosCs,
      oosPc,
      amt
    };
  }

  statusSeverity(status?: string): 'secondary' | 'info' | 'success' | 'warn' {
    switch (status) {
      case 'Downloaded': return 'info';
      case 'Processed': return 'success';
      case 'Deallocated': return 'warn';
      case 'Invoiced': return 'success';
      default: return 'secondary';
    }
  }

  private load(): void {
    this.loading.set(true);
    this.api.getAll().subscribe({
      next: res => {
        this.orders.set(res.data ?? []);
        this.loading.set(false);
      },
      error: err => {
        this.apiError.set(err?.error?.message ?? 'Unable to load sales orders.');
        this.loading.set(false);
      }
    });
  }
}
