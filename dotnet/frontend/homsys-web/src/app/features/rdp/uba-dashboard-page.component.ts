import { Component, computed, inject, OnDestroy, OnInit, signal, viewChild } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { ChartModule } from 'primeng/chart';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { TabBarService } from '../../core/services/tab-bar.service';
import { RdpService, UbaFilter, UbaFilters, UbaKpi, UbaMetric, UbaSplit, UbaSummary } from '../../core/services/rdp.service';
import { monthLabel, UbaMatrixComponent, UbaSort } from './uba-matrix.component';

const TAB_KEY = '/uba-dashboard';

interface SavedView {
  areas: string[]; rdms: string[]; distributorIds: number[]; from: string | null; to: string | null; quick: string | null;
  metric: UbaMetric; sort: UbaSort; minRange: number | null; find: string; groupBy: string[];
  channels?: string[]; monthPrev?: { from: string | null; to: string | null; quick: string | null } | null;
}

const BRAND = '#800000';
const METRIC_LABEL: Record<UbaMetric, string> = { accounts: 'Buying accounts', sales: 'Net sales', cases: 'Cases' };

/** "2026-09" shifted by n months. */
const addMonths = (p: string, n: number) => {
  const t = +p.slice(0, 4) * 12 + +p.slice(5, 7) - 1 + n;
  return `${Math.floor(t / 12)}-${String(t % 12 + 1).padStart(2, '0')}`;
};

/**
 * UBA (unique buying accounts) dashboard over the uploaded RDP sales files — the COO end-state of the
 * TMG × IT "UBA Tracker" deck: filters, scorecard, monthly trend + area split, distributor leaderboard,
 * channel split and a drill-down split explorer.
 */
@Component({
  selector: 'app-uba-dashboard-page',
  standalone: true,
  imports: [FormsModule, DecimalPipe, MultiSelectModule, SelectModule, ButtonModule, InputNumberModule, InputTextModule,
            ChartModule, MessageModule, UbaMatrixComponent],
  template: `
    <p class="intro">Distributor (RDP) sales files, tagged with the RDP list. An account counts as <b>buying</b> in a month when its net
      purchases that month (after returns, credits and discounts) are above zero; over a range it counts once.</p>

    @if (filters(); as f) {
      @if (!f.periods.length) {
        <p-message severity="info" styleClass="w-full" text="No RDP sales uploaded yet. Upload distributor files on the RDP Sales Upload page." />
      } @else {
        <div class="filters">
          <label>Area<p-multiSelect [options]="f.areas" [(ngModel)]="areas" placeholder="All areas" (onChange)="reload()" [showToggleAll]="false" /></label>
          <label>RDM<p-multiSelect [options]="f.rdms" [(ngModel)]="rdms" placeholder="All RDMs" (onChange)="reload()" [showToggleAll]="false" /></label>
          <label>Distributor<p-multiSelect [options]="distributorOptions()" [(ngModel)]="distributorIds" optionLabel="label" optionValue="id"
            placeholder="All distributors" [filter]="true" (onChange)="reload()" styleClass="dist" [maxSelectedLabels]="2" /></label>
          <label>Quick range<p-select [options]="quickRanges()" [(ngModel)]="quick" optionLabel="label" optionValue="value"
            placeholder="Custom" (onChange)="applyQuick()" /></label>
          <label>From<p-select [options]="periodOptions()" [(ngModel)]="from" optionLabel="label" optionValue="value" (onChange)="quick = null; monthPrev = null; reload()" /></label>
          <label>To<p-select [options]="periodOptions()" [(ngModel)]="to" optionLabel="label" optionValue="value" (onChange)="quick = null; monthPrev = null; reload()" /></label>
          <label>Show<p-select [options]="metrics" [(ngModel)]="metric" optionLabel="label" optionValue="value" (onChange)="reload()" /></label>
          <p-button label="Clear filters" icon="pi pi-filter-slash" severity="secondary" [outlined]="true"
            [disabled]="!filtered()" (onClick)="clearFilters()" />
        </div>
        <div class="chips">
          @for (c of chips(); track c.key) {
            <span class="chip">{{ c.label }}<button type="button" [attr.aria-label]="'Remove ' + c.label" (click)="c.remove()"><i class="pi pi-times"></i></button></span>
          } @empty {
            <span class="hint"><i class="pi pi-info-circle"></i> Click a month on the chart, an area, a distributor or a channel type to filter the whole dashboard.</span>
          }
        </div>

        @if (summary(); as s) {
          <div class="kpis">
            <div class="kpi"><span>Buying accounts (unique) · {{ rangeLabel() }}</span><b>{{ s.buyingAccounts.value | number:'1.0-0' }}</b>
              <small [class]="cls(s.buyingAccounts)">{{ change(s.buyingAccounts, s) }}</small></div>
            <div class="kpi"><span>Net sales · {{ rangeLabel() }}</span><b>{{ peso(s.netSales.value) }}</b>
              <small [class]="cls(s.netSales)">{{ change(s.netSales, s) }}</small></div>
            <div class="kpi"><span>Cases · {{ rangeLabel() }}</span><b>{{ s.cases.value | number:'1.0-0' }}</b>
              <small [class]="cls(s.cases)">{{ change(s.cases, s) }}</small></div>
            <div class="kpi"><span>Average buying accounts per month</span><b>{{ s.avgAccountsPerMonth | number:'1.0-0' }}</b></div>
            <div class="kpi"><span>Average lines (SKUs) per account</span><b>{{ s.avgLinesPerAccount | number:'1.1-1' }}</b></div>
            <div class="kpi"><span>SKUs sold</span><b>{{ s.skusSold | number }}</b></div>
          </div>

          <div class="two">
            <section class="card">
              <h3>{{ metricLabel() }} per month · {{ s.distributors }} distributor{{ s.distributors === 1 ? '' : 's' }}</h3>
              <p-chart type="line" [data]="chartData()" [options]="chartOptions" height="260px" (onDataSelect)="pickMonth($event)" />
            </section>
            <section class="card">
              <h3>{{ metricLabel() }} by area, {{ rangeLabel() }}</h3>
              <table class="mini">
                <thead><tr><th>Area</th><th class="n">{{ metricLabel() }}</th><th class="n">Share</th></tr></thead>
                <tbody>@for (a of s.byArea; track a.label) {
                  <tr class="pick" [class.sel]="areas.includes(a.keys[0])" (click)="toggleArea(a.keys[0])"><td>{{ a.label }}</td><td class="n">{{ a.range | number:'1.0-0' }}</td><td class="n">{{ a.share | number:'1.0-0' }}%</td></tr>
                }</tbody>
              </table>
            </section>
          </div>
        }

        <section class="card">
          <h3>Topline by distributor · {{ metricLabel() }}, {{ rangeLabel() }}</h3>
          <app-uba-matrix [split]="leaderboard()" title="Distributor" [owner]="true"
            [selectable]="true" [selected]="pickedDistributors()" (pick)="toggleDistributor($event.keys[0])" />
        </section>

        <section class="card">
          <h3>Channel type · {{ metricLabel() }}, {{ rangeLabel() }}</h3>
          <p class="sub">Each account is counted once, under its most common channel label. Files with no channel show under
            “No channel in source”; labels not yet mapped show as “Unmapped: …” (map them on the RDP Sales Upload page).</p>
          <app-uba-matrix [split]="channels()" title="Channel type"
            [selectable]="true" [selected]="channelPick" (pick)="toggleChannel($event.keys[0])" />
        </section>

        <section class="card">
          <h3>Split explorer</h3>
          <p class="sub">Choose how rows are grouped and click a row to open its next level. Lower-level counts can add up to more than
            their parent, because one account can buy from several categories.</p>
          <div class="filters">
            @for (i of [0, 1, 2]; track i) {
              <label>Level {{ i + 1 }}<p-select [options]="levelOptions(i)" [ngModel]="groupBy()[i]" optionLabel="label" optionValue="value"
                [showClear]="i > 0" placeholder="—" (onChange)="setLevel(i, $event.value)" [disabled]="i > 0 && !groupBy()[i - 1]" /></label>
            }
            <label>Sort rows<p-select [options]="sorts" [(ngModel)]="sort" optionLabel="label" optionValue="value" /></label>
            <label>Hide rows below<p-inputNumber [(ngModel)]="minRange" [min]="0" inputStyleClass="w6" /></label>
            <label>Find row<input pInputText [(ngModel)]="find" placeholder="distributor, category…" /></label>
            <p-button label="Expand all" severity="secondary" size="small" (onClick)="explorer.expandAll()" />
            <p-button label="Collapse all" severity="secondary" size="small" (onClick)="explorer.collapseAll()" />
            <p-button label="Copy table" icon="pi pi-copy" severity="secondary" size="small" (onClick)="copy()" />
          </div>
          <app-uba-matrix #explorer [split]="split()" [title]="groupTitle()" [owner]="groupBy()[0] === 'distributor'"
            [sort]="sort" [minRange]="minRange ?? 0" [search]="find" style="--uba-max-h: 70vh" />
        </section>
      }
    }
  `,
  styles: [`
    :host { display: block; }
    .intro, .sub { font-size: .8rem; color: var(--p-text-muted-color); margin: 0 0 .6rem; }
    .filters { display: flex; flex-wrap: wrap; gap: .6rem; align-items: flex-end; margin-bottom: .8rem; }
    .filters label { display: flex; flex-direction: column; gap: .2rem; font-size: .75rem; color: var(--p-text-muted-color); }
    :host ::ng-deep .dist { min-width: 260px; max-width: 360px; }
    :host ::ng-deep .w6 { width: 6rem; }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .6rem; margin-bottom: .8rem; }
    .kpi, .card { background: #fff; border: 1px solid #d6d9de; border-radius: 8px; box-shadow: 0 1px 3px rgba(16, 24, 40, .06); }
    .kpi { padding: .6rem .8rem; display: flex; flex-direction: column; gap: .2rem; border-top: 3px solid ${BRAND}; }
    .kpi span { font-size: .72rem; color: var(--p-text-muted-color); }
    .kpi b { font-size: 1.5rem; color: ${BRAND}; }
    .kpi small { font-size: .72rem; color: var(--p-text-muted-color); }
    .kpi small.up::first-letter { color: #0f7b4f; } .kpi small.dn::first-letter { color: #b42318; }
    .two { display: grid; grid-template-columns: 2fr 1fr; gap: .8rem; }
    @media (max-width: 1000px) { .two { grid-template-columns: 1fr; } }
    .card { padding: .7rem .9rem; margin-bottom: .8rem; min-width: 0; }
    h3 { margin: -.7rem -.9rem .6rem; padding: .55rem .9rem; font-size: .95rem; border-bottom: 1px solid #e4e7ec; }
    .mini { width: 100%; border-collapse: collapse; font-size: .82rem; }
    .mini th, .mini td { padding: .35rem .3rem; border-bottom: 1px solid var(--p-surface-border); text-align: left; }
    .mini th { color: var(--p-text-muted-color); font-weight: 600; }
    .n { text-align: right !important; font-variant-numeric: tabular-nums; }
    .chips { display: flex; flex-wrap: wrap; gap: .4rem; align-items: center; margin: -.3rem 0 .8rem; min-height: 1.6rem; }
    .chip { display: inline-flex; align-items: center; gap: .35rem; padding: .2rem .3rem .2rem .6rem; border-radius: 999px;
            background: #f7e9e9; border: 1px solid #e3c2c2; color: ${BRAND}; font-size: .78rem; font-weight: 600; }
    .chip button { border: 0; background: none; cursor: pointer; color: ${BRAND}; padding: 0 .2rem; line-height: 1; }
    .chip .pi { font-size: .65rem; }
    .hint { font-size: .78rem; color: var(--p-text-muted-color); }
    .mini tr.pick { cursor: pointer; }
    .mini tr.pick:hover td { background: rgba(128, 0, 0, .05); }
    .mini tr.sel td { background: rgba(128, 0, 0, .1); color: ${BRAND}; font-weight: 600; }
  `]
})
export class UbaDashboardPageComponent implements OnInit, OnDestroy {
  private api = inject(RdpService);
  private toolbar = inject(GlobalToolbarService);
  private tabBar = inject(TabBarService);
  private toast = inject(MessageService);

  protected readonly metrics = (Object.keys(METRIC_LABEL) as UbaMetric[]).map(value => ({ value, label: METRIC_LABEL[value] }));
  protected readonly sorts: { value: UbaSort; label: string }[] =
    [{ value: 'high', label: 'Highest first' }, { value: 'low', label: 'Lowest first' }, { value: 'name', label: 'A – Z' }];

  protected loading = signal(false);
  protected filters = signal<UbaFilters | null>(null);
  protected summary = signal<UbaSummary | null>(null);
  protected leaderboard = signal<UbaSplit | null>(null);
  protected channels = signal<UbaSplit | null>(null);
  protected split = signal<UbaSplit | null>(null);
  protected groupBy = signal<string[]>(['distributor', 'category', 'subcategory']);
  private explorerCmp = viewChild<UbaMatrixComponent>('explorer');

  protected areas: string[] = [];
  protected rdms: string[] = [];
  protected distributorIds: number[] = [];
  protected from: string | null = null;
  protected to: string | null = null;
  protected quick: string | null = 'ytd';
  protected metric: UbaMetric = 'accounts';
  protected sort: UbaSort = 'high';
  protected minRange: number | null = 0;
  protected find = '';
  /** Channel types picked in the channel table. */
  protected channelPick: string[] = [];
  /** The range before a month was clicked on the chart (clicking it again, or its chip, goes back). */
  protected monthPrev: { from: string | null; to: string | null; quick: string | null } | null = null;
  private shownMetric = signal<UbaMetric>('accounts');
  private shownRange = signal<[string, string] | null>(null);

  protected metricLabel = computed(() => METRIC_LABEL[this.shownMetric()]);
  protected rangeLabel = computed(() => {
    const r = this.shownRange();
    return r ? `${monthLabel(r[0])} – ${monthLabel(r[1])}` : '';
  });
  protected periodOptions = computed(() => (this.filters()?.periods ?? []).map(p => ({ value: p, label: monthLabel(p) })));
  protected distributorOptions = computed(() => (this.filters()?.distributors ?? [])
    .filter(d => d.lineCount > 0 || this.distributorIds.includes(d.id))
    .map(d => ({ id: d.id, label: `${d.name}${d.siteName ? ' – ' + d.siteName : ''}` })));
  protected quickRanges = computed(() => {
    const last = this.filters()?.periods.at(-1);
    if (!last) return [];
    const y = last.slice(0, 4);
    return [
      { value: 'ytd', label: `${y} year to date` },
      { value: 'm3', label: 'Last 3 months' }, { value: 'm6', label: 'Last 6 months' }, { value: 'm12', label: 'Last 12 months' },
      { value: 'py', label: `${+y - 1} full year` }, { value: 'all', label: 'Everything on file' },
    ];
  });
  protected groupTitle = computed(() => this.groupBy()
    .map(g => this.filters()?.dimensions.find(d => d.value === g)?.label ?? g).join(' › '));

  protected chartData = computed(() => {
    const t = this.summary()?.trend ?? [];
    return {
      labels: t.map(p => monthLabel(p.period)),
      datasets: [{
        label: this.metricLabel(), data: t.map(p => p.value), borderColor: BRAND, backgroundColor: BRAND, tension: 0.25,
        pointRadius: t.map(p => p.inRange ? 4 : 2), pointBackgroundColor: t.map(p => p.inRange ? BRAND : '#c9a3a3'),
        segment: { borderColor: (c: any) => t[c.p1DataIndex]?.inRange ? BRAND : '#c9a3a3' },
      }],
    };
  });
  protected readonly chartOptions = {
    maintainAspectRatio: false,
    onHover: (e: any, els: unknown[]) => { if (e.native?.target) e.native.target.style.cursor = els.length ? 'pointer' : 'default'; },
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true, ticks: { precision: 0 } }, x: { grid: { display: false } } },
  };

  ngOnInit() {
    this.restoreView();
    this.toolbar.set({ title: 'UBA Dashboard', refresh: { onClick: () => this.init(), loading: this.loading } });
    this.init();
  }

  ngOnDestroy() {
    this.saveView();
    this.toolbar.clear();
  }

  /** Switching to another app tab destroys this page; keep the filters and split levels on our app tab. */
  private saveView() {
    const view: SavedView = { areas: this.areas, rdms: this.rdms, distributorIds: this.distributorIds, from: this.from, to: this.to,
      quick: this.quick, metric: this.metric, sort: this.sort, minRange: this.minRange, find: this.find, groupBy: this.groupBy(),
      channels: this.channelPick, monthPrev: this.monthPrev };
    this.tabBar.updateTabState(TAB_KEY, { view });
  }

  private restoreView() {
    const v = this.tabBar.tabs().find(t => t.key === TAB_KEY)?.state?.['view'] as SavedView | undefined;
    if (!v) return;
    ({ areas: this.areas, rdms: this.rdms, distributorIds: this.distributorIds, from: this.from, to: this.to, quick: this.quick,
       metric: this.metric, sort: this.sort, minRange: this.minRange, find: this.find } = v);
    this.groupBy.set(v.groupBy);
    this.channelPick = v.channels ?? [];
    this.monthPrev = v.monthPrev ?? null;
  }

  private init() {
    this.api.filters().subscribe({
      next: r => {
        this.filters.set(r.data ?? null);
        if (r.data?.periods.length) {
          if (!this.from || !this.to) this.applyQuick();
          else this.reload();
        }
      },
      error: e => this.fail(e),
    });
  }

  protected applyQuick() {
    const periods = this.filters()?.periods ?? [];
    const last = periods.at(-1);
    if (!last || !this.quick) return;
    const first = periods[0];
    this.monthPrev = null;
    const clamp = (p: string) => p < first ? first : p;
    const y = last.slice(0, 4);
    [this.from, this.to] = ({
      ytd: [clamp(`${y}-01`), last], m3: [clamp(addMonths(last, -2)), last], m6: [clamp(addMonths(last, -5)), last],
      m12: [clamp(addMonths(last, -11)), last], py: [clamp(`${+y - 1}-01`), `${+y - 1}-12` < first ? last : `${+y - 1}-12`],
      all: [first, last],
    } as Record<string, [string, string]>)[this.quick];
    this.reload();
  }

  /** Anything picked besides the defaults (all areas / RDMs / distributors, year to date, buying accounts). */
  protected filtered() {
    return this.areas.length > 0 || this.rdms.length > 0 || this.distributorIds.length > 0 || this.channelPick.length > 0
      || this.quick !== 'ytd' || this.metric !== 'accounts';
  }

  protected clearFilters() {
    this.areas = [];
    this.rdms = [];
    this.distributorIds = [];
    this.channelPick = [];
    this.metric = 'accounts';
    this.quick = 'ytd';
    this.applyQuick();
  }

  // ── cross-filtering: a click on any chart / table filters the whole dashboard ──

  private static toggled<T>(list: T[], v: T) { return list.includes(v) ? list.filter(x => x !== v) : [...list, v]; }

  protected toggleArea(area: string) { this.areas = UbaDashboardPageComponent.toggled(this.areas, area); this.reload(); }

  protected toggleDistributor(key: string) {
    this.distributorIds = UbaDashboardPageComponent.toggled(this.distributorIds, +key);
    this.reload();
  }

  protected toggleChannel(channel: string) { this.channelPick = UbaDashboardPageComponent.toggled(this.channelPick, channel); this.reload(); }

  protected pickedDistributors() { return this.distributorIds.map(String); }

  /** A month clicked on the trend: the dashboard narrows to it; clicking it again goes back to the range before. */
  protected pickMonth(e: any) {
    const p = this.summary()?.trend[e?.element?.index]?.period;
    if (!p) return;
    if (this.from === p && this.to === p && this.monthPrev) return this.undoMonth();
    this.monthPrev ??= { from: this.from, to: this.to, quick: this.quick };
    this.from = this.to = p;
    this.quick = null;
    this.reload();
  }

  private undoMonth() {
    if (!this.monthPrev) return;
    ({ from: this.from, to: this.to, quick: this.quick } = this.monthPrev);
    this.monthPrev = null;
    this.reload();
  }

  /** What's filtered, as removable chips. */
  protected chips() {
    const out: { key: string; label: string; remove: () => void }[] = [];
    if (this.monthPrev && this.from) out.push({ key: 'm', label: `Month: ${monthLabel(this.from)}`, remove: () => this.undoMonth() });
    for (const a of this.areas) out.push({ key: 'a' + a, label: `Area: ${a || '(not tagged)'}`, remove: () => this.toggleArea(a) });
    for (const r of this.rdms) out.push({ key: 'r' + r, label: `RDM: ${r}`, remove: () => { this.rdms = this.rdms.filter(x => x !== r); this.reload(); } });
    const names = new Map(this.distributorOptions().map(d => [d.id, d.label]));
    for (const d of this.distributorIds) out.push({ key: 'd' + d, label: names.get(d) ?? `Distributor ${d}`, remove: () => this.toggleDistributor(String(d)) });
    for (const c of this.channelPick) out.push({ key: 'c' + c, label: `Channel: ${c}`, remove: () => this.toggleChannel(c) });
    return out;
  }

  private filter(): UbaFilter {
    return { from: this.from, to: this.to, areas: this.areas, rdms: this.rdms, distributorIds: this.distributorIds, metric: this.metric,
             channels: this.channelPick };
  }

  protected reload() {
    const f = this.filter();
    this.loading.set(true);
    let pending = 3;
    const done = () => { if (--pending === 0) { this.loading.set(false); this.shownMetric.set(f.metric); } };
    this.api.summary(f).subscribe({
      next: r => { this.summary.set(r.data ?? null); if (r.data) this.shownRange.set([r.data.from, r.data.to]); done(); },
      error: e => { done(); this.fail(e); },
    });
    this.api.split(f, ['distributor']).subscribe({ next: r => { this.leaderboard.set(r.data ?? null); done(); }, error: e => { done(); this.fail(e); } });
    this.api.split(f, ['channel']).subscribe({ next: r => { this.channels.set(r.data ?? null); done(); }, error: e => { done(); this.fail(e); } });
    this.loadSplit();
  }

  private loadSplit() {
    this.api.split(this.filter(), this.groupBy()).subscribe({ next: r => this.split.set(r.data ?? null), error: e => this.fail(e) });
  }

  protected levelOptions(i: number) {
    const used = this.groupBy().slice(0, i);
    return (this.filters()?.dimensions ?? []).filter(d => !used.includes(d.value));
  }

  protected setLevel(i: number, value: string | null) {
    const next = this.groupBy().slice(0, i);
    if (value) next.push(value, ...this.groupBy().slice(i + 1).filter(g => g !== value && !next.includes(g)));
    this.groupBy.set(next.length ? next : ['distributor']);
    this.loadSplit();
  }

  protected copy() {
    const text = this.explorerCmp()?.asText() ?? '';
    navigator.clipboard.writeText(text).then(
      () => this.toast.add({ severity: 'success', summary: 'Copied', detail: 'Paste it into Excel.' }),
      () => this.toast.add({ severity: 'error', summary: 'Copy failed', detail: 'The browser blocked clipboard access.' }));
  }

  protected peso(v: number) {
    const a = Math.abs(v);
    const s = a >= 1e9 ? (a / 1e9).toFixed(2) + 'B' : a >= 1e6 ? (a / 1e6).toFixed(2) + 'M' : a >= 1e3 ? (a / 1e3).toFixed(1) + 'K' : a.toFixed(0);
    return `${v < 0 ? '-' : ''}₱${s}`;
  }

  protected cls(k: UbaKpi) { return k.changePct === null ? '' : k.changePct > 0 ? 'up' : k.changePct < 0 ? 'dn' : ''; }

  protected change(k: UbaKpi, s: UbaSummary) {
    if (k.changePct === null) return 'No prior-period data to compare';
    const arrow = k.changePct > 0 ? '▲' : k.changePct < 0 ? '▼' : '•';
    return `${arrow} ${Math.abs(k.changePct).toFixed(1)}% vs prior period (like-for-like: ${s.likeForLikeDistributors} of ${s.distributors} distributors)`;
  }

  private fail(err: any) {
    this.toast.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Please try again.' });
  }
}
