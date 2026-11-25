import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { InputNumberModule } from 'primeng/inputnumber';
import { MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { ExportService } from '../../core/services/export.service';
import { TasafDashboard, TasafDashboardRow, TasafDashboardService } from '../../core/services/tasaf-dashboard.service';

type Totals = Omit<TasafDashboardRow, 'salesGroupId' | 'salesGroup' | 'chainId' | 'chain' | 'areaId' | 'area' | 'budgetEntered'>;

/**
 * TASAF Dashboard (/tasaf-dashboard, permission `tasaf-dashboard`; "TASAF Next Steps as of Nov 23.xlsx" › dashboard):
 * per year, the approved annual budget vs TASAFs created / approved / completed (with TPAR) / closed and the balance —
 * "By Sales Group" (LKA split by Area) and "By Account Chain". TMM / Admin (tasaf-masters) enter the
 * budgets here with Edit. Closed (actual spend) fills in once the post-evaluation step exists.
 */
@Component({
  selector: 'app-tasaf-dashboard-page',
  standalone: true,
  imports: [DecimalPipe, FormsModule, SelectModule, InputNumberModule],
  template: `
    <div class="page">
      <section class="card head">
        <h2>Budget Dashboard</h2>
        <label class="yr">Year
          <p-select [options]="years()" [ngModel]="year()" (ngModelChange)="pickYear($event)" styleClass="yr-sel" />
        </label>
        @if (editing()) { <span class="edit-note"><i class="pi pi-pencil"></i> Editing budgets for {{ year() }} — Save on the toolbar.</span> }
        <span class="muted note">Created = all but Draft / Cancelled · Approved = Approved + Completed · Completed = their approved TPARs ·
          Balance = Budget − Approved. LKA is shown by Area. A TASAF with several chains or areas splits its amount evenly.</span>
      </section>

      @for (v of views; track v.key) {
        <section class="card">
          <h3>{{ v.title }}</h3>
          <div class="tbl-wrap">
          <table class="dash">
            <thead>
              <tr>
                <th rowspan="2">Sales Group</th>
                @if (v.key === 'chain') { <th rowspan="2">Account Chain</th> }
                <th rowspan="2" class="num">Approved Annual Budget</th>
                <th colspan="2">TASAF Created</th>
                <th colspan="2">TASAF Approved</th>
                <th colspan="2">TASAF Completed (with TPAR)</th>
                <th colspan="2" title="Fills in once the post-evaluation step is added">TASAF Closed (Actual Spend)</th>
                <th rowspan="2" class="num">Balance Budget</th>
              </tr>
              <tr>
                <th class="num sub">No.</th><th class="num sub">Amount</th>
                <th class="num sub">No.</th><th class="num sub">Amount</th>
                <th class="num sub">No.</th><th class="num sub">Amount</th>
                <th class="num sub">No.</th><th class="num sub">Amount</th>
              </tr>
            </thead>
            <tbody>
              @for (r of rows(v.key); track $index; let first = $first) {
                <tr>
                  @if (v.key === 'chain') {
                    <td class="grp">{{ showGroup(v.key, $index) ? r.salesGroup : '' }}</td>
                    <td>{{ r.chain }}</td>
                  } @else {
                    <td class="grp">{{ r.area ? r.salesGroup + ' – ' + r.area : r.salesGroup }}</td>
                  }
                  <td class="num">
                    @if (editing() && r.salesGroupId > 0 && (r.chainId || r.areaId || (!r.chain && !r.area))) {
                      <p-inputNumber [ngModel]="budgetOf(r)" (ngModelChange)="setBudget(r, $event)" [min]="0" [minFractionDigits]="2"
                        [maxFractionDigits]="2" placeholder="—" inputStyleClass="bud-in" />
                    } @else {
                      <span [class.derived]="r.budgetEntered === null && r.budget !== 0" [class.neg]="r.budget < 0"
                        [title]="r.budgetEntered === null && r.budget !== 0
                          ? (r.chain && !r.chainId ? 'Sales group budget not given to any of its chains' : 'Total of its chains\\' budgets') : ''">{{ r.budget | number:'1.2-2' }}</span>
                    }
                  </td>
                  <td class="num">{{ r.createdCount }}</td><td class="num">{{ r.createdAmount | number:'1.2-2' }}</td>
                  <td class="num">{{ r.approvedCount }}</td><td class="num">{{ r.approvedAmount | number:'1.2-2' }}</td>
                  <td class="num">{{ r.completedCount }}</td><td class="num">{{ r.completedAmount | number:'1.2-2' }}</td>
                  <td class="num muted">{{ r.closedCount }}</td><td class="num muted">{{ r.closedAmount | number:'1.2-2' }}</td>
                  <td class="num" [class.neg]="r.balance < 0">{{ r.balance | number:'1.2-2' }}</td>
                </tr>
              } @empty {
                <tr><td [attr.colspan]="v.key === 'chain' ? 12 : 11" class="muted">Nothing for {{ year() }} yet.</td></tr>
              }
            </tbody>
            @if (total(v.key); as t) {
              <tfoot>
                <tr>
                  <td [attr.colspan]="v.key === 'chain' ? 2 : 1"><strong>TOTAL</strong></td>
                  <td class="num">{{ t.budget | number:'1.2-2' }}</td>
                  <td class="num">{{ t.createdCount }}</td><td class="num">{{ t.createdAmount | number:'1.2-2' }}</td>
                  <td class="num">{{ t.approvedCount }}</td><td class="num">{{ t.approvedAmount | number:'1.2-2' }}</td>
                  <td class="num">{{ t.completedCount }}</td><td class="num">{{ t.completedAmount | number:'1.2-2' }}</td>
                  <td class="num">{{ t.closedCount }}</td><td class="num">{{ t.closedAmount | number:'1.2-2' }}</td>
                  <td class="num" [class.neg]="t.balance < 0">{{ t.balance | number:'1.2-2' }}</td>
                </tr>
              </tfoot>
            }
          </table>
          </div>
        </section>
      }
    </div>
  `,
  styles: [`
    .page { padding: 0.25rem; }
    .card { background: var(--p-surface-card); border-radius: 8px; padding: 1rem 1.25rem; margin-bottom: 0.85rem; }
    .head { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; }
    .head h2 { margin: 0; font-size: 1.1rem; color: #800000; letter-spacing: .03em; }
    .card h3 { margin: 0 0 .6rem; font-size: .95rem; color: #800000; text-transform: uppercase; letter-spacing: .04em; }
    .yr { display: flex; align-items: center; gap: .5rem; font-weight: 500; }
    .note { flex-basis: 100%; font-size: .8rem; }
    .edit-note { color: #b45309; font-weight: 600; font-size: .85rem; }
    .tbl-wrap { overflow-x: auto; }
    table.dash { width: 100%; border-collapse: collapse; font-size: .85rem; }
    .dash th { background: #800000; color: #fff; padding: .45rem .5rem; font-weight: 600; border: 1px solid #a33; text-align: center; }
    .dash th.sub { background: #9b2a2a; font-size: .75rem; }
    .dash td { padding: .35rem .5rem; border-bottom: 1px solid var(--p-surface-border); white-space: nowrap; }
    .dash tbody tr:hover td { background: var(--p-surface-hover, #f6f6f6); }
    .dash tfoot td { border-top: 2px solid #800000; font-weight: 700; background: #faf3f3; }
    .grp { font-weight: 600; }
    .num { text-align: right; }
    .neg { color: #c62828; font-weight: 600; }
    .muted { color: var(--p-text-muted-color); }
    .derived { font-style: italic; color: var(--p-text-muted-color); }
    :host ::ng-deep .bud-in { width: 9rem; text-align: right; padding: .2rem .4rem; }
  `]
})
export class TasafDashboardPageComponent implements OnInit, OnDestroy {
  private api     = inject(TasafDashboardService);
  private toolbar = inject(GlobalToolbarService);
  private msg     = inject(MessageService);
  private exporter = inject(ExportService);

  protected readonly views = [
    { key: 'group' as const, title: 'By Sales Group' },
    { key: 'chain' as const, title: 'By Account Chain' },
  ];
  protected data    = signal<TasafDashboard | null>(null);
  protected year    = signal<number | null>(null);
  protected years   = computed(() => this.data()?.years ?? []);
  protected loading = signal(false);
  protected saving  = signal(false);
  protected editing = signal(false);
  /** Budgets being edited: "salesGroupId:chainId" → amount (null = none). */
  private draft = new Map<string, number | null>();

  private notEditable = computed(() => !this.data()?.canEditBudget || this.loading());
  private saveOff     = computed(() => !this.editing() || this.saving());

  ngOnInit() { this.setToolbar(); this.load(); }
  ngOnDestroy() { this.toolbar.clear(); }

  private setToolbar() {
    this.toolbar.set({
      title: 'TASAF Dashboard',
      edit:    { onClick: () => this.startEdit(), disabled: this.notEditable, label: 'Edit budgets' },
      save:    { onClick: () => this.save(), loading: this.saving, disabled: this.saveOff },
      cancel:  { onClick: () => this.cancelEdit(), disabled: computed(() => !this.editing()) },
      refresh: { onClick: () => this.load(), loading: this.loading },
      export:  { excel: () => this.exportExcel() },
    });
  }

  rows(view: 'group' | 'chain') { return (view === 'group' ? this.data()?.bySalesGroup : this.data()?.byChain) ?? []; }
  /** Chain view: the sales group shows on its first row only. */
  showGroup(view: 'group' | 'chain', i: number) {
    const r = this.rows(view); return i === 0 || r[i - 1].salesGroupId !== r[i].salesGroupId;
  }

  total(view: 'group' | 'chain'): Totals | null {
    const r = this.rows(view); if (!r.length) return null;
    const sum = (f: (x: TasafDashboardRow) => number) => r.reduce((a, x) => a + f(x), 0);
    return {
      budget: sum(x => x.budget), createdCount: sum(x => x.createdCount), createdAmount: sum(x => x.createdAmount),
      approvedCount: sum(x => x.approvedCount), approvedAmount: sum(x => x.approvedAmount),
      completedCount: sum(x => x.completedCount), completedAmount: sum(x => x.completedAmount),
      closedCount: sum(x => x.closedCount), closedAmount: sum(x => x.closedAmount), balance: sum(x => x.balance),
    };
  }

  pickYear(y: number) {
    if (this.editing() && !confirm('Discard the budget changes?')) return;
    this.editing.set(false); this.year.set(y); this.load();
  }

  load() {
    this.loading.set(true);
    this.api.get(this.year()).subscribe({
      next: res => { this.loading.set(false); this.data.set(res.data ?? null); this.year.set(res.data?.year ?? null); },
      error: err => { this.loading.set(false); this.fail(err, 'Failed to load the dashboard.'); }
    });
  }

  // ── Budgets ──
  private key(r: TasafDashboardRow) { return `${r.salesGroupId}:${r.chainId ?? ''}:${r.areaId ?? ''}`; }
  budgetOf(r: TasafDashboardRow) { return this.draft.has(this.key(r)) ? this.draft.get(this.key(r)) : r.budgetEntered; }
  setBudget(r: TasafDashboardRow, v: number | null) { this.draft.set(this.key(r), v); }

  startEdit() { this.draft.clear(); this.editing.set(true); }
  cancelEdit() { this.draft.clear(); this.editing.set(false); }

  save() {
    const y = this.year(); if (!y || this.saveOff()) return;
    const items = [...this.draft.entries()].map(([k, amount]) => {
      const [g, c, a] = k.split(':'); return { salesGroupId: +g, chainId: c ? +c : null, areaId: a ? +a : null, amount };
    });
    if (!items.length) { this.editing.set(false); return; }
    this.saving.set(true);
    this.api.saveBudgets(y, items).subscribe({
      next: () => {
        this.saving.set(false); this.editing.set(false); this.draft.clear();
        this.msg.add({ severity: 'success', summary: 'Budget Dashboard', detail: `Budgets for ${y} saved.` });
        this.load();
      },
      error: err => { this.saving.set(false); this.fail(err, 'Failed to save the budgets.'); }
    });
  }

  exportExcel() {
    const headers = ['View', 'Sales Group', 'Account Chain / Area', 'Approved Annual Budget', 'TASAF Created (No.)', 'TASAF Created (Amount)',
      'TASAF Approved (No.)', 'TASAF Approved (Amount)', 'TASAF Completed (No.)', 'TASAF Completed (with TPAR)',
      'TASAF Closed (No.)', 'TASAF Closed (Actual Spend)', 'Balance Budget'];
    const line = (view: string, r: TasafDashboardRow): (string | number | null)[] => [view, r.salesGroup, r.chain ?? r.area ?? '', r.budget,
      r.createdCount, r.createdAmount, r.approvedCount, r.approvedAmount, r.completedCount, r.completedAmount, r.closedCount, r.closedAmount, r.balance];
    const rows = [...this.rows('group').map(r => line('By Sales Group', r)), ...this.rows('chain').map(r => line('By Account Chain', r))];
    this.exporter.exportExcelRows(`TASAF Budget Dashboard ${this.year()}`, `${this.year()}`, headers, rows, [3, 5, 7, 9, 11, 12]);
  }

  private fail(err: any, fallback: string) {
    this.msg.add({ severity: 'error', summary: 'Budget Dashboard', detail: err?.error?.message ?? fallback, life: 8000 });
  }
}
