import {
  Bucket, ColumnMeta, DATE_PRESETS, DatasetMeta, FieldMeta, QueryResult, ReportSpec, SpecFilter
} from '../../core/models/analytics.model';

/**
 * Chart styling + formatting for Data Analytics. Categorical order is the
 * dataviz reference palette (validated: adjacent CVD ΔE ≥ 9.1, normal-vision
 * ≥ 19.6 on a light surface). Three slots are < 3:1 contrast, so every widget
 * offers tooltips + "Show as table" (relief rule). Single series = brand.
 */
export const BRAND = '#800000';
export const OTHERS_GRAY = '#9e9e9e';
export const PALETTE = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#4a3aa7', '#e34948'];
export const OTHERS = '(Others)';
const MAX_SERIES = 8;

const nf0 = new Intl.NumberFormat('en-PH', { maximumFractionDigits: 0 });
const nf2 = new Intl.NumberFormat('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const compact = new Intl.NumberFormat('en-PH', { notation: 'compact', maximumFractionDigits: 1 });
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

export function formatValue(v: unknown, format?: string | null, type?: string): string {
  if (v === null || v === undefined || v === '') return '—';
  if (typeof v === 'string' && isNaN(Number(v))) return v;
  const n = Number(v);
  switch (format) {
    case 'php': return '₱' + nf2.format(n);
    case 'pct': return nf2.format(n * 100).replace(/\.?0+$/, '') + '%';
    case 'dec': return nf2.format(n);
    case 'days': case 'daysSev': return nf0.format(n) + (n === 1 ? ' day' : ' days');
    case 'int': return nf0.format(n);
    default: return type === 'int' ? nf0.format(n) : nf2.format(n);
  }
}

export function formatAxis(v: number, format?: string | null): string {
  if (format === 'pct') return Math.round(v * 100) + '%';
  return (format === 'php' ? '₱' : '') + compact.format(v);
}

function parseIso(s: string): Date {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function formatDate(v: string, bucket?: Bucket | null): string {
  const d = parseIso(v);
  switch (bucket) {
    case 'year': return String(d.getFullYear());
    case 'quarter': return `${d.getFullYear()} Q${Math.floor(d.getMonth() / 3) + 1}`;
    case 'month': return `${MONTHS[d.getMonth()]} ${d.getFullYear()}`;
    case 'week': return `Wk of ${MONTHS[d.getMonth()]} ${d.getDate()}, ${d.getFullYear()}`;
    default: return `${MONTHS[d.getMonth()]} ${d.getDate()}, ${d.getFullYear()}`;
  }
}

/** Display text for a dimension cell: caption when present, else the formatted key. */
export function dimText(key: unknown, caption: unknown, col: ColumnMeta): string {
  if (key === OTHERS) return OTHERS;
  if (key === null || key === undefined || key === '') return col.type === 'date' ? '(invalid date)' : '(blank)';
  if (col.type === 'bool') return Number(key) === 1 || key === true ? 'Yes' : 'No';
  if (col.type === 'date') return formatDate(String(key), col.bucket);
  if (caption !== null && caption !== undefined && caption !== '') return String(caption);
  return String(key);
}

/** Resolved view of a result: which array index holds what. */
export interface Shape {
  d: { key: number; cap: number; col: ColumnMeta }[];
  m: { idx: number; col: ColumnMeta }[];
}

export function shapeOf(r: QueryResult): Shape {
  const idx = (k: string) => r.columns.findIndex(c => c.key === k);
  const d: Shape['d'] = [];
  for (let i = 0; idx(`d${i}`) >= 0; i++) d.push({ key: idx(`d${i}`), cap: idx(`d${i}c`), col: r.columns[idx(`d${i}`)] });
  const m = r.columns.map((col, i) => ({ idx: i, col })).filter(x => x.col.role === 'measure');
  return { d, m };
}

/** Stable entity → palette slot, so filtering never repaints surviving series. */
export class ColorMap {
  private slots = new Map<string, number>();
  color(entity: string): string {
    if (entity === OTHERS) return OTHERS_GRAY;
    if (!this.slots.has(entity)) this.slots.set(entity, this.slots.size);
    const slot = this.slots.get(entity)!;
    return slot < PALETTE.length ? PALETTE[slot] : OTHERS_GRAY;
  }
}

export interface ChartPoint { dim: number; key: unknown; caption: unknown; series?: { key: unknown; caption: unknown } }

export interface ChartModel {
  data: { labels: string[]; datasets: Record<string, unknown>[] };
  options: Record<string, unknown>;
  /** points[datasetIndex][index] → the dimension values behind a mark. */
  points: ChartPoint[][];
}

function withAlpha(hex: string, alpha: number): string {
  const n = parseInt(hex.slice(1), 16);
  return `rgba(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}, ${alpha})`;
}

/** Builds chart.js data/options from a query result. `selected` = keys highlighted by a cross-filter. */
export function buildChart(spec: ReportSpec, r: QueryResult, colors: ColorMap, selected: Set<string> | null): ChartModel {
  const s = shapeOf(r);
  const type = spec.type;
  const isPie = type === 'pie' || type === 'doughnut';
  const horizontal = !!spec.horizontal && type === 'bar';
  const fmt = s.m[0]?.col.format;
  const rows = [...r.rows, ...r.othersRows];
  const d0 = s.d[0];
  const points: ChartPoint[][] = [];
  const datasets: Record<string, unknown>[] = [];
  let labels: string[] = [];
  const dim = (base: string, key: unknown) =>
    selected && selected.size > 0 && key !== OTHERS && !selected.has(String(key)) ? withAlpha(base, 0.3) : base;

  if (s.d.length === 2 && !isPie) {
    // Axis = d0, legend = d1, single measure.
    const d1 = s.d[1];
    const m = s.m[0];
    const axisKeys: unknown[] = [];
    const axisCaps = new Map<string, unknown>();
    for (const row of (r.rowTotals?.length ? r.rowTotals : rows)) {
      if (!axisKeys.some(k => String(k) === String(row[d0.key]))) { axisKeys.push(row[d0.key]); axisCaps.set(String(row[d0.key]), d0.cap >= 0 ? row[d0.cap] : null); }
    }
    if (r.othersRows.length) axisKeys.push(OTHERS);
    let seriesKeys: unknown[] = (r.colTotals?.length ? r.colTotals : rows).map(x => x[d1.key])
      .filter((k, i, a) => a.findIndex(z => String(z) === String(k)) === i);
    const folded = seriesKeys.length > MAX_SERIES ? seriesKeys.slice(MAX_SERIES - 1).map(String) : [];
    if (folded.length) seriesKeys = [...seriesKeys.slice(0, MAX_SERIES - 1), OTHERS];
    labels = axisKeys.map(k => dimText(k, axisCaps.get(String(k)), d0.col));

    seriesKeys.forEach(sk => {
      const skStr = String(sk);
      const sample = rows.find(x => String(x[d1.key]) === skStr);
      const label = sk === OTHERS ? 'Other' : dimText(sk, sample && d1.cap >= 0 ? sample[d1.cap] : null, d1.col);
      const base = sk === OTHERS ? OTHERS_GRAY : colors.color(skStr);
      const data = axisKeys.map(ak => rows
        .filter(x => String(x[d0.key]) === String(ak) && (sk === OTHERS ? folded.includes(String(x[d1.key])) : String(x[d1.key]) === skStr))
        .reduce((sum, x) => sum + (Number(x[m.idx]) || 0), 0));
      datasets.push(seriesStyle(type, label, data, base, axisKeys.map(ak => dim(base, ak))));
      points.push(axisKeys.map(ak => ({ dim: 0, key: ak, caption: axisCaps.get(String(ak)), series: { key: sk, caption: sample && d1.cap >= 0 ? sample[d1.cap] : null } })));
    });
  } else {
    labels = rows.map(x => dimText(x[d0.key], d0.cap >= 0 ? x[d0.cap] : null, d0.col));
    const keys = rows.map(x => x[d0.key]);
    s.m.forEach((m, mi) => {
      const data = rows.map(x => x[m.idx] === null ? null : Number(x[m.idx]));
      let bg: string | string[];
      let base: string;
      if (isPie) {
        bg = keys.map(k => dim(colors.color(String(k)), k));
        base = BRAND;
      } else {
        base = s.m.length === 1 ? BRAND : PALETTE[mi % PALETTE.length];
        bg = keys.map(k => dim(k === OTHERS ? OTHERS_GRAY : base, k));
      }
      datasets.push(seriesStyle(type, m.col.label, data, base, bg));
      points.push(rows.map(x => ({ dim: 0, key: x[d0.key], caption: d0.cap >= 0 ? x[d0.cap] : null })));
    });
  }

  const legend = isPie || datasets.length > 1;
  const valueAxis = {
    beginAtZero: true,
    stacked: !!spec.stacked,
    grid: { color: '#eeeeee' },
    ticks: { color: '#666', callback: (v: number) => formatAxis(v, fmt) },
  };
  const catAxis = { stacked: !!spec.stacked, grid: { display: false }, ticks: { color: '#666', autoSkip: true, maxRotation: 0 } };
  const options: Record<string, unknown> = {
    responsive: true,
    maintainAspectRatio: false,
    animation: { duration: 250 },
    indexAxis: horizontal ? 'y' : 'x',
    interaction: type === 'line' ? { mode: 'index', intersect: false } : { mode: 'nearest', intersect: true },
    plugins: {
      legend: { display: legend, position: 'bottom', labels: { boxWidth: 10, color: '#444' } },
      tooltip: {
        callbacks: {
          label: (ctx: { dataset: { label?: string }; raw: unknown; chart: { data: { datasets: unknown[] } } }) =>
            `${formatValue(ctx.raw, fmt)}  ${ctx.chart.data.datasets.length > 1 || isPie ? ctx.dataset.label ?? '' : ''}`.trim(),
        },
      },
    },
    scales: isPie ? {} : horizontal ? { x: valueAxis, y: catAxis } : { x: catAxis, y: valueAxis },
  };
  if (isPie) {
    const plugins = options['plugins'] as Record<string, unknown>;
    plugins['tooltip'] = {
      callbacks: { label: (ctx: { label: string; raw: unknown }) => `${formatValue(ctx.raw, fmt)}  ${ctx.label}` },
    };
    // Plain data only: Chart.js treats functions inside options as scriptable and calls them itself.
    plugins['anSliceLabels'] = { format: fmt ?? null };
  }
  return { data: { labels, datasets }, options, points };
}

function seriesStyle(type: string, label: string, data: (number | null)[], base: string, bg: string | string[]): Record<string, unknown> {
  if (type === 'line') {
    return {
      label, data, borderColor: base, backgroundColor: withAlpha(base, 0.12), borderWidth: 2,
      pointRadius: data.length > 60 ? 0 : 3, pointHoverRadius: 5, pointHitRadius: 12, tension: 0.25, fill: false,
      pointBackgroundColor: bg,
    };
  }
  if (type === 'pie' || type === 'doughnut') {
    return { label, data, backgroundColor: bg, borderColor: '#ffffff', borderWidth: 2, hoverOffset: 4 };
  }
  return {
    label, data, backgroundColor: bg, borderRadius: 4, borderSkipped: 'start',
    borderColor: '#ffffff', borderWidth: { top: 0, right: 0, bottom: 0, left: 0 }, maxBarThickness: 48, categoryPercentage: 0.8, barPercentage: 0.9,
  };
}

// ── filters, drill, cross-filter ──────────────────────────────────────────

const FINER: Record<Bucket, Bucket | null> = { year: 'quarter', quarter: 'month', month: 'day', week: 'day', day: null };

function isoOf(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** [start, end] ISO dates covered by one bucket value. */
export function bucketRange(start: string, bucket: Bucket): [string, string] {
  const d = parseIso(start);
  const end = new Date(d);
  switch (bucket) {
    case 'week': end.setDate(end.getDate() + 6); break;
    case 'month': end.setMonth(end.getMonth() + 1, 0); break;
    case 'quarter': end.setMonth(end.getMonth() + 3, 0); break;
    case 'year': end.setFullYear(end.getFullYear(), 11, 31); break;
  }
  return [start, isoOf(end)];
}

/** Filter that selects one dimension value (handles null, date buckets, bools). */
export function filterFor(col: ColumnMeta, key: unknown): SpecFilter {
  const field = col.field!;
  if (key === null || key === undefined) return { field, op: 'isNull' };
  if (col.type === 'date') return { field, op: 'between', values: bucketRange(String(key), col.bucket ?? 'day') };
  if (col.type === 'bool') return { field, op: 'eq', values: [Number(key) === 1 ? 'true' : 'false'] };
  return { field, op: 'eq', values: [String(key)] };
}

export function excludeFor(col: ColumnMeta, key: unknown): SpecFilter {
  const field = col.field!;
  if (key === null || key === undefined) return { field, op: 'notNull' };
  if (col.type === 'bool') return { field, op: 'eq', values: [Number(key) === 1 ? 'false' : 'true'] };
  return { field, op: 'notIn', values: [String(key)] };
}

/** Next drill level for a dimension column: DrillTo field, or a finer date bucket. */
export function drillTarget(col: ColumnMeta, ds: DatasetMeta): { field: string; bucket?: Bucket; label: string } | null {
  if (col.type === 'date' && col.bucket) {
    const finer = FINER[col.bucket];
    return finer ? { field: col.field!, bucket: finer, label: finer[0].toUpperCase() + finer.slice(1) } : null;
  }
  const f = ds.fields.find(x => x.key === col.field);
  const to = f?.drillTo ? ds.fields.find(x => x.key === f.drillTo) : null;
  return to ? { field: to.key, bucket: to.kind === 'date' ? 'day' : undefined, label: to.label } : null;
}

/** The spec a widget actually runs: its own filters win over dashboard/cross filters on the same field. */
export function mergeFilters(spec: ReportSpec, ds: DatasetMeta | undefined, external: SpecFilter[]): ReportSpec {
  if (!external.length || !ds) return spec;
  const own = new Set(spec.filters.map(f => f.field));
  const has = (field: string) => ds.fields.some(f => f.key === field);
  const extra = external.filter(f => has(f.field) && !own.has(f.field) && !(f.op === 'in' && !(f.values?.length)));
  return extra.length ? { ...spec, filters: [...spec.filters, ...extra] } : spec;
}

/** Drill-through spec: the dataset's detail fields + current filters + the clicked filter. */
export function recordsSpec(spec: ReportSpec, ds: DatasetMeta, extraFilters: SpecFilter[]): ReportSpec {
  const byKey = (k: string) => ds.fields.find(f => f.key === k);
  const detail = ds.detail.map(byKey).filter((f): f is FieldMeta => !!f);
  const dims = detail.filter(f => f.kind !== 'measure').slice(0, 10).map(f => ({ field: f.key, bucket: f.kind === 'date' ? 'day' as Bucket : null }));
  let measures = detail.filter(f => f.kind === 'measure').map(f => ({ field: f.key }));
  if (!measures.length) {
    const count = ds.fields.find(f => f.kind === 'measure' && f.defaultAgg === 'custom') ?? ds.fields.find(f => f.kind === 'measure');
    if (count) measures = [{ field: count.key }];
  }
  return {
    v: 1, dataset: spec.dataset, type: 'table', dimensions: dims, measures,
    filters: [...spec.filters.filter(f => f.op !== 'any'), ...extraFilters],
  };
}

/** Short human text for a filter chip. */
export function filterText(f: SpecFilter, ds: DatasetMeta | undefined, captions?: Map<string, string>): string {
  const field = ds?.fields.find(x => x.key === f.field);
  const label = field?.label ?? f.field;
  const vals = (f.values ?? []).map(v => v === null ? '(blank)' : captions?.get(v) ?? (field?.kind === 'date' && v ? formatDate(v) : field?.type === 'bool' ? (v === 'true' ? 'Yes' : 'No') : v));
  switch (f.op) {
    case 'preset': return `${label}: ${DATE_PRESETS.find(p => p.value === f.values?.[0])?.label ?? vals[0]}`;
    case 'between': return `${label}: ${vals[0]} – ${vals[1]}`;
    case 'isNull': return `${label}: (blank)`;
    case 'notNull': return `${label}: not blank`;
    case 'notIn': case 'neq': return `${label} ≠ ${vals.join(', ')}`;
    case 'contains': return `${label} contains "${vals[0]}"`;
    case 'startsWith': return `${label} starts with "${vals[0]}"`;
    case 'gt': return `${label} > ${vals[0]}`;
    case 'gte': return `${label} ≥ ${vals[0]}`;
    case 'lt': return `${label} < ${vals[0]}`;
    case 'lte': return `${label} ≤ ${vals[0]}`;
    default: return `${label}: ${vals.join(', ')}`;
  }
}

export function newId(): string {
  return Math.random().toString(36).slice(2, 10);
}

/** Drops filters that are still being edited (missing values) so the preview never errors mid-edit. */
export function cleanSpec(spec: ReportSpec): ReportSpec {
  const ok = (f: SpecFilter) => {
    const v = f.values ?? [];
    switch (f.op) {
      case 'isNull': case 'notNull': case 'any': return true;
      case 'in': case 'notIn': return v.length > 0;
      case 'between': return v.length === 2 && v[0] != null && v[1] != null && v[0] !== '' && v[1] !== '';
      default: return v.length > 0 && v[0] != null && v[0] !== '';
    }
  };
  const filters = spec.filters.filter(ok);
  return filters.length === spec.filters.length ? spec : { ...spec, filters };
}

/** Undelivered-age severity (thresholds kept from the old Sales Analytics page): icon + label, never colour alone. */
export function daysSeverity(v: unknown): { cls: string; icon: string; label: string } | null {
  if (v === null || v === undefined) return null;
  const d = Number(v);
  if (d >= 8) return { cls: 'an-sev-critical', icon: 'pi-times-circle', label: 'Critical' };
  if (d >= 4) return { cls: 'an-sev-serious', icon: 'pi-exclamation-triangle', label: 'Serious' };
  return { cls: 'an-sev-warning', icon: 'pi-clock', label: 'Warning' };
}

// ── on-slice value labels (pie / doughnut) ────────────────────────────────

/** Relative luminance of a #rrggbb or rgba() colour. */
function luminance(color: string): number {
  let r = 0, g = 0, b = 0;
  const m = /rgba?\((\d+),\s*(\d+),\s*(\d+)/.exec(color);
  if (m) { r = +m[1]; g = +m[2]; b = +m[3]; }
  else if (color.startsWith('#')) { const n = parseInt(color.slice(1, 7), 16); r = (n >> 16) & 255; g = (n >> 8) & 255; b = n & 255; }
  const lin = (c: number) => { const v = c / 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); };
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
}

/** White or near-black text, whichever contrasts more with the slice. */
function labelInk(bg: string): string {
  const L = luminance(bg);
  return (1.05 / (L + 0.05)) >= ((L + 0.05) / 0.0637) ? '#ffffff' : '#1f1f1f';
}

const MIN_LABEL_SHARE = 0.04;

/**
 * Chart.js plugin: draws "value / share%" on each pie/doughnut slice.
 * Enabled per chart via options.plugins.anSliceLabels = { format }. Slices
 * under 4% are left to the tooltip so labels never collide.
 */
export const sliceLabelsPlugin = {
  id: 'anSliceLabels',
  afterDatasetsDraw(chart: any, _args: unknown, opts: { format?: string | null } | undefined) {
    const type = chart.config?.type;
    if (!opts || opts.format === undefined || (type !== 'pie' && type !== 'doughnut')) return;
    const ctx = chart.ctx as CanvasRenderingContext2D;
    chart.data.datasets.forEach((ds: any, di: number) => {
      const meta = chart.getDatasetMeta(di);
      if (meta.hidden) return;
      const values: number[] = ds.data.map((v: unknown) => Number(v) || 0);
      const total = values.reduce((sum, v, i) => sum + (chart.getDataVisibility(i) ? Math.max(v, 0) : 0), 0);
      if (total <= 0) return;
      meta.data.forEach((arc: any, i: number) => {
        const v = values[i];
        if (!chart.getDataVisibility(i) || v <= 0 || v / total < MIN_LABEL_SHARE) return;
        const { x, y } = arc.tooltipPosition();
        const bg = Array.isArray(ds.backgroundColor) ? ds.backgroundColor[i] : ds.backgroundColor;
        const pct = (v / total * 100).toFixed(1).replace(/\.0$/, '') + '%';
        ctx.save();
        ctx.fillStyle = labelInk(String(bg ?? '#800000'));
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.font = '600 12px Inter, -apple-system, "Segoe UI", sans-serif';
        ctx.fillText(formatValue(v, opts.format), x, y - 7);
        ctx.font = '11px Inter, -apple-system, "Segoe UI", sans-serif';
        ctx.fillText(pct, x, y + 8);
        ctx.restore();
      });
    });
  },
};
