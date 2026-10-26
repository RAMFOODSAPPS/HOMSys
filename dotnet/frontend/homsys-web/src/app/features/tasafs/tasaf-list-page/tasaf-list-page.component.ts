import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { ExportColumn, ExportService } from '../../../core/services/export.service';
import { TasafService } from '../../../core/services/tasaf.service';
import { AuthService } from '../../../core/services/auth.service';
import { FormsModule } from '@angular/forms';
import { SelectButtonModule } from 'primeng/selectbutton';
import { formatPeriod, TASAF_STATUS_SEVERITY, TasafListItem } from '../../../core/models/tasaf.model';
import { utc } from '../../../core/utils/utc-date';

type View = 'action' | 'mine' | 'all';
const VIEW_KEY = 'homsys_tasaf_list_view';

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Document No',  field: 'refNo' },
  { header: 'Status',       field: 'status' },
  { header: 'Title',        field: 'title' },
  { header: 'Sales Group',  field: 'salesGroupName' },
  { header: 'Chain',        field: 'chainName' },
  { header: 'Customer',     field: 'customerName' },
  { header: 'Program Type', field: 'programTypeName' },
  { header: 'Duration',     field: 'duration' },
  { header: 'Total Cost',   field: 'totalCost' },
  { header: 'Proponent',    field: 'proponentName' },
  { header: 'Waiting On',   field: 'currentApproverName' },
  { header: 'Role',         field: 'currentRoleCode' },
  { header: 'Submitted',    field: 'submittedText' },
  { header: 'Created',      field: 'createdText' },
];

@Component({
  selector: 'app-tasaf-list-page',
  standalone: true,
  providers: [ConfirmationService, MessageService],
  imports: [TableModule, TagModule, ToastModule, ConfirmDialogModule, DatePipe, DecimalPipe, FormsModule, SelectButtonModule],
  template: `
    <p-toast position="top-right" />
    <p-confirmDialog />

    <div class="views">
      <p-selectButton [options]="views()" [ngModel]="view()" (ngModelChange)="setView($event)"
        optionLabel="label" optionValue="value" [allowEmpty]="false" />
    </div>

    <p-table [value]="filtered()" [loading]="loading()" [paginator]="true" [rows]="15"
      [rowsPerPageOptions]="[15, 30, 50]" [(selection)]="selected" selectionMode="single" dataKey="id"
      (onRowSelect)="noSelection.set(false)" (onRowUnselect)="noSelection.set(true)" styleClass="p-datatable-sm">
      <ng-template pTemplate="header">
        <tr>
          <th pSortableColumn="refNo">Document No <p-sortIcon field="refNo" /></th>
          <th pSortableColumn="status">Status <p-sortIcon field="status" /></th>
          <th pSortableColumn="title">Activity Title <p-sortIcon field="title" /></th>
          <th pSortableColumn="salesGroupName">Sales Group <p-sortIcon field="salesGroupName" /></th>
          <th pSortableColumn="chainName">Customer <p-sortIcon field="chainName" /></th>
          <th pSortableColumn="programTypeName">Program Type <p-sortIcon field="programTypeName" /></th>
          <th>Duration</th>
          <th pSortableColumn="totalCost" class="num">Total Cost <p-sortIcon field="totalCost" /></th>
          <th pSortableColumn="proponentName">Proponent <p-sortIcon field="proponentName" /></th>
          <th>With</th>
          <th pSortableColumn="submittedAt">Submitted <p-sortIcon field="submittedAt" /></th>
          <th pSortableColumn="createdAt">Created <p-sortIcon field="createdAt" /></th>
        </tr>
      </ng-template>
      <ng-template pTemplate="body" let-t>
        <tr [pSelectableRow]="t" (dblclick)="open(t)" [class.awaiting]="t.awaitingMe">
          <td>{{ t.refNo || '—' }}</td>
          <td>
            <p-tag [value]="t.status" [severity]="severity(t.status)" />
            @if (t.awaitingMe) { <div class="sub mine">Your action</div> }
          </td>
          <td>{{ t.title }}</td>
          <td>{{ t.salesGroupName || '—' }}</td>
          <td>{{ t.chainName || '—' }}@if (t.customerName) { <div class="sub">{{ t.customerName }}</div> }</td>
          <td>{{ t.programTypeName || '—' }}</td>
          <td>{{ duration(t) || '—' }}</td>
          <td class="num">{{ t.totalCost | number:'1.2-2' }}</td>
          <td>{{ t.proponentName }}</td>
          <td>{{ withWhom(t) }}</td>
          <td class="dt">{{ (utc(t.submittedAt) | date:'MM/dd/yyyy h:mm a') ?? '—' }}</td>
          <td class="dt">{{ utc(t.createdAt) | date:'MM/dd/yyyy h:mm a' }}</td>
        </tr>
      </ng-template>
      <ng-template pTemplate="emptymessage">
        <tr><td colspan="12" class="text-center">{{ view() === 'action' ? 'Nothing is waiting for your action.' : 'No TASAFs yet.' }}</td></tr>
      </ng-template>
    </p-table>
  `,
  styles: [`
    :host { display: block; }
    .num { text-align: right; }
    .dt { white-space: nowrap; }
    .sub { font-size: 0.75rem; color: var(--p-text-muted-color); }
    .sub.mine { color: #800000; font-weight: 600; }
    .views { margin-bottom: 0.6rem; }
    :host ::ng-deep tr.awaiting > td { background: rgba(128, 0, 0, 0.04); }
  `]
})
export class TasafListPageComponent implements OnInit, OnDestroy {
  private api        = inject(TasafService);
  private router     = inject(Router);
  private toolbar    = inject(GlobalToolbarService);
  private exportSvc  = inject(ExportService);
  private datePipe   = new DatePipe('en-US');
  protected readonly utc = utc;
  private confirmSvc = inject(ConfirmationService);
  private messageSvc = inject(MessageService);
  private auth       = inject(AuthService);

  private initialSearch = (history.state as Record<string, unknown>)?.['searchTerm'] as string ?? '';

  protected all         = signal<TasafListItem[]>([]);
  protected searchTerm  = signal(this.initialSearch);
  protected loading     = signal(true);
  protected deleting    = signal(false);
  protected noSelection = signal(true);
  protected selected: TasafListItem | null = null;

  protected view = signal<View>((localStorage.getItem(VIEW_KEY) as View) || 'all');

  protected views = computed(() => {
    const n = this.all().filter(t => t.awaitingMe).length;
    return [
      { label: `Awaiting my action${n ? ` (${n})` : ''}`, value: 'action' },
      { label: 'My TASAFs', value: 'mine' },
      { label: 'All', value: 'all' },
    ];
  });

  protected filtered = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const v = this.view();
    const rows = this.all().filter(t => v === 'all' || (v === 'mine' ? t.isMine : t.awaitingMe));
    if (!term) return rows;
    return rows.filter(t =>
      [t.refNo, t.status, t.title, t.salesGroupName, t.chainName, t.customerName, t.programTypeName, t.proponentName]
        .some(v => (v ?? '').toLowerCase().includes(term)));
  });

  ngOnInit() {
    this.load();
    this.toolbar.set({
      title:         'TASAF — List',
      add:           { onClick: () => this.router.navigate(['/tasafs']) },
      edit:          { onClick: () => this.selected && this.open(this.selected), disabled: this.noSelection },
      // Delete is Admin-only (also enforced by the API).
      ...(this.auth.isAdmin() ? { delete: { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting } } : {}),
      refresh:       { onClick: () => this.load(), loading: this.loading },
      search:        term => this.searchTerm.set(term),
      initialSearch: this.initialSearch,
      print:         { onClick: () => window.print() },
      export:        { pdf: () => this.export('pdf'), excel: () => this.export('excel'), csv: () => this.export('csv') },
    });
  }

  ngOnDestroy() {
    this.toolbar.clear();
  }

  private load() {
    this.loading.set(true);
    this.api.getAll().subscribe({
      next: res => {
        this.all.set(res.data ?? []);
        this.loading.set(false);
        // First visit: open on "Awaiting my action" if anything is waiting.
        if (!localStorage.getItem(VIEW_KEY) && this.all().some(t => t.awaitingMe)) this.view.set('action');
      },
      error: () => this.loading.set(false)
    });
  }

  severity(status: string) { return TASAF_STATUS_SEVERITY[status] ?? 'info'; }

  setView(v: View) {
    this.view.set(v);
    localStorage.setItem(VIEW_KEY, v);
  }

  /** Who the TASAF is currently waiting on. */
  withWhom(t: TasafListItem) {
    switch (t.status) {
      case 'For Approval': return `${t.currentApproverName} (${t.currentRoleCode}) · ${t.stepsDone + 1} of ${t.stepCount}`;
      case 'Draft':
      case 'Returned':     return t.proponentName;
      default:             return '—';
    }
  }

  duration(t: TasafListItem) {
    return formatPeriod(t.startDate, t.endDate);
  }

  open(t: TasafListItem) {
    this.router.navigate(['/tasafs'], { state: { tasafId: t.id } });
  }

  private confirmDelete() {
    const t = this.selected;
    if (!t) return;
    this.confirmSvc.confirm({
      message: `Delete TASAF <strong>${t.title}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.api.delete(t.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: `${t.title} deleted.` });
            this.selected = null;
            this.noSelection.set(true);
            this.load();
          },
          error: err => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Cannot delete', detail: err?.error?.message ?? 'Failed to delete.' });
          }
        });
      }
    });
  }

  private export(kind: 'pdf' | 'excel' | 'csv') {
    const dt = (s: string | null | undefined) => this.datePipe.transform(utc(s), 'MM/dd/yyyy h:mm a') ?? '';
    const rows = this.filtered().map(t => ({ ...t, duration: this.duration(t), submittedText: dt(t.submittedAt), createdText: dt(t.createdAt) })) as unknown as Record<string, unknown>[];
    if (kind === 'pdf') this.exportSvc.exportPdf('TASAF List', EXPORT_COLS, rows);
    else if (kind === 'excel') this.exportSvc.exportExcel('TASAF List', EXPORT_COLS, rows);
    else this.exportSvc.exportCsv('TASAF List', EXPORT_COLS, rows);
  }
}
