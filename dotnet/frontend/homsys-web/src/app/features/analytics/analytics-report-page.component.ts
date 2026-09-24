import { Component, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { Subscription, filter } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { MessageService } from 'primeng/api';
import { AnalyticsService } from '../../core/services/analytics.service';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { TabBarService } from '../../core/services/tab-bar.service';
import { ExportService } from '../../core/services/export.service';
import { DashboardDef, ReportSpec, SavedItem, emptySpec } from '../../core/models/analytics.model';
import { ReportDesignerComponent } from './report-designer.component';
import { AnalyticsWidgetComponent } from './analytics-widget.component';
import { ItemPropsDialogComponent, ItemProps } from './item-props-dialog.component';
import { newId } from './analytics-viz';

const ROUTE = '/analytics/report';

@Component({
  selector: 'app-analytics-report-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, ToastModule, DialogModule, SelectModule,
    ReportDesignerComponent, AnalyticsWidgetComponent, ItemPropsDialogComponent],
  providers: [MessageService],
  template: `
    <p-toast position="top-right" />
    @if (meta(); as m) {
      <div class="an-report" [class.an-collapsed]="collapsed()">
        <aside class="an-report-side">
          <div class="an-side-head">
            <span class="an-side-title">{{ item()?.name ?? 'New report' }}</span>
            <button type="button" class="an-icon-btn" (click)="collapsed.set(!collapsed())" [attr.aria-label]="collapsed() ? 'Show designer' : 'Hide designer'">
              <i class="pi" [class.pi-angle-double-left]="!collapsed()" [class.pi-angle-double-right]="collapsed()"></i>
            </button>
          </div>
          @if (!collapsed()) { <app-report-designer [meta]="m" [(spec)]="spec" /> }
        </aside>
        <section class="an-report-main">
          <div class="an-report-actions">
            @if (item() && !item()!.canEdit) { <span class="an-badge"><i class="pi pi-eye"></i> View only — use Save As for your own copy</span> }
            @if (item()?.isSystem) { <span class="an-badge"><i class="pi pi-verified"></i> System template</span> }
            <span class="an-spacer"></span>
            <p-button label="Save As" icon="pi pi-copy" size="small" [text]="true" (onClick)="openSaveAs()" [disabled]="!canRun()" />
            @if (item()?.canEdit) { <p-button label="Share" icon="pi pi-share-alt" size="small" [text]="true" (onClick)="propsMode.set('share'); propsOpen.set(true)" /> }
            <p-button label="Add to dashboard" icon="pi pi-th-large" size="small" [text]="true" (onClick)="openAddToDashboard()" [disabled]="!canRun()" />
          </div>
          @if (canRun()) {
            <app-analytics-widget #preview [spec]="spec()" [title]="item()?.name ?? ''" [height]="0"
                                  [autoRun]="!heavy()" (openAsReport)="spec.set($event)" />
          } @else {
            <div class="an-empty an-start">
              <i class="pi pi-chart-bar"></i>
              <div>Pick a dataset, then add a value (measure) and optionally some rows.</div>
            </div>
          }
        </section>
      </div>

      <app-item-props-dialog [(visible)]="propsOpen" [roles]="m.roles" [canPublish]="m.canPublish"
                             [header]="propsMode() === 'share' ? 'Share report' : 'Save report as'"
                             [saveLabel]="propsMode() === 'share' ? 'Update' : 'Save'"
                             [initial]="propsInitial()" (save)="onProps($event)" />

      <p-dialog header="Add to dashboard" [(visible)]="addOpen" [modal]="true" [style]="{ width: '420px' }" appendTo="body">
        <div class="an-form">
          <label>Dashboard</label>
          <p-select [options]="dashboards()" optionLabel="name" optionValue="id" [(ngModel)]="targetDashboard"
                    placeholder="Choose a dashboard you can edit" styleClass="w-full" appendTo="body" />
          <label>Widget title</label>
          <input class="p-inputtext p-inputtext-sm" [(ngModel)]="widgetTitle" />
        </div>
        <ng-template pTemplate="footer">
          <p-button label="Cancel" severity="secondary" [text]="true" (onClick)="addOpen = false" />
          <p-button label="Add" icon="pi pi-plus" [disabled]="!targetDashboard" (onClick)="addToDashboard()" />
        </ng-template>
      </p-dialog>
    }
  `,
})
export class AnalyticsReportPageComponent implements OnInit, OnDestroy {
  private api = inject(AnalyticsService);
  private toolbar = inject(GlobalToolbarService);
  private tabBar = inject(TabBarService);
  private router = inject(Router);
  private toast = inject(MessageService);
  private exportService = inject(ExportService);

  private preview = viewChild<AnalyticsWidgetComponent>('preview');

  readonly meta = toSignal(this.api.meta());
  readonly spec = signal<ReportSpec>(emptySpec(''));
  readonly item = signal<SavedItem | null>(null);
  readonly collapsed = signal(false);
  readonly propsOpen = signal(false);
  readonly propsMode = signal<'saveAs' | 'share'>('saveAs');
  readonly dashboards = signal<SavedItem[]>([]);
  readonly saving = signal(false);
  addOpen = false;
  targetDashboard: number | null = null;
  widgetTitle = '';

  private savedJson = JSON.stringify(emptySpec(''));
  private currentTabKey = ROUTE;
  private navSub?: Subscription;

  readonly canRun = computed(() => !!this.spec().dataset && this.spec().measures.length > 0);
  readonly heavy = computed(() => !!this.meta()?.datasets.find(d => d.key === this.spec().dataset)?.heavy);
  readonly propsInitial = computed<ItemProps>(() => {
    const it = this.item();
    return this.propsMode() === 'share' && it
      ? { name: it.name, description: it.description, sharedRoleIds: it.sharedRoleIds, isSystem: it.isSystem }
      : { name: it ? `${it.name} (copy)` : '', description: it?.description ?? '', sharedRoleIds: [], isSystem: false };
  });

  ngOnInit(): void {
    this.setToolbar();
    this.handleNavState();
    this.navSub = this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => { if (e.urlAfterRedirects.split('?')[0] === ROUTE) this.handleNavState(); });
  }

  ngOnDestroy(): void {
    this.saveDraft();
    this.navSub?.unsubscribe();
    this.toolbar.clear();
  }

  private setToolbar() {
    this.toolbar.set({
      title: 'Report Builder',
      save: { onClick: () => this.save(), loading: this.saving },
      add: { onClick: () => this.newReport() },
      refresh: { onClick: () => this.preview()?.run() },
      list: { onClick: () => this.router.navigate(['/analytics']) },
      export: {
        items: [
          { label: 'Excel (all rows)', icon: 'pi pi-file-excel', command: () => this.preview()?.exportExcel() },
          { label: 'PDF (as shown)', icon: 'pi pi-file-pdf', command: () => this.exportPdf() },
        ],
      },
    });
  }

  private handleNavState() {
    this.saveDraft();
    const st = (history.state ?? {}) as Record<string, unknown>;
    const draft = st['draft'] as ReportSpec | undefined;
    if (typeof st['reportId'] === 'number') {
      this.loadItem(st['reportId'] as number, draft);
    } else if (st['analyticsSpec']) {
      this.item.set(null);
      this.currentTabKey = ROUTE;
      this.tabBar.openTab({ key: ROUTE, label: 'New Report', route: ROUTE, icon: 'pi-chart-bar' });
      this.spec.set(st['analyticsSpec'] as ReportSpec);
      this.savedJson = JSON.stringify(emptySpec(''));
      this.registerDirty();
    } else {
      this.item.set(null);
      this.currentTabKey = ROUTE;
      const tabDraft = this.tabBar.tabs().find(t => t.key === ROUTE)?.state?.['draft'] as ReportSpec | undefined;
      this.spec.set(draft ?? tabDraft ?? emptySpec(''));
      this.savedJson = JSON.stringify(emptySpec(''));
      this.registerDirty();
    }
  }

  private loadItem(id: number, draft?: ReportSpec) {
    this.api.item(id).subscribe({
      next: it => {
        this.item.set(it);
        this.currentTabKey = `${ROUTE}#${id}`;
        this.tabBar.openTab({ key: this.currentTabKey, label: it.name, route: ROUTE, icon: 'pi-chart-bar', state: { reportId: id } });
        const saved = JSON.parse(it.definition ?? '{}') as ReportSpec;
        this.savedJson = JSON.stringify(saved);
        this.spec.set(draft ?? saved);
        this.registerDirty();
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Not found', detail: 'This report no longer exists or is not shared with you.' }),
    });
  }

  private registerDirty() {
    const key = this.currentTabKey;
    this.tabBar.registerDirtyChecker(key, () => this.spec().dataset !== '' && JSON.stringify(this.spec()) !== this.savedJson);
  }

  private saveDraft() {
    if (!this.spec().dataset) return;
    const id = this.item()?.id;
    const dirty = JSON.stringify(this.spec()) !== this.savedJson;
    this.tabBar.updateTabState(this.currentTabKey, id ? { reportId: id, ...(dirty ? { draft: this.spec() } : {}) } : { draft: this.spec() });
  }

  newReport() {
    this.saveDraft();
    this.item.set(null);
    this.currentTabKey = ROUTE;
    this.spec.set(emptySpec(''));
    this.savedJson = JSON.stringify(emptySpec(''));
    this.tabBar.updateTabState(ROUTE, undefined);
    this.tabBar.openTab({ key: ROUTE, label: 'New Report', route: ROUTE, icon: 'pi-chart-bar' });
    this.registerDirty();
  }

  save() {
    const it = this.item();
    if (!this.canRun()) { this.toast.add({ severity: 'warn', summary: 'Nothing to save', detail: 'Add a dataset and at least one value first.' }); return; }
    if (!it || !it.canEdit) { this.openSaveAs(); return; }
    this.saving.set(true);
    this.api.update(it.id, {
      kind: 'report', name: it.name, description: it.description, definition: JSON.stringify(this.spec()),
      sharedRoleIds: it.sharedRoleIds, isSystem: it.isSystem,
    }).subscribe({
      next: saved => { this.afterSave(saved); this.toast.add({ severity: 'success', summary: 'Saved', detail: saved.name }); },
      error: err => this.fail(err),
    });
  }

  openSaveAs() {
    this.propsMode.set('saveAs');
    this.propsOpen.set(true);
  }

  onProps(p: ItemProps) {
    const it = this.item();
    const req = { kind: 'report' as const, ...p, definition: JSON.stringify(this.spec()) };
    this.saving.set(true);
    const call = this.propsMode() === 'share' && it ? this.api.update(it.id, req) : this.api.create(req);
    call.subscribe({
      next: saved => {
        const wasNew = this.propsMode() === 'saveAs';
        if (wasNew) {
          this.tabBar.unregisterDirtyChecker(this.currentTabKey);
          if (this.currentTabKey === ROUTE) this.tabBar.updateTabState(ROUTE, undefined);
        }
        this.afterSave(saved);
        this.toast.add({ severity: 'success', summary: wasNew ? 'Saved' : 'Sharing updated', detail: saved.name });
      },
      error: err => this.fail(err),
    });
  }

  private afterSave(saved: SavedItem) {
    this.saving.set(false);
    this.item.set(saved);
    this.savedJson = JSON.stringify(this.spec());
    this.currentTabKey = `${ROUTE}#${saved.id}`;
    this.tabBar.openTab({ key: this.currentTabKey, label: saved.name, route: ROUTE, icon: 'pi-chart-bar', state: { reportId: saved.id } });
    this.registerDirty();
  }

  private fail(err: { error?: { message?: string } }) {
    this.saving.set(false);
    this.toast.add({ severity: 'error', summary: 'Could not save', detail: err?.error?.message ?? 'Unexpected error.' });
  }

  openAddToDashboard() {
    this.widgetTitle = this.item()?.name ?? this.meta()?.datasets.find(d => d.key === this.spec().dataset)?.label ?? 'Widget';
    this.api.items().subscribe(items => {
      this.dashboards.set(items.filter(i => i.kind === 'dashboard' && i.canEdit));
      this.targetDashboard = null;
      this.addOpen = true;
    });
  }

  addToDashboard() {
    const id = this.targetDashboard!;
    this.api.item(id).subscribe({
      next: d => {
        const def = JSON.parse(d.definition ?? '{"v":1,"filters":[],"widgets":[]}') as DashboardDef;
        const wide = ['table', 'pivot', 'line'].includes(this.spec().type);
        def.widgets.push({ id: newId(), title: this.widgetTitle || 'Widget', w: this.spec().type === 'kpi' ? 3 : wide ? 12 : 6, h: this.spec().type === 'kpi' ? 2 : 4, spec: this.spec(), sourceItemId: this.item()?.id ?? null });
        this.api.update(id, { kind: 'dashboard', name: d.name, description: d.description, definition: JSON.stringify(def), sharedRoleIds: d.sharedRoleIds, isSystem: d.isSystem })
          .subscribe({
            next: () => { this.addOpen = false; this.toast.add({ severity: 'success', summary: 'Added', detail: `Pinned to ${d.name}` }); },
            error: err => this.fail(err),
          });
      },
      error: err => this.fail(err),
    });
  }

  exportPdf() {
    const w = this.preview();
    if (!w) return;
    const title = this.item()?.name ?? 'Report';
    const r = w.result();
    const subtitle = `Generated ${new Date().toLocaleString('en-PH')}` + (r?.scope ? ` · ${r.scope} only` : '');
    this.exportService.exportPdfSections(title, subtitle, [w.pdfSection()]);
  }
}
