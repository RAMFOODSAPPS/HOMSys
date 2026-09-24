import { Component, OnDestroy, OnInit, computed, inject, input, signal, viewChildren } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { Subscription, filter } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { DrawerModule } from 'primeng/drawer';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { DragDropModule } from 'primeng/dragdrop';
import { MessageService } from 'primeng/api';
import { AnalyticsService } from '../../core/services/analytics.service';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { TabBarService } from '../../core/services/tab-bar.service';
import { ExportService } from '../../core/services/export.service';
import {
  DashboardDef, DashboardWidget, DatasetMeta, ReportSpec, SavedItem, SpecFilter, emptySpec
} from '../../core/models/analytics.model';
import { AnalyticsWidgetComponent, CrossFilterEvent } from './analytics-widget.component';
import { ReportDesignerComponent } from './report-designer.component';
import { FilterEditorComponent, defaultFilter } from './filter-editor.component';
import { ItemPropsDialogComponent, ItemProps } from './item-props-dialog.component';
import { dimText, filterFor, filterText, mergeFilters, newId } from './analytics-viz';
import { readHomeId } from './analytics-library-page.component';

const ROUTE = '/analytics/dashboard';
const ROW_PX = 80;

interface CrossFilter { sourceId: string; field: string; col: CrossFilterEvent['col']; keys: unknown[]; captions: string[] }

function emptyDef(): DashboardDef {
  return { v: 1, filters: [{ field: 'orderDate', op: 'preset', values: ['mtd'] }], widgets: [] };
}

@Component({
  selector: 'app-analytics-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonModule, SelectModule, DrawerModule, ToastModule, TooltipModule,
    DragDropModule, AnalyticsWidgetComponent, ReportDesignerComponent, FilterEditorComponent, ItemPropsDialogComponent],
  providers: [MessageService],
  template: `
    <p-toast position="top-right" />
    @if (meta(); as m) {
      <div class="an-dash">
        @if (homeMode()) {
          <div class="an-dash-title">
            <span>{{ item()?.name ?? 'Dashboard' }}</span>
            @if (item()) { <a class="an-link an-small" [routerLink]="['/analytics/dashboard']" [state]="{ dashboardId: item()!.id }">Open in Data Analytics <i class="pi pi-external-link"></i></a> }
          </div>
        } @else {
          <div class="an-report-actions">
            <span class="an-side-title">{{ item()?.name ?? 'New dashboard' }}</span>
            @if (item() && !item()!.canEdit) { <span class="an-badge"><i class="pi pi-eye"></i> View only</span> }
            @if (item()?.isSystem) { <span class="an-badge"><i class="pi pi-verified"></i> System template</span> }
            <span class="an-spacer"></span>
            @if (canEdit()) {
              <p-button [label]="editMode() ? 'Done editing' : 'Edit layout'" [icon]="editMode() ? 'pi pi-check' : 'pi pi-pencil'" size="small"
                        [outlined]="!editMode()" (onClick)="editMode.set(!editMode())" />
            }
            <p-button label="Save As" icon="pi pi-copy" size="small" [text]="true" (onClick)="propsMode.set('saveAs'); propsOpen.set(true)" />
            @if (item()?.canEdit) { <p-button label="Share" icon="pi pi-share-alt" size="small" [text]="true" (onClick)="propsMode.set('share'); propsOpen.set(true)" /> }
          </div>
        }

        <!-- Filter bar: date first, branch locked for scoped users, then slicers -->
        <div class="an-filterbar">
          @if (m.scope) { <span class="an-chip an-chip-lock"><i class="pi pi-lock"></i> Branch: {{ m.scope }}</span> }
          @for (f of def().filters; track $index; let i = $index) {
            @if (datasetFor(f.field); as fd) { @if (!(m.scope && f.field === 'branch')) {
              <app-filter-editor [dataset]="fd" [filter]="f" (filterChange)="setFilter(i, $event)" (remove)="removeFilter(i)" [removable]="!homeMode()" />
            } }
          }
          @if (!homeMode() && filterFields().length) {
            <p-select [options]="filterFields()" optionLabel="label" optionValue="key" [ngModel]="null" (ngModelChange)="addFilter($event)"
                      placeholder="+ Filter" [filter]="true" size="small" appendTo="body" styleClass="an-add-filter" />
          }
        </div>
        @if (crossFilters().length) {
          <div class="an-crossbar">
            <span class="an-muted">Filtered by:</span>
            @for (c of crossFilters(); track c.sourceId + c.field) {
              <span class="an-chip">{{ c.col.label }} = {{ c.captions.join(', ') }}
                <button type="button" class="an-icon-btn" (click)="clearCross(c)"><i class="pi pi-times"></i></button>
              </span>
            }
            <button type="button" class="an-link-btn" (click)="crossFilters.set([])">Clear all</button>
          </div>
        }

        @if (!def().widgets.length) {
          <div class="an-empty an-start">
            <i class="pi pi-th-large"></i>
            @if (canEdit() && !homeMode()) {
              <div>This dashboard is empty.</div>
              <p-button label="Add a widget" icon="pi pi-plus" size="small" (onClick)="editMode.set(true); openDesigner(null)" />
            } @else { <div>No widgets yet.</div> }
          </div>
        }

        <div class="an-grid">
          @for (w of def().widgets; track w.id; let i = $index) {
            <div class="an-cell" [style.grid-column]="'span ' + w.w" [style.height.px]="w.h * rowPx"
                 pDroppable="an-widget" (onDrop)="dropOn(i)" [class.an-editing]="editMode()">
              @if (editMode()) {
                <div class="an-edit-bar" pDraggable="an-widget" (onDragStart)="dragIndex = i" (onDragEnd)="dragIndex = null">
                  <i class="pi pi-bars an-drag" pTooltip="Drag to reorder"></i>
                  <button type="button" class="an-icon-btn" (click)="move(i, -1)" pTooltip="Move earlier"><i class="pi pi-arrow-left"></i></button>
                  <button type="button" class="an-icon-btn" (click)="move(i, 1)" pTooltip="Move later"><i class="pi pi-arrow-right"></i></button>
                  <span class="an-muted an-small">W</span>
                  <button type="button" class="an-icon-btn" (click)="resize(i, -1, 0)"><i class="pi pi-minus"></i></button>
                  <button type="button" class="an-icon-btn" (click)="resize(i, 1, 0)"><i class="pi pi-plus"></i></button>
                  <span class="an-muted an-small">H</span>
                  <button type="button" class="an-icon-btn" (click)="resize(i, 0, -1)"><i class="pi pi-minus"></i></button>
                  <button type="button" class="an-icon-btn" (click)="resize(i, 0, 1)"><i class="pi pi-plus"></i></button>
                  <span class="an-spacer"></span>
                  <button type="button" class="an-icon-btn" (click)="openDesigner(i)" pTooltip="Edit widget"><i class="pi pi-pencil"></i></button>
                  <button type="button" class="an-icon-btn" (click)="removeWidget(i)" pTooltip="Remove"><i class="pi pi-trash"></i></button>
                </div>
              }
              <app-analytics-widget [spec]="w.spec" [title]="w.title" [height]="0"
                                    [externalFilters]="externals().get(w.id) ?? noFilters" [crossFilterMode]="true" [selectedKeys]="selections().get(w.id) ?? null"
                                    [autoRun]="!isHeavy(w)" [compact]="true"
                                    (crossFilter)="onCross(w, $event)" (filterDashboard)="addDashboardFilter($event)"
                                    (openAsReport)="openAsReport($event)" />
            </div>
          }
          @if (editMode() && def().widgets.length) {
            <div class="an-cell an-add-cell" [style.grid-column]="'span 3'" [style.height.px]="2 * rowPx">
              <p-button label="Add widget" icon="pi pi-plus" [text]="true" (onClick)="openDesigner(null)" />
            </div>
          }
        </div>
      </div>

      <p-drawer [(visible)]="designerOpen" position="right" [style]="{ width: 'min(1200px, 96vw)' }" [header]="editingIndex() === null ? 'Add widget' : 'Edit widget'" appendTo="body">
        <div class="an-drawer">
          <div class="an-form an-form-row">
            <div>
              <label>Title</label>
              <input class="p-inputtext p-inputtext-sm" [(ngModel)]="draftTitle" />
            </div>
            <div>
              <label>Start from a saved report</label>
              <p-select [options]="libraryReports()" optionLabel="name" optionValue="id" [ngModel]="null" (ngModelChange)="useReport($event)"
                        placeholder="(optional)" [filter]="true" size="small" appendTo="body" />
            </div>
          </div>
          <div class="an-drawer-body">
            <div class="an-drawer-designer"><app-report-designer [meta]="m" [(spec)]="draftSpec" /></div>
            <div class="an-drawer-preview">
              @if (draftSpec().dataset && draftSpec().measures.length) {
                <app-analytics-widget [spec]="draftSpec()" [title]="draftTitle" [height]="420" [externalFilters]="def().filters" [autoRun]="!draftHeavy()" />
              } @else { <div class="an-empty">Pick a dataset and add a value to preview.</div> }
            </div>
          </div>
          <div class="an-drawer-foot">
            <p-button label="Cancel" severity="secondary" [text]="true" (onClick)="designerOpen = false" />
            <p-button [label]="editingIndex() === null ? 'Add to dashboard' : 'Apply'" icon="pi pi-check"
                      [disabled]="!draftSpec().dataset || !draftSpec().measures.length" (onClick)="applyDesigner()" />
          </div>
        </div>
      </p-drawer>

      <app-item-props-dialog [(visible)]="propsOpen" [roles]="m.roles" [canPublish]="m.canPublish"
                             [header]="propsMode() === 'share' ? 'Share dashboard' : 'Save dashboard as'"
                             [saveLabel]="propsMode() === 'share' ? 'Update' : 'Save'"
                             [initial]="propsInitial()" (save)="onProps($event)" />
    }
  `,
})
export class AnalyticsDashboardComponent implements OnInit, OnDestroy {
  private api = inject(AnalyticsService);
  private toolbar = inject(GlobalToolbarService);
  private tabBar = inject(TabBarService);
  private router = inject(Router);
  private toast = inject(MessageService);
  private exportService = inject(ExportService);

  /** Embedded on /home: read-only, no toolbar/tab handling. */
  readonly homeMode = input(false);

  private widgets = viewChildren(AnalyticsWidgetComponent);

  readonly rowPx = ROW_PX;
  readonly meta = toSignal(this.api.meta());
  readonly item = signal<SavedItem | null>(null);
  readonly def = signal<DashboardDef>(emptyDef());
  readonly editMode = signal(false);
  readonly crossFilters = signal<CrossFilter[]>([]);
  readonly propsOpen = signal(false);
  readonly propsMode = signal<'saveAs' | 'share'>('saveAs');
  readonly saving = signal(false);
  readonly editingIndex = signal<number | null>(null);
  readonly draftSpec = signal<ReportSpec>(emptySpec(''));
  readonly libraryReports = signal<SavedItem[]>([]);
  designerOpen = false;
  draftTitle = '';
  dragIndex: number | null = null;

  private savedJson = JSON.stringify(emptyDef());
  private currentTabKey = ROUTE;
  private navSub?: Subscription;

  readonly canEdit = computed(() => !this.homeMode() && (!this.item() || this.item()!.canEdit));
  readonly draftHeavy = computed(() => !!this.dsOf(this.draftSpec().dataset)?.heavy);

  /** Every field usable as a dashboard filter: the union of the widgets' datasets (dims + dates). */
  readonly filterFields = computed(() => {
    const seen = new Map<string, { key: string; label: string }>();
    const keys = new Set(this.def().widgets.map(w => w.spec.dataset));
    for (const ds of this.meta()?.datasets ?? []) {
      if (!keys.has(ds.key)) continue;
      for (const f of ds.fields) if (f.kind !== 'measure' && !seen.has(f.key)) seen.set(f.key, { key: f.key, label: f.label });
    }
    const used = new Set(this.def().filters.map(f => f.field));
    return [...seen.values()].filter(f => !used.has(f.key)).sort((a, b) => a.label.localeCompare(b.label));
  });

  readonly propsInitial = computed<ItemProps>(() => {
    const it = this.item();
    return this.propsMode() === 'share' && it
      ? { name: it.name, description: it.description, sharedRoleIds: it.sharedRoleIds, isSystem: it.isSystem }
      : { name: it ? `${it.name} (copy)` : '', description: it?.description ?? '', sharedRoleIds: [], isSystem: false };
  });

  ngOnInit(): void {
    if (this.homeMode()) { this.loadHome(); return; }
    this.setToolbar();
    this.handleNavState();
    this.navSub = this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => { if (e.urlAfterRedirects.split('?')[0] === ROUTE) this.handleNavState(); });
  }

  ngOnDestroy(): void {
    if (this.homeMode()) return;
    this.saveDraft();
    this.navSub?.unsubscribe();
    this.toolbar.clear();
  }

  private setToolbar() {
    this.toolbar.set({
      title: 'Dashboard',
      save: { onClick: () => this.save(), loading: this.saving },
      add: { onClick: () => { this.editMode.set(true); this.openDesigner(null); } },
      refresh: { onClick: () => this.widgets().forEach(w => w.run()) },
      list: { onClick: () => this.router.navigate(['/analytics']) },
      export: {
        items: [
          { label: 'Excel (all widgets)', icon: 'pi pi-file-excel', command: () => this.exportExcel() },
          { label: 'PDF (as shown)', icon: 'pi pi-file-pdf', command: () => this.exportPdf() },
        ],
      },
    });
  }

  private loadHome() {
    const home = readHomeId();
    this.api.items().subscribe(items => {
      const dash = items.find(i => i.kind === 'dashboard' && i.id === home) ?? items.find(i => i.kind === 'dashboard' && i.isSystem);
      if (dash) this.api.item(dash.id).subscribe(it => this.apply(it));
    });
  }

  private handleNavState() {
    this.saveDraft();
    const st = (history.state ?? {}) as Record<string, unknown>;
    const draft = st['draft'] as DashboardDef | undefined;
    this.crossFilters.set([]);
    if (typeof st['dashboardId'] === 'number') {
      this.api.item(st['dashboardId'] as number).subscribe({
        next: it => {
          this.apply(it, draft);
          this.currentTabKey = `${ROUTE}#${it.id}`;
          this.tabBar.openTab({ key: this.currentTabKey, label: it.name, route: ROUTE, icon: 'pi-th-large', state: { dashboardId: it.id } });
          this.registerDirty();
        },
        error: () => this.toast.add({ severity: 'error', summary: 'Not found', detail: 'This dashboard no longer exists or is not shared with you.' }),
      });
    } else {
      this.item.set(null);
      this.currentTabKey = ROUTE;
      const tabDraft = this.tabBar.tabs().find(t => t.key === ROUTE)?.state?.['draft'] as DashboardDef | undefined;
      this.def.set(draft ?? tabDraft ?? emptyDef());
      this.savedJson = JSON.stringify(emptyDef());
      this.editMode.set(!this.def().widgets.length);
      this.registerDirty();
    }
  }

  private apply(it: SavedItem, draft?: DashboardDef) {
    this.item.set(it);
    const saved = JSON.parse(it.definition ?? '{}') as DashboardDef;
    saved.filters ??= [];
    saved.widgets ??= [];
    this.savedJson = JSON.stringify(saved);
    this.def.set(draft ?? saved);
  }

  private registerDirty() {
    this.tabBar.registerDirtyChecker(this.currentTabKey, () => JSON.stringify(this.def()) !== this.savedJson && this.def().widgets.length > 0);
  }

  private saveDraft() {
    const dirty = JSON.stringify(this.def()) !== this.savedJson;
    const id = this.item()?.id;
    if (!id && !this.def().widgets.length) return;
    this.tabBar.updateTabState(this.currentTabKey, id ? { dashboardId: id, ...(dirty ? { draft: this.def() } : {}) } : { draft: this.def() });
  }

  // ── datasets / filters ───────────────────────────────────────────────────

  dsOf(key: string): DatasetMeta | undefined { return this.meta()?.datasets.find(d => d.key === key); }

  isHeavy(w: DashboardWidget) { return !!this.dsOf(w.spec.dataset)?.heavy; }

  /** The dataset whose field metadata drives a dashboard filter editor. */
  datasetFor(field: string): DatasetMeta | undefined {
    const keys = this.def().widgets.map(w => w.spec.dataset);
    return (this.meta()?.datasets ?? []).filter(d => keys.includes(d.key)).find(d => d.fields.some(f => f.key === field))
      ?? this.meta()?.datasets.find(d => d.fields.some(f => f.key === field));
  }

  addFilter(field: string | null) {
    const ds = field ? this.datasetFor(field) : undefined;
    if (!field || !ds) return;
    this.def.update(d => ({ ...d, filters: [...d.filters, defaultFilter(ds, field)] }));
  }

  addDashboardFilter(f: SpecFilter) {
    this.def.update(d => ({ ...d, filters: [...d.filters.filter(x => x.field !== f.field), f] }));
  }

  setFilter(i: number, f: SpecFilter) { this.def.update(d => ({ ...d, filters: d.filters.map((x, j) => j === i ? f : x) })); }
  removeFilter(i: number) { this.def.update(d => ({ ...d, filters: d.filters.filter((_, j) => j !== i) })); }

  // ── cross-filter ─────────────────────────────────────────────────────────

  onCross(w: DashboardWidget, e: CrossFilterEvent) {
    const field = e.col.field!;
    const caption = dimText(e.key, e.caption, e.col);
    this.crossFilters.update(list => {
      const existing = list.find(c => c.field === field && c.sourceId === w.id);
      const others = list.filter(c => !(c.field === field && c.sourceId === w.id));
      if (!existing) return [...others, { sourceId: w.id, field, col: e.col, keys: [e.key], captions: [caption] }];
      const idx = existing.keys.findIndex(k => String(k) === String(e.key));
      if (idx >= 0) {
        // Clicking a selected mark toggles it off.
        const keys = existing.keys.filter((_, i) => i !== idx);
        const captions = existing.captions.filter((_, i) => i !== idx);
        return keys.length ? [...others, { ...existing, keys, captions }] : others;
      }
      if (e.multi && e.col.type !== 'date') return [...others, { ...existing, keys: [...existing.keys, e.key], captions: [...existing.captions, caption] }];
      return [...others, { ...existing, keys: [e.key], captions: [caption] }];
    });
  }

  clearCross(c: CrossFilter) { this.crossFilters.update(l => l.filter(x => x !== c)); }

  private crossToFilter(c: CrossFilter): SpecFilter {
    if (c.keys.length === 1) return filterFor(c.col, c.keys[0]);
    return { field: c.field, op: 'in', values: c.keys.map(k => k === null ? null : String(k)) };
  }

  readonly noFilters: SpecFilter[] = [];

  /** Per-widget external filters / selections as stable references (recomputed only when filters change). */
  readonly externals = computed(() => new Map(this.def().widgets.map(w => [w.id, this.externalFor(w)])));
  readonly selections = computed(() => new Map(this.def().widgets.map(w => [w.id, this.selectedFor(w)])));

  /** Dashboard filters + cross-filters from OTHER widgets (the source highlights instead of filtering itself). */
  externalFor(w: DashboardWidget): SpecFilter[] {
    const cross = this.crossFilters().filter(c => c.sourceId !== w.id).map(c => this.crossToFilter(c));
    const fields = new Set(cross.map(c => c.field));
    return [...this.def().filters.filter(f => !fields.has(f.field)), ...cross];
  }

  selectedFor(w: DashboardWidget): string[] | null {
    const own = this.crossFilters().filter(c => c.sourceId === w.id);
    return own.length ? own.flatMap(c => c.keys.map(k => String(k))) : null;
  }

  // ── layout editing ───────────────────────────────────────────────────────

  move(i: number, delta: number) {
    this.def.update(d => {
      const j = i + delta;
      if (j < 0 || j >= d.widgets.length) return d;
      const widgets = [...d.widgets];
      [widgets[i], widgets[j]] = [widgets[j], widgets[i]];
      return { ...d, widgets };
    });
  }

  dropOn(target: number) {
    const from = this.dragIndex;
    this.dragIndex = null;
    if (from === null || from === target) return;
    this.def.update(d => {
      const widgets = [...d.widgets];
      const [w] = widgets.splice(from, 1);
      widgets.splice(target, 0, w);
      return { ...d, widgets };
    });
  }

  resize(i: number, dw: number, dh: number) {
    this.def.update(d => ({
      ...d,
      widgets: d.widgets.map((w, j) => j === i ? { ...w, w: Math.min(12, Math.max(2, w.w + dw)), h: Math.min(8, Math.max(2, w.h + dh)) } : w),
    }));
  }

  removeWidget(i: number) { this.def.update(d => ({ ...d, widgets: d.widgets.filter((_, j) => j !== i) })); }

  openDesigner(i: number | null) {
    this.editingIndex.set(i);
    const w = i === null ? null : this.def().widgets[i];
    this.draftSpec.set(w ? structuredClone(w.spec) : emptySpec(''));
    this.draftTitle = w?.title ?? '';
    this.api.items().subscribe(items => this.libraryReports.set(items.filter(x => x.kind === 'report')));
    this.designerOpen = true;
  }

  useReport(id: number | null) {
    if (!id) return;
    this.api.item(id).subscribe(it => {
      this.draftSpec.set(JSON.parse(it.definition ?? '{}') as ReportSpec);
      this.draftTitle = this.draftTitle || it.name;
    });
  }

  applyDesigner() {
    const spec = this.draftSpec();
    const title = this.draftTitle.trim() || this.dsOf(spec.dataset)?.label || 'Widget';
    const i = this.editingIndex();
    this.def.update(d => {
      if (i !== null) return { ...d, widgets: d.widgets.map((w, j) => j === i ? { ...w, spec, title } : w) };
      const wide = ['table', 'pivot', 'line'].includes(spec.type);
      const w: DashboardWidget = { id: newId(), title, spec, w: spec.type === 'kpi' ? 3 : wide ? 12 : 6, h: spec.type === 'kpi' ? 2 : 4 };
      return { ...d, widgets: [...d.widgets, w] };
    });
    this.designerOpen = false;
  }

  openAsReport(spec: ReportSpec) {
    this.router.navigate(['/analytics/report'], { state: { analyticsSpec: spec } });
  }

  // ── persistence ──────────────────────────────────────────────────────────

  save() {
    const it = this.item();
    if (!it || !it.canEdit) { this.propsMode.set('saveAs'); this.propsOpen.set(true); return; }
    this.saving.set(true);
    this.api.update(it.id, {
      kind: 'dashboard', name: it.name, description: it.description, definition: JSON.stringify(this.def()),
      sharedRoleIds: it.sharedRoleIds, isSystem: it.isSystem,
    }).subscribe({
      next: saved => { this.afterSave(saved); this.toast.add({ severity: 'success', summary: 'Saved', detail: saved.name }); },
      error: err => this.fail(err),
    });
  }

  onProps(p: ItemProps) {
    const it = this.item();
    const req = { kind: 'dashboard' as const, ...p, definition: JSON.stringify(this.def()) };
    this.saving.set(true);
    const isShare = this.propsMode() === 'share' && !!it;
    (isShare ? this.api.update(it!.id, req) : this.api.create(req)).subscribe({
      next: saved => {
        if (!isShare) {
          this.tabBar.unregisterDirtyChecker(this.currentTabKey);
          if (this.currentTabKey === ROUTE) this.tabBar.updateTabState(ROUTE, undefined);
        }
        this.afterSave(saved);
        this.toast.add({ severity: 'success', summary: isShare ? 'Sharing updated' : 'Saved', detail: saved.name });
      },
      error: err => this.fail(err),
    });
  }

  private afterSave(saved: SavedItem) {
    this.saving.set(false);
    this.item.set(saved);
    this.savedJson = JSON.stringify(this.def());
    this.editMode.set(false);
    this.currentTabKey = `${ROUTE}#${saved.id}`;
    this.tabBar.openTab({ key: this.currentTabKey, label: saved.name, route: ROUTE, icon: 'pi-th-large', state: { dashboardId: saved.id } });
    this.registerDirty();
  }

  private fail(err: { error?: { message?: string } }) {
    this.saving.set(false);
    this.toast.add({ severity: 'error', summary: 'Could not save', detail: err?.error?.message ?? 'Unexpected error.' });
  }

  // ── export ───────────────────────────────────────────────────────────────

  private title() { return this.item()?.name ?? 'Dashboard'; }

  exportExcel() {
    const items = this.def().widgets.map(w => ({
      title: w.title,
      spec: mergeFilters(w.spec, this.dsOf(w.spec.dataset), this.externalFor(w)),
    }));
    if (!items.length) return;
    this.api.export(this.title(), items).subscribe({
      next: res => this.exportService.saveResponse(res, `${this.title()}.xlsx`),
      error: err => this.toast.add({ severity: 'error', summary: 'Export failed', detail: err?.error?.message }),
    });
  }

  exportPdf() {
    const filters = this.def().filters.map(f => filterText(f, this.datasetFor(f.field)));
    const cross = this.crossFilters().map(c => `${c.col.label} = ${c.captions.join(', ')}`);
    const subtitle = [`Generated ${new Date().toLocaleString('en-PH')}`, ...(this.meta()?.scope ? [`${this.meta()!.scope} only`] : []), ...filters, ...cross].join(' · ');
    this.exportService.exportPdfSections(this.title(), subtitle, this.widgets().map(w => w.pdfSection()));
  }
}
