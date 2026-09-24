import {
  Component, DestroyRef, computed, effect, inject, input, output, signal, untracked, viewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, debounceTime, distinctUntilChanged, filter, finalize, map, merge, skip, switchMap, tap } from 'rxjs';
import { ChartModule, UIChart } from 'primeng/chart';
import { TableModule } from 'primeng/table';
import { MenuModule, Menu } from 'primeng/menu';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { MenuItem } from 'primeng/api';
import { AnalyticsService } from '../../core/services/analytics.service';
import { AuthService } from '../../core/services/auth.service';
import { ExportService } from '../../core/services/export.service';
import { ColumnMeta, QueryResult, ReportSpec, SpecFilter } from '../../core/models/analytics.model';
import {
  ColorMap, OTHERS, buildChart, cleanSpec, daysSeverity, dimText, sliceLabelsPlugin, drillTarget, excludeFor, filterFor, formatValue,
  mergeFilters, recordsSpec, shapeOf
} from './analytics-viz';

interface Clicked { col: ColumnMeta; key: unknown; caption: unknown; series?: { col: ColumnMeta; key: unknown; caption: unknown } }

export interface CrossFilterEvent { col: ColumnMeta; key: unknown; caption: unknown; multi: boolean }

export interface PdfSection { title: string; image: string | null; columns: string[]; rows: string[][] }

/**
 * Renders one report spec as KPI / table / matrix / chart. Owns its own
 * drill stack and "See records" drill-through; in dashboard mode a click
 * emits a cross-filter instead and right-click opens the action menu.
 */
@Component({
  selector: 'app-analytics-widget',
  standalone: true,
  imports: [CommonModule, ChartModule, TableModule, MenuModule, DialogModule, ButtonModule, TooltipModule],
  // height > 0 = fixed height (builder preview, records dialog); 0 = fill the parent (dashboard cells).
  host: { class: 'an-widget-host', '[style.height.px]': 'height() > 0 ? height() : null' },
  template: `
    <div class="an-widget" [class.an-busy]="loading() && !!result()">
      <div class="an-widget-head">
        <div class="an-widget-title">
          @if (title()) { <span class="an-title-text">{{ title() }}</span> }
          @if (drillStack().length) {
            <span class="an-crumbs">
              <a (click)="drillTo(0)">All</a>
              @for (c of drillStack(); track $index; let i = $index) {
                <i class="pi pi-angle-right"></i>
                @if (i < drillStack().length - 1) { <a (click)="drillTo(i + 1)">{{ c.label }}</a> } @else { <span>{{ c.label }}</span> }
              }
            </span>
          }
        </div>
        <div class="an-widget-tools">
          @if (notFiltered()) {
            <i class="pi pi-filter-slash an-muted" pTooltip="This widget's dataset doesn't have the field(s) the dashboard is filtered by" tooltipPosition="left"></i>
          }
          @if (!autoRun() && !loading()) {
            <p-button icon="pi pi-play" label="Run" size="small" [text]="true" (onClick)="run()" />
          }
          <p-button icon="pi pi-ellipsis-v" size="small" [text]="true" [rounded]="true" (onClick)="toolsMenu.toggle($event)" ariaLabel="Widget actions" />
          <p-menu #toolsMenu [popup]="true" [model]="toolItems()" appendTo="body" />
        </div>
      </div>

      <div class="an-widget-body">
        @if (error()) {
          <div class="an-empty an-error"><i class="pi pi-exclamation-circle"></i> {{ error() }}</div>
        } @else if (!result()) {
          <div class="an-empty">
            @if (loading()) { <i class="pi pi-spin pi-spinner"></i> Loading… }
            @else if (!autoRun()) { Press Run to load this dataset. }
            @else { Add fields to build the visual. }
          </div>
        } @else if (isEmpty()) {
          <div class="an-empty">No data for the current filters.</div>
        } @else if (effectiveType() === 'kpi') {
          <div class="an-kpis">
            @for (k of kpis(); track $index) {
              <div class="an-kpi">
                <div class="an-kpi-value">{{ k.value }}</div>
                <div class="an-kpi-label">{{ k.label }}</div>
                @if (k.delta !== null) {
                  <div class="an-kpi-delta" [class.up]="k.delta > 0" [class.down]="k.delta < 0">
                    <i class="pi" [class.pi-arrow-up]="k.delta > 0" [class.pi-arrow-down]="k.delta < 0" [class.pi-minus]="k.delta === 0"></i>
                    {{ k.deltaText }} <span class="an-muted">{{ result()!.compareLabel }}</span>
                  </div>
                }
              </div>
            }
          </div>
        } @else if (effectiveType() === 'pivot' && pivot()) {
          <div class="an-scroll">
            <table class="an-pivot">
              <thead>
                <tr>
                  <th>{{ pivot()!.rowLabel }}</th>
                  @for (h of pivot()!.headers; track $index) { <th class="num">{{ h }}</th> }
                </tr>
              </thead>
              <tbody>
                @for (r of pivot()!.rows; track $index) {
                  <tr (click)="onRowClick($event, r.click)" (contextmenu)="onRowMenu($event, r.click)">
                    <th>{{ r.label }}</th>
                    @for (v of r.cells; track $index; let last = $last) { <td class="num" [class.an-margin]="last">{{ v }}</td> }
                  </tr>
                }
              </tbody>
              <tfoot>
                <tr><th>Total</th>@for (v of pivot()!.totals; track $index) { <td class="num">{{ v }}</td> }</tr>
              </tfoot>
            </table>
          </div>
        } @else if (effectiveType() === 'table') {
          <p-table [value]="table().rows" [paginator]="table().rows.length > pageSize()" [rows]="pageSize()"
                   [scrollable]="true" scrollHeight="flex" size="small" styleClass="an-table">
            <ng-template pTemplate="header">
              <tr>@for (c of table().cols; track c.key) { <th [class.num]="c.num" [pTooltip]="c.tip" tooltipPosition="top">{{ c.label }}</th> }</tr>
            </ng-template>
            <ng-template pTemplate="body" let-row>
              <tr (click)="onRowClick($event, row.click)" (contextmenu)="onRowMenu($event, row.click)" [class.an-others]="row.others">
                @for (c of table().cols; track c.key; let i = $index) {
                  <td [class.num]="c.num">
                    @if (c.soLink && row.soId && canOpenOrders) {
                      <a class="an-link" (click)="$event.stopPropagation(); openOrder(row.soId)">{{ row.cells[i] }}</a>
                    } @else if (c.severity && row.sev[i]) {
                      <span class="an-sev" [class]="row.sev[i].cls"><i class="pi" [class]="row.sev[i].icon"></i> {{ row.cells[i] }} · {{ row.sev[i].label }}</span>
                    } @else { {{ row.cells[i] }} }
                  </td>
                }
              </tr>
            </ng-template>
            <ng-template pTemplate="footer">
              @if (table().total) {
                <tr class="an-total">@for (v of table().total; track $index) { <td [class.num]="table().cols[$index].num">{{ v }}</td> }</tr>
              }
            </ng-template>
          </p-table>
        } @else if (chart()) {
          <div class="an-chart" (contextmenu)="onChartMenu($event)">
            <p-chart #uiChart [type]="chartJsType()" [data]="chart()!.data" [options]="chart()!.options" [plugins]="chartPlugins"
                     height="100%" (onDataSelect)="onChartSelect($event)" />
          </div>
        }
      </div>

      @if (result() && !compact()) {
        <div class="an-widget-foot">
          @if (result()!.asOf) { <span>Data as of {{ result()!.asOf | date:'MMM d, y h:mm a' }}</span> }
          @if (result()!.scope) { <span><i class="pi pi-lock"></i> {{ result()!.scope }} only</span> }
          @if (result()!.truncated) { <span class="an-warn"><i class="pi pi-exclamation-triangle"></i> Row limit reached — add filters</span> }
        </div>
      }
    </div>

    <p-menu #pointMenu [popup]="true" [model]="pointItems()" appendTo="body" />

    <p-dialog [header]="'Records' + (recordsLabel() ? ' — ' + recordsLabel() : '')" [visible]="!!records()" (visibleChange)="!$event && records.set(null)"
              [modal]="true" [style]="{ width: 'min(1100px, 95vw)' }" [draggable]="false" appendTo="body">
      @if (records()) {
        <app-analytics-widget [spec]="records()!" [height]="480" />
      }
    </p-dialog>
  `,
})
export class AnalyticsWidgetComponent {
  private api = inject(AnalyticsService);
  private auth = inject(AuthService);
  private router = inject(Router);
  private exportService = inject(ExportService);
  private destroyRef = inject(DestroyRef);

  readonly spec = input.required<ReportSpec>();
  readonly title = input('');
  readonly externalFilters = input<SpecFilter[]>([]);
  /** Dashboard mode: click = cross-filter, right-click = menu. */
  readonly crossFilterMode = input(false);
  readonly selectedKeys = input<string[] | null>(null);
  readonly autoRun = input(true);
  readonly height = input(300);
  readonly compact = input(false);

  readonly crossFilter = output<CrossFilterEvent>();
  readonly filterDashboard = output<SpecFilter>();
  readonly openAsReport = output<ReportSpec>();

  private uiChart = viewChild<UIChart>('uiChart');
  private pointMenu = viewChild.required<Menu>('pointMenu');

  readonly meta = toSignal(this.api.meta());
  readonly ds = computed(() => this.meta()?.datasets.find(d => d.key === this.spec().dataset));
  readonly drillStack = signal<{ spec: ReportSpec; label: string }[]>([]);
  readonly current = computed(() => this.drillStack().at(-1)?.spec ?? this.spec());
  readonly effective = computed(() => cleanSpec(mergeFilters(this.current(), this.ds(), this.externalFilters())));
  readonly result = signal<QueryResult | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal(false);
  readonly asTable = signal(false);
  readonly records = signal<ReportSpec | null>(null);
  readonly recordsLabel = signal('');
  readonly pointItems = signal<MenuItem[]>([]);
  private readonly runToken = signal(0);
  private colors = new ColorMap();
  readonly chartPlugins = [sliceLabelsPlugin];
  readonly canOpenOrders = this.auth.isAdmin() || this.auth.hasPermission('sales-orders');

  readonly effectiveType = computed(() => this.asTable() ? 'table' : this.current().type);
  readonly chartJsType = computed(() => this.current().type as 'bar' | 'line' | 'pie' | 'doughnut');
  readonly pageSize = computed(() => this.height() > 0 && this.height() < 400 ? 10 : 50);

  /** External filters on fields this dataset doesn't have (dashboard-level). */
  readonly notFiltered = computed(() => {
    const ds = this.ds();
    return !!ds && this.externalFilters().some(f => !(f.op === 'in' && !f.values?.length) && !ds.fields.some(x => x.key === f.field));
  });

  readonly isEmpty = computed(() => {
    const r = this.result();
    if (!r) return true;
    return r.rows.length === 0 || (this.current().type === 'kpi' && r.total?.every(v => v === null));
  });

  constructor() {
    // A new base spec (edited in the designer / different widget) resets drilling.
    effect(() => { this.spec(); untracked(() => this.drillStack.set([])); });

    // Only a spec that actually changed re-queries: parents may hand us a fresh
    // (but equal) filters array on every change detection.
    merge(
      toObservable(this.effective).pipe(
        filter(() => this.autoRun()),
        map(s => JSON.stringify(s)),
        distinctUntilChanged(),
        map(j => JSON.parse(j) as ReportSpec),
      ),
      toObservable(this.runToken).pipe(skip(1), map(() => this.effective())),
    ).pipe(
      debounceTime(250),
      filter(s => !!s.dataset && s.measures.length > 0),
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(s => this.api.query(s).pipe(
        catchError(err => {
          this.error.set(err?.error?.message ?? 'Could not load this visual.');
          this.result.set(null);
          return EMPTY;
        }),
        finalize(() => this.loading.set(false)),
      )),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(r => this.result.set(r));
  }

  run() { this.runToken.update(n => n + 1); }

  // ── view models ──────────────────────────────────────────────────────────

  readonly chart = computed(() => {
    const r = this.result();
    const sel = this.selectedKeys();
    return r ? buildChart(this.current(), r, this.colors, sel ? new Set(sel) : null) : null;
  });

  readonly kpis = computed(() => {
    const r = this.result();
    if (!r) return [];
    const s = shapeOf(r);
    const row = r.total ?? r.rows[0] ?? [];
    return s.m.map(m => {
      const v = row[m.idx];
      const prev = r.compare?.[m.idx];
      const delta = prev !== undefined && prev !== null && Number(prev) !== 0 && v !== null
        ? (Number(v) - Number(prev)) / Math.abs(Number(prev)) : null;
      return {
        label: m.col.label,
        value: formatValue(v, m.col.format, m.col.type),
        delta,
        deltaText: delta === null ? '' : `${delta > 0 ? '+' : ''}${(delta * 100).toFixed(1)}%`,
      };
    });
  });

  readonly table = computed(() => {
    const r = this.result();
    if (!r) return { cols: [], rows: [], total: null as string[] | null };
    const s = shapeOf(r);
    const fields = this.ds()?.fields ?? [];
    const cols = [
      ...s.d.map(d => ({ key: d.col.key, label: d.col.label, num: false, soLink: d.col.field === 'soId', severity: false, tip: fields.find(f => f.key === d.col.field)?.description ?? '' })),
      ...s.m.map(m => ({ key: m.col.key, label: m.col.label, num: true, soLink: false, severity: m.col.format === 'daysSev', tip: fields.find(f => f.key === m.col.field)?.description ?? '' })),
    ];
    const soIdx = s.d.find(d => d.col.field === 'soId')?.key;
    const toRow = (x: unknown[], others: boolean) => ({
      others,
      soId: soIdx !== undefined ? x[soIdx] : null,
      click: s.d.length ? { col: s.d[0].col, key: x[s.d[0].key], caption: s.d[0].cap >= 0 ? x[s.d[0].cap] : null } as Clicked : null,
      cells: [
        ...s.d.map(d => dimText(x[d.key], d.cap >= 0 ? x[d.cap] : null, d.col)),
        ...s.m.map(m => formatValue(x[m.idx], m.col.format, m.col.type)),
      ],
      sev: [...s.d.map(() => null), ...s.m.map(m => m.col.format === 'daysSev' ? daysSeverity(x[m.idx]) : null)],
    });
    const rows = [...r.rows.map(x => toRow(x, false)), ...r.othersRows.map(x => toRow(x, true))];
    const total = r.total && s.d.length
      ? [...s.d.map((_, i) => i === 0 ? 'Total' : ''), ...s.m.map(m => formatValue(r.total![m.idx], m.col.format, m.col.type))]
      : null;
    return { cols, rows, total };
  });

  readonly pivot = computed(() => {
    const r = this.result();
    if (!r) return null;
    const s = shapeOf(r);
    if (s.d.length !== 2) return null;
    const [d0, d1] = s.d;
    const k = (v: unknown) => v === null || v === undefined ? '\u0000' : String(v);
    const uniq = (rows: unknown[][], d: typeof d0) => {
      const seen = new Map<string, { key: unknown; caption: unknown }>();
      for (const x of rows) if (!seen.has(k(x[d.key]))) seen.set(k(x[d.key]), { key: x[d.key], caption: d.cap >= 0 ? x[d.cap] : null });
      return [...seen.values()];
    };
    const rowKeys = uniq(r.rowTotals?.length ? r.rowTotals : r.rows, d0);
    const colKeys = uniq(r.colTotals?.length ? r.colTotals : r.rows, d1).map(c => {
      const sample = r.rows.find(x => k(x[d1.key]) === k(c.key));
      return { ...c, caption: sample && d1.cap >= 0 ? sample[d1.cap] : c.caption };
    });
    const cell = new Map(r.rows.map(x => [k(x[d0.key]) + '|' + k(x[d1.key]), x]));
    const rowTot = new Map((r.rowTotals ?? []).map(x => [k(x[d0.key]), x]));
    const colTot = new Map((r.colTotals ?? []).map(x => [k(x[d1.key]), x]));
    const multi = s.m.length > 1;
    const f = (x: unknown[] | undefined, m: typeof s.m[0]) => x ? formatValue(x[m.idx], m.col.format, m.col.type) : '';
    return {
      rowLabel: d0.col.label,
      headers: [
        ...colKeys.flatMap(c => s.m.map(m => dimText(c.key, c.caption, d1.col) + (multi ? ' · ' + m.col.label : ''))),
        ...s.m.map(m => 'Total' + (multi ? ' · ' + m.col.label : '')),
      ],
      rows: rowKeys.map(rk => {
        const sample = r.rows.find(x => k(x[d0.key]) === k(rk.key));
        const caption = sample && d0.cap >= 0 ? sample[d0.cap] : rk.caption;
        return {
          label: dimText(rk.key, caption, d0.col),
          click: { col: d0.col, key: rk.key, caption } as Clicked,
          cells: [
            ...colKeys.flatMap(c => s.m.map(m => f(cell.get(k(rk.key) + '|' + k(c.key)), m))),
            ...s.m.map(m => f(rowTot.get(k(rk.key)), m)),
          ],
        };
      }),
      totals: [
        ...colKeys.flatMap(c => s.m.map(m => f(colTot.get(k(c.key)), m))),
        ...s.m.map(m => f(r.total ?? undefined, m)),
      ],
    };
  });

  readonly toolItems = computed<MenuItem[]>(() => {
    const t = this.current().type;
    const items: MenuItem[] = [
      { label: 'Refresh', icon: 'pi pi-refresh', command: () => this.run() },
      { label: 'Export to Excel', icon: 'pi pi-file-excel', command: () => this.exportExcel() },
    ];
    if (t !== 'table' && t !== 'kpi') {
      items.push({ label: this.asTable() ? 'Show as visual' : 'Show as table', icon: 'pi pi-table', command: () => this.asTable.update(v => !v) });
    }
    if (this.ds()?.detail.length) items.push({ label: 'See all records', icon: 'pi pi-list', command: () => this.openRecords([], '') });
    if (this.drillStack().length) items.push({ label: 'Reset drill', icon: 'pi pi-replay', command: () => this.drillStack.set([]) });
    items.push({ label: 'Open as report', icon: 'pi pi-external-link', command: () => this.openAsReport.emit(this.current()) });
    return items;
  });

  // ── interactions ─────────────────────────────────────────────────────────

  onChartSelect(e: { originalEvent: MouseEvent; element: { datasetIndex: number; index: number } }) {
    const p = this.pointAt(e.element.datasetIndex, e.element.index);
    if (!p) return;
    if (this.crossFilterMode() && p.key !== OTHERS) {
      this.crossFilter.emit({ col: p.col, key: p.key, caption: p.caption, multi: e.originalEvent?.ctrlKey || e.originalEvent?.metaKey });
    } else {
      this.openPointMenu(e.originalEvent, p);
    }
  }

  onChartMenu(e: MouseEvent) {
    const chart = this.uiChart()?.chart;
    if (!chart) return;
    const hits = chart.getElementsAtEventForMode(e, 'nearest', { intersect: true }, true);
    if (!hits?.length) return;
    const p = this.pointAt(hits[0].datasetIndex, hits[0].index);
    if (!p) return;
    e.preventDefault();
    this.openPointMenu(e, p);
  }

  onRowClick(e: MouseEvent, c: Clicked | null) {
    if (!c) return;
    if (this.crossFilterMode() && c.key !== OTHERS) this.crossFilter.emit({ ...c, multi: e.ctrlKey || e.metaKey });
    else this.openPointMenu(e, c);
  }

  onRowMenu(e: MouseEvent, c: Clicked | null) {
    if (!c) return;
    e.preventDefault();
    this.openPointMenu(e, c);
  }

  private pointAt(datasetIndex: number, index: number): Clicked | null {
    const r = this.result();
    const pt = this.chart()?.points[datasetIndex]?.[index];
    if (!r || !pt) return null;
    const s = shapeOf(r);
    const clicked: Clicked = { col: s.d[0].col, key: pt.key, caption: pt.caption };
    if (pt.series && s.d[1]) clicked.series = { col: s.d[1].col, key: pt.series.key, caption: pt.series.caption };
    return clicked;
  }

  private openPointMenu(event: Event, c: Clicked) {
    const ds = this.ds();
    const label = dimText(c.key, c.caption, c.col);
    const isOthers = c.key === OTHERS || c.series?.key === OTHERS;
    const pointFilters = [filterFor(c.col, c.key), ...(c.series ? [filterFor(c.series.col, c.series.key)] : [])];
    const seriesLabel = c.series ? ' · ' + dimText(c.series.key, c.series.caption, c.series.col) : '';
    const items: MenuItem[] = [{ label: label + seriesLabel, disabled: true, styleClass: 'an-menu-head' }];
    if (this.crossFilterMode()) {
      items.push(
        { label: `Filter dashboard by this`, icon: 'pi pi-filter', disabled: isOthers, command: () => this.filterDashboard.emit(filterFor(c.col, c.key)) },
        { label: `Exclude from dashboard`, icon: 'pi pi-filter-slash', disabled: isOthers, command: () => this.filterDashboard.emit(excludeFor(c.col, c.key)) },
      );
    }
    const target = ds ? drillTarget(c.col, ds) : null;
    if (target) items.push({ label: `Drill down to ${target.label}`, icon: 'pi pi-arrow-down', disabled: isOthers, command: () => this.drill(c, target) });
    if (ds?.detail.length) items.push({ label: 'See records', icon: 'pi pi-list', disabled: isOthers, command: () => this.openRecords(pointFilters, label + seriesLabel) });
    this.pointItems.set(items);
    this.pointMenu().toggle(event);
  }

  private drill(c: Clicked, target: { field: string; bucket?: ReportSpec['dimensions'][0]['bucket']; label: string }) {
    const cur = this.current();
    const dims = cur.dimensions.map(d => d.field === c.col.field ? { field: target.field, bucket: target.bucket ?? null } : d);
    if (dims.filter(d => d.field === target.field && (d.bucket ?? null) === (target.bucket ?? null)).length > 1) return;
    const filters = [
      ...cur.filters.filter(f => !(c.col.type === 'date' && f.field === c.col.field)),
      filterFor(c.col, c.key),
    ];
    this.drillStack.update(st => [...st, { spec: { ...cur, dimensions: dims, filters }, label: dimText(c.key, c.caption, c.col) }]);
  }

  drillTo(depth: number) {
    this.drillStack.update(st => st.slice(0, depth));
  }

  private openRecords(extra: SpecFilter[], label: string) {
    const ds = this.ds();
    if (!ds) return;
    this.recordsLabel.set(label);
    this.records.set(recordsSpec(this.effective(), ds, extra));
  }

  openOrder(soId: unknown) {
    this.records.set(null);
    this.router.navigate(['/sales-orders'], { state: { soId: Number(soId), mode: 'view' } });
  }

  exportExcel() {
    const title = this.title() || this.ds()?.label || 'Report';
    this.api.export(title, [{ title, spec: this.effective() }]).subscribe({
      next: res => this.exportService.saveResponse(res, `${title}.xlsx`),
      error: err => this.error.set(err?.error?.message ?? 'Export failed.'),
    });
  }

  /** Chart image + table rows for the client-side PDF export. */
  pdfSection(): PdfSection {
    const image = this.chart() && this.effectiveType() !== 'table' && this.effectiveType() !== 'kpi'
      ? this.uiChart()?.getBase64Image() ?? null : null;
    if (this.effectiveType() === 'kpi') {
      return { title: this.title(), image: null, columns: ['Measure', 'Value'], rows: this.kpis().map(k => [k.label, k.value + (k.deltaText ? ` (${k.deltaText})` : '')]) };
    }
    const t = this.table();
    return {
      title: this.title() || this.ds()?.label || '',
      image,
      columns: t.cols.map(c => c.label),
      rows: [...t.rows.slice(0, 200).map(r => r.cells), ...(t.total ? [t.total] : [])],
    };
  }
}
