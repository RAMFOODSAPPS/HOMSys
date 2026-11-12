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
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DatePickerModule } from 'primeng/datepicker';
import { FindPanelComponent } from '../../../shared/find-panel/find-panel.component';
import { formatPeriod, TASAF_STATUS_SEVERITY, TasafListItem } from '../../../core/models/tasaf.model';
import { utc } from '../../../core/utils/utc-date';

type View = 'action' | 'mine' | 'all';
const VIEW_KEY = 'homsys_tasaf_list_view';
/** Local date → 'yyyy-MM-dd' (null stays null). */
const ymd = (d: Date | null) => d
  ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` : null;

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Document No',  field: 'refNo' },
  { header: 'TPAR No.',     field: 'tparNo' },
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
  imports: [TableModule, TagModule, ToastModule, ConfirmDialogModule, DatePipe, DecimalPipe, FormsModule, SelectButtonModule,
    SelectModule, MultiSelectModule, InputTextModule, InputNumberModule, DatePickerModule, FindPanelComponent],
  template: `
    <p-toast position="top-right" />
    <p-confirmDialog />

    <app-find-panel [(visible)]="findVisible" [active]="activeFilters()" (clear)="clearFilters()">
      <div class="ff">
        <label>Search <span class="hint">(document no., TPAR, title, customer…)</span></label>
        <input pInputText [ngModel]="searchTerm()" (ngModelChange)="searchTerm.set($event)" />
      </div>
      <div class="ff">
        <label>Show</label>
        <p-select [options]="views()" optionLabel="label" optionValue="value" [ngModel]="view()"
          (ngModelChange)="setView($event)" appendTo="body" />
      </div>
      <div class="ff">
        <label>Status</label>
        <p-multiselect [options]="optsOf('status')" [ngModel]="fStatus()" (ngModelChange)="fStatus.set($event)"
          placeholder="All" [showClear]="true" display="chip" appendTo="body" />
      </div>
      <div class="ff">
        <label>Sales Group</label>
        <p-multiselect [options]="optsOf('salesGroupName')" [ngModel]="fGroups()" (ngModelChange)="fGroups.set($event)"
          placeholder="All" [showClear]="true" [filter]="true" display="chip" appendTo="body" />
      </div>
      <div class="ff">
        <label>Program Type</label>
        <p-multiselect [options]="optsOf('programTypeName')" [ngModel]="fTypes()" (ngModelChange)="fTypes.set($event)"
          placeholder="All" [showClear]="true" [filter]="true" display="chip" appendTo="body" />
      </div>
      <div class="ff">
        <label>Customer / Chain <span class="hint">(contains)</span></label>
        <input pInputText [ngModel]="fCustomer()" (ngModelChange)="fCustomer.set($event)" />
      </div>
      <div class="ff">
        <label>Proponent</label>
        <p-multiselect [options]="optsOf('proponentName')" [ngModel]="fProponents()" (ngModelChange)="fProponents.set($event)"
          placeholder="All" [showClear]="true" [filter]="true" display="chip" appendTo="body" />
      </div>
      <div class="ff">
        <label>Waiting On</label>
        <p-multiselect [options]="optsOf('currentApproverName')" [ngModel]="fApprovers()" (ngModelChange)="fApprovers.set($event)"
          placeholder="Anyone" [showClear]="true" [filter]="true" display="chip" appendTo="body" />
      </div>
      <div class="ff">
        <label>Total Cost</label>
        <div class="pair">
          <p-inputnumber [ngModel]="costMin()" (ngModelChange)="costMin.set($event)" mode="decimal" [maxFractionDigits]="2" placeholder="Min" />
          <p-inputnumber [ngModel]="costMax()" (ngModelChange)="costMax.set($event)" mode="decimal" [maxFractionDigits]="2" placeholder="Max" />
        </div>
      </div>
      <div class="ff">
        <label>Program Duration <span class="hint">(runs within / overlaps)</span></label>
        <div class="pair">
          <p-datepicker [ngModel]="progFrom()" (ngModelChange)="progFrom.set($event)" dateFormat="mm/dd/yy" [showButtonBar]="true" appendTo="body" placeholder="From" />
          <p-datepicker [ngModel]="progTo()" (ngModelChange)="progTo.set($event)" dateFormat="mm/dd/yy" [showButtonBar]="true" appendTo="body" placeholder="To" />
        </div>
      </div>
      <div class="ff">
        <label>Created</label>
        <div class="pair">
          <p-datepicker [ngModel]="createdFrom()" (ngModelChange)="createdFrom.set($event)" dateFormat="mm/dd/yy" [showButtonBar]="true" appendTo="body" placeholder="From" />
          <p-datepicker [ngModel]="createdTo()" (ngModelChange)="createdTo.set($event)" dateFormat="mm/dd/yy" [showButtonBar]="true" appendTo="body" placeholder="To" />
        </div>
      </div>
    </app-find-panel>

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
          <th pSortableColumn="tparNo">TPAR No. <p-sortIcon field="tparNo" /></th>
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
          <td>{{ t.tparNo || '—' }}</td>
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
        <tr><td colspan="13" class="text-center">{{ view() === 'action' ? 'Nothing is waiting for your action.' : all().length ? 'No TASAFs match.' : 'No TASAFs yet.' }}</td></tr>
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

  // Find panel filters (blank / empty = any).
  protected findVisible = signal(false);
  protected fStatus     = signal<string[]>([]);
  protected fGroups     = signal<string[]>([]);
  protected fTypes      = signal<string[]>([]);
  protected fCustomer   = signal('');
  protected fProponents = signal<string[]>([]);
  protected fApprovers  = signal<string[]>([]);
  protected costMin     = signal<number | null>(null);
  protected costMax     = signal<number | null>(null);
  protected progFrom    = signal<Date | null>(null);
  protected progTo      = signal<Date | null>(null);
  protected createdFrom = signal<Date | null>(null);
  protected createdTo   = signal<Date | null>(null);

  protected activeFilters = computed(() =>
    [this.searchTerm().trim(), this.view() !== 'all', this.fStatus().length, this.fGroups().length, this.fTypes().length,
     this.fCustomer().trim(), this.fProponents().length, this.fApprovers().length, this.costMin() != null,
     this.costMax() != null, this.progFrom(), this.progTo(), this.createdFrom(), this.createdTo()].filter(Boolean).length);

  private optCache = computed(() => {
    const keys = ['status', 'salesGroupName', 'programTypeName', 'proponentName', 'currentApproverName'] as const;
    return Object.fromEntries(keys.map(k => [k,
      [...new Set(this.all().map(t => (t[k] ?? '').trim()).filter(v => v))].sort()])) as Record<string, string[]>;
  });
  optsOf(key: 'status' | 'salesGroupName' | 'programTypeName' | 'proponentName' | 'currentApproverName') { return this.optCache()[key]; }

  clearFilters() {
    this.searchTerm.set(''); this.setView('all');
    this.fStatus.set([]); this.fGroups.set([]); this.fTypes.set([]); this.fCustomer.set('');
    this.fProponents.set([]); this.fApprovers.set([]); this.costMin.set(null); this.costMax.set(null);
    this.progFrom.set(null); this.progTo.set(null); this.createdFrom.set(null); this.createdTo.set(null);
  }

  protected filtered = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const v = this.view();
    const st = new Set(this.fStatus()), sg = new Set(this.fGroups()), pt = new Set(this.fTypes());
    const pr = new Set(this.fProponents()), ap = new Set(this.fApprovers());
    const cust = this.fCustomer().toLowerCase().trim();
    const min = this.costMin(), max = this.costMax();
    const pFrom = ymd(this.progFrom()), pTo = ymd(this.progTo());
    const cFrom = ymd(this.createdFrom()), cTo = ymd(this.createdTo());
    const localDay = (s: string | null) => s ? ymd(new Date(utc(s)!)) : null;
    const rows = this.all().filter(t =>
      (v === 'all' || (v === 'mine' ? t.isMine : t.awaitingMe)) &&
      (!st.size || st.has(t.status)) &&
      (!sg.size || sg.has((t.salesGroupName ?? '').trim())) &&
      (!pt.size || pt.has((t.programTypeName ?? '').trim())) &&
      (!pr.size || pr.has(t.proponentName.trim())) &&
      (!ap.size || ap.has((t.currentApproverName ?? '').trim())) &&
      (!cust || `${t.chainName ?? ''} ${t.customerName ?? ''}`.toLowerCase().includes(cust)) &&
      (min == null || t.totalCost >= min) &&
      (max == null || t.totalCost <= max) &&
      // Program overlaps [from, to]: ends on/after From and starts on/before To.
      (!pFrom || (!!(t.endDate ?? t.startDate) && (t.endDate ?? t.startDate)!.slice(0, 10) >= pFrom)) &&
      (!pTo || (!!(t.startDate ?? t.endDate) && (t.startDate ?? t.endDate)!.slice(0, 10) <= pTo)) &&
      (!cFrom || localDay(t.createdAt)! >= cFrom) &&
      (!cTo || localDay(t.createdAt)! <= cTo));
    if (!term) return rows;
    return rows.filter(t =>
      [t.refNo, t.tparNo, t.status, t.title, t.salesGroupName, t.chainName, t.customerName, t.programTypeName, t.proponentName]
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
      searchValue:   this.searchTerm,
      filters:       { onClick: () => this.findVisible.set(true), active: this.activeFilters },
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
