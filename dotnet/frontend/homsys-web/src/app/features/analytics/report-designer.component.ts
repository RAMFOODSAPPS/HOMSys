import { Component, computed, input, model, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { DragDropModule } from 'primeng/dragdrop';
import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import {
  AnalyticsMeta, Bucket, FieldMeta, ReportSpec, SpecFilter, VISUALS, Visual
} from '../../core/models/analytics.model';
import { FilterEditorComponent, defaultFilter } from './filter-editor.component';

type Well = 'dims' | 'measures' | 'filters';

const AGG_LABELS: Record<string, string> = {
  sum: 'Sum', avg: 'Average', min: 'Min', max: 'Max', count: 'Count', countDistinct: 'Distinct count', custom: 'Calculated',
};
const BUCKETS: { value: Bucket; label: string }[] = [
  { value: 'day', label: 'Day' }, { value: 'week', label: 'Week' }, { value: 'month', label: 'Month' },
  { value: 'quarter', label: 'Quarter' }, { value: 'year', label: 'Year' },
];

/**
 * Field-well report designer. Every drag has a click equivalent: the "+"
 * next to a field sends it to its natural well (measure → Values, date →
 * Rows by month, dimension → Rows). Choosing a visual reshapes the wells.
 */
@Component({
  selector: 'app-report-designer',
  standalone: true,
  imports: [CommonModule, FormsModule, SelectModule, CheckboxModule, ButtonModule, TooltipModule, DragDropModule,
    CdkDropList, CdkDrag, CdkDragHandle, FilterEditorComponent],
  template: `
    <div class="an-designer">
      <div class="an-section">
        <label class="an-section-label">Dataset</label>
        <p-select [options]="meta().datasets" optionLabel="label" optionValue="key" [ngModel]="spec().dataset"
                  (ngModelChange)="setDataset($event)" placeholder="Choose data…" styleClass="w-full" size="small" appendTo="body" />
        @if (ds(); as d) { <div class="an-hint">{{ d.grain }}</div> }
      </div>

      @if (ds(); as d) {
        <div class="an-section">
          <label class="an-section-label">Visual</label>
          <div class="an-visuals">
            @for (v of visuals; track v.value) {
              <button type="button" class="an-visual" [class.active]="spec().type === v.value" (click)="setType(v.value)" [pTooltip]="v.label" tooltipPosition="top">
                <i [class]="v.icon"></i>
              </button>
            }
          </div>
          @if (suggestion(); as s) {
            <button type="button" class="an-suggest" (click)="setType(s.value)"><i class="pi pi-lightbulb"></i> Try {{ s.label }}</button>
          }
        </div>

        <div class="an-panes">
          <div class="an-fields">
            <input class="p-inputtext p-inputtext-sm w-full" placeholder="Search fields…" [ngModel]="search()" (ngModelChange)="search.set($event)" />
            @for (g of groups(); track g.label) {
              @if (g.fields.length) {
                <div class="an-group">{{ g.label }}</div>
                @for (f of g.fields; track f.key) {
                  <div class="an-field" pDraggable="an-field" (onDragStart)="dragged = f" (onDragEnd)="dragged = null" [pTooltip]="f.description ?? ''" tooltipPosition="right">
                    <i class="pi" [class.pi-hashtag]="f.kind === 'measure'" [class.pi-calendar]="f.kind === 'date'" [class.pi-tag]="f.kind === 'dim'"></i>
                    <span class="an-field-label">{{ f.label }}</span>
                    <button type="button" class="an-icon-btn" (click)="add(f)" pTooltip="Add" tooltipPosition="left"><i class="pi pi-plus"></i></button>
                    <button type="button" class="an-icon-btn" (click)="add(f, 'filters')" pTooltip="Filter by this" tooltipPosition="left"><i class="pi pi-filter"></i></button>
                  </div>
                }
              }
            }
          </div>

          <div class="an-wells">
            @if (spec().type !== 'kpi') {
              <div class="an-well" pDroppable="an-field" (onDrop)="drop('dims')">
                <div class="an-well-label">{{ wellLabels().dims }}</div>
                <div class="an-chip-list" cdkDropList cdkDropListOrientation="mixed" (cdkDropListDropped)="reorder('dims', $event)">
                @for (dim of spec().dimensions; track dim.field + (dim.bucket ?? ''); let i = $index) {
                  <div class="an-chip" cdkDrag>
                    <i class="pi pi-bars an-handle" cdkDragHandle pTooltip="Drag to reorder" tooltipPosition="top"></i>
                    <span class="an-chip-tag">{{ dimRole(i) }}</span>
                    <span class="an-chip-label">{{ fieldOf(dim.field)?.label ?? dim.field }}</span>
                    @if (fieldOf(dim.field)?.kind === 'date') {
                      <p-select [options]="buckets" optionLabel="label" optionValue="value" [ngModel]="dim.bucket ?? 'day'"
                                (ngModelChange)="setBucket(i, $event)" size="small" appendTo="body" styleClass="an-chip-select" />
                    }
                    <button type="button" class="an-icon-btn" (click)="removeDim(i)"><i class="pi pi-times"></i></button>
                  </div>
                }
                </div>
                @if (spec().dimensions.length === 2) {
                  <button type="button" class="an-link-btn" (click)="swapDims()"><i class="pi pi-arrow-right-arrow-left"></i> Swap</button>
                }
                @if (!spec().dimensions.length) { <div class="an-well-empty">Drop or + a field</div> }
              </div>
            }

            <div class="an-well" pDroppable="an-field" (onDrop)="drop('measures')">
              <div class="an-well-label">Values</div>
              <div class="an-chip-list an-chip-list-col" cdkDropList (cdkDropListDropped)="reorder('measures', $event)">
              @for (m of spec().measures; track $index; let i = $index) {
                <div class="an-chip an-chip-col" cdkDrag>
                  <div class="an-chip-row">
                    <i class="pi pi-bars an-handle" cdkDragHandle pTooltip="Drag to reorder" tooltipPosition="top"></i>
                    <span class="an-chip-label">{{ fieldOf(m.field)?.label ?? m.field }}</span>
                    @if ((fieldOf(m.field)?.aggs?.length ?? 0) > 1) {
                      <p-select [options]="aggOptions(m.field)" optionLabel="label" optionValue="value" [ngModel]="m.agg ?? fieldOf(m.field)?.defaultAgg"
                                (ngModelChange)="setAgg(i, $event)" size="small" appendTo="body" styleClass="an-chip-select" />
                    }
                    <button type="button" class="an-icon-btn" (click)="removeMeasure(i)"><i class="pi pi-times"></i></button>
                  </div>
                  <input class="p-inputtext p-inputtext-sm an-rename" placeholder="Rename (optional)" [ngModel]="m.label ?? ''" (ngModelChange)="rename(i, $event)" />
                </div>
              }
              </div>
              @if (!spec().measures.length) { <div class="an-well-empty">Drop or + a measure</div> }
              @if (mixedUnits()) { <div class="an-hint an-warn"><i class="pi pi-info-circle"></i> Values use different units — consider a second visual.</div> }
            </div>

            <div class="an-well" pDroppable="an-field" (onDrop)="drop('filters')">
              <div class="an-well-label">Filters</div>
              @for (f of spec().filters; track $index; let i = $index) {
                <app-filter-editor [dataset]="d" [filter]="f" (filterChange)="setFilter(i, $event)" (remove)="removeFilter(i)" />
              }
              @if (!spec().filters.length) { <div class="an-well-empty">Drop a field or use its <i class="pi pi-filter"></i></div> }
            </div>

            @if (spec().type !== 'kpi' && spec().dimensions.length) {
              <div class="an-well">
                <div class="an-well-label">Options</div>
                <div class="an-opt">
                  <span>Sort</span>
                  <p-select [options]="sortOptions()" optionLabel="label" optionValue="value" [ngModel]="sortValue()"
                            (ngModelChange)="setSort($event)" size="small" appendTo="body" styleClass="an-val" />
                </div>
                <div class="an-opt">
                  <span>Top N</span>
                  <input class="p-inputtext p-inputtext-sm an-num" type="number" min="1" max="1000" placeholder="All"
                         [ngModel]="spec().limit" (ngModelChange)="patch({ limit: $event ? +$event : null })" />
                  <p-checkbox [binary]="true" inputId="an-others" [ngModel]="!!spec().others" (ngModelChange)="patch({ others: $event })" [disabled]="!spec().limit" />
                  <label for="an-others">Show "Others"</label>
                </div>
                @if (firstIsDate()) {
                  <div class="an-opt">
                    <p-checkbox [binary]="true" inputId="an-fill" [ngModel]="!!spec().fill" (ngModelChange)="patch({ fill: $event })" />
                    <label for="an-fill">Fill empty periods with zero</label>
                  </div>
                }
                @if (spec().type === 'bar' || spec().type === 'line') {
                  <div class="an-opt">
                    <p-checkbox [binary]="true" inputId="an-stacked" [ngModel]="!!spec().stacked" (ngModelChange)="patch({ stacked: $event })" />
                    <label for="an-stacked">Stacked</label>
                    @if (spec().type === 'bar') {
                      <p-checkbox [binary]="true" inputId="an-horiz" [ngModel]="!!spec().horizontal" (ngModelChange)="patch({ horizontal: $event })" />
                      <label for="an-horiz">Horizontal</label>
                    }
                  </div>
                }
              </div>
            }
            @if (spec().type === 'kpi') {
              <div class="an-well">
                <div class="an-well-label">Options</div>
                <div class="an-opt">
                  <p-checkbox [binary]="true" inputId="an-compare" [ngModel]="spec().compare === 'previousPeriod'"
                              (ngModelChange)="patch({ compare: $event ? 'previousPeriod' : null })" />
                  <label for="an-compare">Compare with previous period</label>
                </div>
                @if (spec().compare && !hasDateFilter()) { <div class="an-hint an-warn">Add a date filter (period or range) to compare.</div> }
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class ReportDesignerComponent {
  readonly spec = model.required<ReportSpec>();
  readonly meta = input.required<AnalyticsMeta>();

  readonly visuals = VISUALS;
  readonly buckets = BUCKETS;
  readonly search = signal('');
  dragged: FieldMeta | null = null;

  readonly ds = computed(() => this.meta().datasets.find(d => d.key === this.spec().dataset));

  readonly groups = computed(() => {
    const q = this.search().trim().toLowerCase();
    const fields = (this.ds()?.fields ?? []).filter(f => !q || f.label.toLowerCase().includes(q));
    return [
      { label: 'Measures', fields: fields.filter(f => f.kind === 'measure') },
      { label: 'Dates', fields: fields.filter(f => f.kind === 'date') },
      { label: 'Dimensions', fields: fields.filter(f => f.kind === 'dim') },
    ];
  });

  readonly wellLabels = computed(() => {
    const t = this.spec().type;
    return { dims: t === 'bar' || t === 'line' ? 'Axis / Legend' : t === 'pivot' ? 'Rows / Columns' : t === 'pie' || t === 'doughnut' ? 'Slices' : 'Rows' };
  });

  dimRole(i: number): string {
    const t = this.spec().type;
    if (t === 'bar' || t === 'line') return i === 0 ? 'Axis' : 'Legend';
    if (t === 'pivot') return i === 0 ? 'Rows' : 'Columns';
    return String(i + 1);
  }

  readonly suggestion = computed(() => {
    const s = this.spec();
    if (!s.measures.length) return null;
    const first = s.dimensions[0] && this.fieldOf(s.dimensions[0].field);
    let v: Visual | null = null;
    if (!s.dimensions.length && s.type !== 'kpi') v = 'kpi';
    else if (first?.kind === 'date' && s.type !== 'line' && s.dimensions.length <= 2) v = 'line';
    else if (first && first.kind !== 'date' && s.type === 'table' && s.dimensions.length === 1) v = 'bar';
    return v ? VISUALS.find(x => x.value === v) ?? null : null;
  });

  readonly mixedUnits = computed(() => {
    const s = this.spec();
    if (!['bar', 'line'].includes(s.type) || s.measures.length < 2) return false;
    const fmts = new Set(s.measures.map(m => this.measureFormat(m.field, m.agg)));
    return fmts.size > 1;
  });

  readonly firstIsDate = computed(() => this.fieldOf(this.spec().dimensions[0]?.field)?.kind === 'date' && this.spec().dimensions.length === 1);
  readonly hasDateFilter = computed(() => this.spec().filters.some(f => this.fieldOf(f.field)?.kind === 'date' && (f.op === 'preset' || f.op === 'between')));

  readonly sortOptions = computed(() => {
    const s = this.spec();
    const opts: { label: string; value: string | null }[] = [{ label: 'Default', value: null }];
    s.dimensions.forEach((d, i) => {
      const l = this.fieldOf(d.field)?.label ?? d.field;
      opts.push({ label: `${l} ↑`, value: `d${i}|asc` }, { label: `${l} ↓`, value: `d${i}|desc` });
    });
    s.measures.forEach((m, i) => {
      const l = m.label || (this.fieldOf(m.field)?.label ?? m.field);
      opts.push({ label: `${l} ↓`, value: `m${i}|desc` }, { label: `${l} ↑`, value: `m${i}|asc` });
    });
    return opts;
  });

  readonly sortValue = computed(() => this.spec().sort ? `${this.spec().sort!.by}|${this.spec().sort!.dir}` : null);

  fieldOf(key: string | undefined): FieldMeta | undefined {
    return key ? this.ds()?.fields.find(f => f.key === key) : undefined;
  }

  aggOptions(key: string) {
    return (this.fieldOf(key)?.aggs ?? []).map(a => ({ value: a, label: AGG_LABELS[a] ?? a }));
  }

  private measureFormat(key: string, agg?: string | null): string {
    const f = this.fieldOf(key);
    const a = agg ?? f?.defaultAgg;
    return a === 'count' || a === 'countDistinct' ? 'int' : f?.format ?? 'dec';
  }

  patch(p: Partial<ReportSpec>) {
    this.spec.update(s => ({ ...s, ...p }));
  }

  setDataset(key: string) {
    const ds = this.meta().datasets.find(d => d.key === key);
    this.spec.set({ v: 1, dataset: key, type: 'table', dimensions: [], measures: [], filters: [...(ds?.defaults ?? [])] });
  }

  /** Reshape wells to fit the chosen visual. */
  setType(type: Visual) {
    this.spec.update(s => {
      let dims = s.dimensions, measures = s.measures;
      if (type === 'kpi') dims = [];
      if (type === 'pie' || type === 'doughnut') { dims = dims.slice(0, 1); measures = measures.slice(0, 1); }
      if (type === 'bar' || type === 'line') { dims = dims.slice(0, 2); if (dims.length === 2) measures = measures.slice(0, 1); }
      if (type === 'pivot') dims = dims.slice(0, 2);
      return { ...s, type, dimensions: dims, measures, sort: null };
    });
  }

  add(f: FieldMeta, well?: Well) {
    const target: Well = well ?? (f.kind === 'measure' ? 'measures' : 'dims');
    if (target === 'filters') {
      const ds = this.ds();
      if (ds) this.spec.update(s => ({ ...s, filters: [...s.filters, defaultFilter(ds, f.key)] }));
      return;
    }
    if (target === 'measures') {
      if (f.kind !== 'measure' && !f.aggs.includes('countDistinct') && !f.aggs.includes('count')) return;
      this.spec.update(s => ({ ...s, measures: [...s.measures, { field: f.key, agg: f.kind === 'measure' ? null : (f.aggs.includes('countDistinct') ? 'countDistinct' : 'count') }] }));
      return;
    }
    if (f.kind === 'measure') { this.add(f, 'measures'); return; }
    this.spec.update(s => {
      if (s.dimensions.some(d => d.field === f.key && f.kind !== 'date')) return s;
      const dims = [...s.dimensions, { field: f.key, bucket: f.kind === 'date' ? 'month' as Bucket : null }];
      let type = s.type;
      if (type === 'kpi') type = f.kind === 'date' ? 'line' : 'bar';
      if ((type === 'pie' || type === 'doughnut') && dims.length > 1) type = 'table';
      if ((type === 'bar' || type === 'line') && dims.length > 2) type = 'table';
      const measures = (type === 'bar' || type === 'line') && dims.length === 2 ? s.measures.slice(0, 1) : s.measures;
      return { ...s, type, dimensions: dims, measures, sort: null };
    });
  }

  drop(well: Well) {
    if (this.dragged) this.add(this.dragged, well);
    this.dragged = null;
  }

  removeDim(i: number) { this.spec.update(s => ({ ...s, dimensions: s.dimensions.filter((_, j) => j !== i), sort: null })); }
  removeMeasure(i: number) { this.spec.update(s => ({ ...s, measures: s.measures.filter((_, j) => j !== i), sort: null })); }
  removeFilter(i: number) { this.spec.update(s => ({ ...s, filters: s.filters.filter((_, j) => j !== i) })); }
  setFilter(i: number, f: SpecFilter) { this.spec.update(s => ({ ...s, filters: s.filters.map((x, j) => j === i ? f : x) })); }
  setBucket(i: number, bucket: Bucket) { this.spec.update(s => ({ ...s, dimensions: s.dimensions.map((d, j) => j === i ? { ...d, bucket } : d) })); }
  setAgg(i: number, agg: string) { this.spec.update(s => ({ ...s, measures: s.measures.map((m, j) => j === i ? { ...m, agg } : m) })); }
  rename(i: number, label: string) { this.spec.update(s => ({ ...s, measures: s.measures.map((m, j) => j === i ? { ...m, label: label || null } : m) })); }
  /** Drag-reorder within a well. Order matters: dims = column/axis order, measures = value order. */
  reorder(well: 'dims' | 'measures', e: CdkDragDrop<unknown>) {
    if (e.previousIndex === e.currentIndex) return;
    this.spec.update(s => {
      const list = well === 'dims' ? [...s.dimensions] : [...s.measures];
      moveItemInArray(list, e.previousIndex, e.currentIndex);
      // Sort keys are positional (d0/m1…), so a reorder resets them.
      return well === 'dims' ? { ...s, dimensions: list as typeof s.dimensions, sort: null } : { ...s, measures: list as typeof s.measures, sort: null };
    });
  }

  swapDims() { this.spec.update(s => ({ ...s, dimensions: [s.dimensions[1], s.dimensions[0]], sort: null })); }

  setSort(v: string | null) {
    if (!v) { this.patch({ sort: null }); return; }
    const [by, dir] = v.split('|');
    this.patch({ sort: { by, dir: dir as 'asc' | 'desc' } });
  }
}
