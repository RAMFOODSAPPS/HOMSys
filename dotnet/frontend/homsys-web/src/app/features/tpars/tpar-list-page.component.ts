import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { TparService } from '../../core/services/tpar.service';
import { TASAF_STATUS_SEVERITY } from '../../core/models/tasaf.model';
import { Tpar, TPAR_STATUS_SEVERITY, TparTasaf } from '../../core/models/tpar.model';

/**
 * TPAR List (/tpars/list, permission `tpar`): the Approved TASAFs with their TPAR totals and balance, and the TPARs.
 * Picking a TASAF filters the TPARs; clicking a TPAR opens it on the New TPAR form (its own tab); "+" on a TASAF row
 * starts a new TPAR for it.
 */
@Component({
  selector: 'app-tpar-list-page',
  standalone: true,
  imports: [DatePipe, DecimalPipe, FormsModule, TableModule, TagModule, ButtonModule, CheckboxModule, TooltipModule],
  template: `
    <div class="page">
      <section class="card">
        <div class="sec-head">
          <h3>Approved TASAFs</h3>
          <span class="muted">Click a TASAF to see its TPARs.</span>
          <label class="chk"><p-checkbox [binary]="true" [(ngModel)]="showCompleted" (onChange)="loadTasafs()" /> Show Completed</label>
        </div>
        <p-table [value]="filteredTasafs()" [loading]="loading()" selectionMode="single" [selection]="selected()" (selectionChange)="selectTasaf($event)"
          dataKey="id" [paginator]="filteredTasafs().length > 10" [rows]="10" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th pSortableColumn="refNo">Document No. <p-sortIcon field="refNo" /></th>
              <th pSortableColumn="title">Title <p-sortIcon field="title" /></th>
              <th pSortableColumn="salesGroupName">Sales Group <p-sortIcon field="salesGroupName" /></th>
              <th pSortableColumn="proponentName">Proponent <p-sortIcon field="proponentName" /></th>
              <th pSortableColumn="approvedAmount" class="num">Approved Amount <p-sortIcon field="approvedAmount" /></th>
              <th pSortableColumn="tparTotal" class="num">TPAR Total <p-sortIcon field="tparTotal" /></th>
              <th pSortableColumn="approvedTotal" class="num">TPAR Approved <p-sortIcon field="approvedTotal" /></th>
              <th pSortableColumn="balance" class="num">Balance <p-sortIcon field="balance" /></th>
              <th pSortableColumn="tparCount" class="num">TPARs <p-sortIcon field="tparCount" /></th>
              <th pSortableColumn="status">Status <p-sortIcon field="status" /></th>
              <th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-t>
            <tr [pSelectableRow]="t">
              <td class="nowrap"><strong>{{ t.refNo }}</strong></td>
              <td>{{ t.title }}</td>
              <td>{{ t.salesGroupName }}</td>
              <td>{{ t.proponentName }}</td>
              <td class="num">{{ t.approvedAmount | number:'1.2-2' }}</td>
              <td class="num">{{ t.tparTotal | number:'1.2-2' }}</td>
              <td class="num">{{ t.approvedTotal | number:'1.2-2' }}</td>
              <td class="num" [class.zero]="t.balance === 0">{{ t.balance | number:'1.2-2' }}</td>
              <td class="num">{{ t.tparCount }}</td>
              <td><p-tag [value]="t.status" [severity]="tasafSev(t.status)" /></td>
              <td class="nowrap">
                @if (t.status === 'Approved' && t.balance > 0) {
                  <p-button icon="pi pi-plus" size="small" [text]="true" pTooltip="New TPAR for this TASAF" (onClick)="newFor(t, $event)" />
                }
              </td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="11" class="muted">{{ term() ? 'No TASAF matches the search.' : 'No Approved TASAF waiting for TPARs.' }}</td></tr>
          </ng-template>
        </p-table>
      </section>

      <section class="card">
        <div class="sec-head">
          @if (selected(); as t) {
            <h3>TPARs — {{ t.refNo }} {{ t.title }}</h3>
            <span class="acts"><p-button icon="pi pi-times" size="small" [text]="true" severity="secondary" pTooltip="Show all TPARs" (onClick)="selectTasaf(null)" /></span>
          } @else {
            <h3>All TPARs</h3><span class="muted">Click a TPAR to open it.</span>
          }
        </div>
        <p-table [value]="filteredTpars()" [loading]="loadingTpars()" selectionMode="single" (selectionChange)="open($event)"
          dataKey="id" [paginator]="filteredTpars().length > 15" [rows]="15" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th pSortableColumn="tparNo">TPAR No. <p-sortIcon field="tparNo" /></th>
              <th pSortableColumn="status">Status <p-sortIcon field="status" /></th>
              @if (!selected()) { <th pSortableColumn="tasafRefNo">TASAF <p-sortIcon field="tasafRefNo" /></th> }
              <th pSortableColumn="branchName">Branch <p-sortIcon field="branchName" /></th>
              <th pSortableColumn="amount" class="num">Amount <p-sortIcon field="amount" /></th>
              <th>Remarks</th>
              <th pSortableColumn="createdByName">Entered by <p-sortIcon field="createdByName" /></th>
              <th pSortableColumn="createdAt">Date <p-sortIcon field="createdAt" /></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-p>
            <tr [pSelectableRow]="p" [class.cancelled]="p.status === 'Cancelled'">
              <td class="nowrap"><strong>{{ p.tparNo }}</strong></td>
              <td><p-tag [value]="p.status" [severity]="tparSev(p.status)" /></td>
              @if (!selected()) { <td>{{ p.tasafRefNo }} <span class="muted">{{ p.tasafTitle }}</span></td> }
              <td>{{ p.branchName }}</td>
              <td class="num">{{ p.amount | number:'1.2-2' }}</td>
              <td>{{ p.remarks }}</td>
              <td>{{ p.createdByName }}</td>
              <td class="nowrap">{{ utc(p.createdAt) | date:'dd MMM yyyy h:mm a' }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td [attr.colspan]="selected() ? 7 : 8" class="muted">No TPAR yet.</td></tr>
          </ng-template>
        </p-table>
      </section>
    </div>
  `,
  styles: [`
    .page { padding: 0.25rem; }
    .card { background: var(--p-surface-card); border-radius: 8px; padding: 1rem 1.25rem; margin-bottom: 0.85rem; }
    .card h3 { margin: 0; font-size: 0.95rem; color: #800000; }
    .sec-head { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 0.6rem; flex-wrap: wrap; }
    .sec-head .acts, .sec-head .chk { margin-left: auto; display: flex; align-items: center; gap: 0.35rem; }
    .num { text-align: right; }
    .nowrap { white-space: nowrap; }
    .zero { color: #2e7d32; font-weight: 600; }
    .muted { color: var(--p-text-muted-color); }
    tr.cancelled td { color: var(--p-text-muted-color); text-decoration: line-through; }
  `]
})
export class TparListPageComponent implements OnInit, OnDestroy {
  private api     = inject(TparService);
  private toolbar = inject(GlobalToolbarService);
  private msg     = inject(MessageService);
  private router  = inject(Router);

  protected tasafs       = signal<TparTasaf[]>([]);
  protected tpars        = signal<Tpar[]>([]);
  protected selected     = signal<TparTasaf | null>(null);
  protected term         = signal((history.state?.['searchTerm'] as string | undefined) ?? '');
  protected loading      = signal(false);
  protected loadingTpars = signal(false);
  protected showCompleted = false;

  protected filteredTasafs = computed(() => {
    const q = this.term().trim().toLowerCase();
    return q ? this.tasafs().filter(t => [t.refNo, t.title, t.salesGroupName, t.programTypeName, t.proponentName]
      .some(v => (v ?? '').toLowerCase().includes(q))) : this.tasafs();
  });
  protected filteredTpars = computed(() => {
    const q = this.term().trim().toLowerCase();
    return q ? this.tpars().filter(p => [p.tparNo, p.branchName, p.tasafRefNo, p.tasafTitle, p.remarks, p.createdByName, p.status]
      .some(v => (v ?? '').toLowerCase().includes(q))) : this.tpars();
  });

  ngOnInit() {
    this.toolbar.set({
      title:   'TPAR List',
      add:     { onClick: () => this.router.navigate(['/tpars']) },
      refresh: { onClick: () => this.reload(), loading: this.loading },
      search:  term => this.term.set(term),
      initialSearch: this.term(),
    });
    this.reload();
  }

  ngOnDestroy() { this.toolbar.clear(); }

  tasafSev(status: string) { return TASAF_STATUS_SEVERITY[status] ?? 'info'; }
  tparSev(status: string) { return TPAR_STATUS_SEVERITY[status] ?? 'info'; }
  /** Server times are UTC without a zone marker. */
  utc(v: string | null) { return v ? (v.endsWith('Z') ? v : v + 'Z') : null; }

  reload() { this.loadTasafs(); this.loadTpars(); }

  loadTasafs() {
    this.loading.set(true);
    this.api.tasafs(this.showCompleted).subscribe({
      next: res => {
        this.loading.set(false);
        const list = res.data ?? [];
        this.tasafs.set(list);
        const id = this.selected()?.id;
        this.selected.set(id ? list.find(t => t.id === id) ?? null : null);
      },
      error: err => { this.loading.set(false); this.fail(err, 'Failed to load the TASAFs.'); }
    });
  }

  loadTpars() {
    this.loadingTpars.set(true);
    this.api.list(this.selected()?.id).subscribe({
      next: res => { this.loadingTpars.set(false); this.tpars.set(res.data ?? []); },
      error: err => { this.loadingTpars.set(false); this.fail(err, 'Failed to load the TPARs.'); }
    });
  }

  selectTasaf(t: TparTasaf | null) {
    this.selected.set(t);
    this.loadTpars();
  }

  /** Opens the TPAR on the form, in its own tab. */
  open(p: Tpar | null) {
    if (p) this.router.navigate(['/tpars'], { state: { tparId: p.id } });
  }

  /** A new TPAR with this TASAF already picked. */
  newFor(t: TparTasaf, e: Event) {
    e.stopPropagation();
    this.router.navigate(['/tpars'], { state: { tparTasafId: t.id } });
  }

  private fail(err: any, fallback: string) {
    this.msg.add({ severity: 'error', summary: 'TPAR', detail: err?.error?.message ?? fallback, life: 8000 });
  }
}
