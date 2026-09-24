import { Component, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { Menu, MenuModule } from 'primeng/menu';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MenuItem, MessageService } from 'primeng/api';
import { AnalyticsService } from '../../core/services/analytics.service';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { SavedItem, VISUALS } from '../../core/models/analytics.model';
import { ItemPropsDialogComponent, ItemProps } from './item-props-dialog.component';

export const HOME_DASHBOARD_KEY = 'homsys.homeDashboardId';

@Component({
  selector: 'app-analytics-library-page',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, ButtonModule, SelectButtonModule, MenuModule, ToastModule,
    ConfirmDialogModule, TooltipModule, ItemPropsDialogComponent],
  providers: [MessageService, ConfirmationService],
  template: `
    <p-toast position="top-right" />
    <p-confirmdialog />
    <div class="an-library">
      <div class="an-library-head">
        <p-selectButton [options]="kinds" optionLabel="label" optionValue="value" [ngModel]="kind()" (ngModelChange)="kind.set($event)" size="small" />
        <span class="an-spacer"></span>
        <p-button label="New report" icon="pi pi-chart-bar" size="small" (onClick)="router.navigate(['/analytics/report'])" />
        <p-button label="New dashboard" icon="pi pi-th-large" size="small" [outlined]="true" (onClick)="router.navigate(['/analytics/dashboard'])" />
      </div>

      @for (sec of sections(); track sec.label) {
        <div class="an-section-head">{{ sec.label }} <span class="an-muted">({{ sec.items.length }})</span></div>
        @if (sec.items.length) {
          <p-table [value]="sec.items" size="small" styleClass="an-table" [rowHover]="true">
            <ng-template pTemplate="header">
              <tr><th style="width:2.5rem"></th><th>Name</th><th>Data</th><th>Owner</th><th>Access</th><th>Updated</th><th style="width:3rem"></th></tr>
            </ng-template>
            <ng-template pTemplate="body" let-it>
              <tr (dblclick)="open(it)" class="an-clickable">
                <td><i [class]="icon(it)"></i></td>
                <td>
                  <a class="an-link" (click)="open(it)">{{ it.name }}</a>
                  @if (homeId() === it.id) { <i class="pi pi-home an-muted" pTooltip="Your home dashboard"></i> }
                  @if (it.description) { <div class="an-muted an-small">{{ it.description }}</div> }
                </td>
                <td>{{ it.kind === 'dashboard' ? 'Dashboard' : datasetLabel(it.datasetKey) }}</td>
                <td>{{ it.ownerName }}</td>
                <td>{{ access(it) }}</td>
                <td>{{ it.updatedAt | date:'MMM d, y' }}</td>
                <td><p-button icon="pi pi-ellipsis-v" size="small" [text]="true" [rounded]="true" (onClick)="openMenu($event, it)" /></td>
              </tr>
            </ng-template>
          </p-table>
        } @else {
          <div class="an-empty an-small">{{ sec.empty }}</div>
        }
      }
    </div>
    <p-menu #rowMenu [popup]="true" [model]="menuItems()" appendTo="body" />

    <app-item-props-dialog [(visible)]="shareOpen" header="Share" saveLabel="Update" [roles]="meta()?.roles ?? []"
                           [canPublish]="meta()?.canPublish ?? false" [initial]="shareInitial()" (save)="onShare($event)" />
  `,
})
export class AnalyticsLibraryPageComponent implements OnInit, OnDestroy {
  private api = inject(AnalyticsService);
  private toolbar = inject(GlobalToolbarService);
  private toast = inject(MessageService);
  private confirm = inject(ConfirmationService);
  readonly router = inject(Router);
  private rowMenu = viewChild.required<Menu>('rowMenu');

  readonly meta = toSignal(this.api.meta());
  readonly items = signal<SavedItem[]>([]);
  readonly kind = signal<'all' | 'dashboard' | 'report'>('all');
  readonly search = signal('');
  readonly menuItems = signal<MenuItem[]>([]);
  readonly shareOpen = signal(false);
  readonly shareTarget = signal<SavedItem | null>(null);
  readonly homeId = signal<number | null>(readHomeId());
  readonly kinds = [{ label: 'All', value: 'all' }, { label: 'Dashboards', value: 'dashboard' }, { label: 'Reports', value: 'report' }];

  readonly sections = computed(() => {
    const q = this.search().trim().toLowerCase();
    const list = this.items().filter(i =>
      (this.kind() === 'all' || i.kind === this.kind()) &&
      (!q || i.name.toLowerCase().includes(q) || i.description.toLowerCase().includes(q)));
    return [
      { label: 'Templates', items: list.filter(i => i.isSystem), empty: 'No system templates yet.' },
      { label: 'Shared with me', items: list.filter(i => !i.isSystem && !i.isOwner), empty: 'Nothing has been shared with your roles.' },
      { label: 'My reports & dashboards', items: list.filter(i => !i.isSystem && i.isOwner), empty: 'Create a report or dashboard to get started.' },
    ];
  });

  readonly shareInitial = computed<ItemProps>(() => {
    const it = this.shareTarget();
    return { name: it?.name ?? '', description: it?.description ?? '', sharedRoleIds: it?.sharedRoleIds ?? [], isSystem: it?.isSystem ?? false };
  });

  ngOnInit(): void {
    this.toolbar.set({
      title: 'Data Analytics',
      add: { onClick: () => this.router.navigate(['/analytics/report']) },
      refresh: { onClick: () => this.load() },
      search: term => this.search.set(term),
    });
    this.load();
  }

  ngOnDestroy(): void { this.toolbar.clear(); }

  load() { this.api.items().subscribe(items => this.items.set(items)); }

  icon(it: SavedItem) {
    return it.kind === 'dashboard' ? 'pi pi-th-large' : VISUALS.find(v => v.value === it.visual)?.icon ?? 'pi pi-chart-bar';
  }

  datasetLabel(key: string | null) { return this.meta()?.datasets.find(d => d.key === key)?.label ?? key ?? ''; }

  access(it: SavedItem) {
    if (it.isSystem) return 'Everyone';
    if (!it.sharedRoleIds.length) return 'Private';
    const roles = this.meta()?.roles ?? [];
    return 'Shared: ' + it.sharedRoleIds.map(id => roles.find(r => r.id === id)?.name ?? id).join(', ');
  }

  open(it: SavedItem) {
    if (it.kind === 'dashboard') this.router.navigate(['/analytics/dashboard'], { state: { dashboardId: it.id } });
    else this.router.navigate(['/analytics/report'], { state: { reportId: it.id } });
  }

  openMenu(e: Event, it: SavedItem) {
    const items: MenuItem[] = [
      { label: 'Open', icon: 'pi pi-folder-open', command: () => this.open(it) },
      { label: 'Save a copy', icon: 'pi pi-copy', command: () => this.copy(it) },
    ];
    if (it.canEdit) items.push({ label: 'Share…', icon: 'pi pi-share-alt', command: () => { this.shareTarget.set(it); this.shareOpen.set(true); } });
    if (it.kind === 'dashboard') {
      items.push(this.homeId() === it.id
        ? { label: 'Unset as Home', icon: 'pi pi-home', command: () => this.setHome(null) }
        : { label: 'Set as Home', icon: 'pi pi-home', command: () => this.setHome(it.id) });
    }
    if (it.canEdit) items.push({ separator: true }, { label: 'Delete', icon: 'pi pi-trash', command: () => this.remove(it) });
    this.menuItems.set(items);
    this.rowMenu().toggle(e);
  }

  copy(it: SavedItem) {
    this.api.item(it.id).subscribe(full => this.api.create({
      kind: full.kind, name: `${full.name} (copy)`.slice(0, 100), description: full.description,
      definition: full.definition ?? '{}', sharedRoleIds: [], isSystem: false,
    }).subscribe({
      next: saved => { this.toast.add({ severity: 'success', summary: 'Copied', detail: saved.name }); this.load(); },
      error: err => this.toast.add({ severity: 'error', summary: 'Copy failed', detail: err?.error?.message }),
    }));
  }

  onShare(p: ItemProps) {
    const it = this.shareTarget();
    if (!it) return;
    this.api.item(it.id).subscribe(full => this.api.update(it.id, {
      kind: full.kind, ...p, definition: full.definition ?? '{}',
    }).subscribe({
      next: () => { this.toast.add({ severity: 'success', summary: 'Updated', detail: p.name }); this.load(); },
      error: err => this.toast.add({ severity: 'error', summary: 'Update failed', detail: err?.error?.message }),
    }));
  }

  setHome(id: number | null) {
    try { id === null ? localStorage.removeItem(HOME_DASHBOARD_KEY) : localStorage.setItem(HOME_DASHBOARD_KEY, String(id)); } catch { /* storage unavailable */ }
    this.homeId.set(id);
  }

  remove(it: SavedItem) {
    this.confirm.confirm({
      header: 'Delete',
      message: `Delete "${it.name}"? Anyone it is shared with will lose access.`,
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.delete(it.id).subscribe({
        next: () => { this.toast.add({ severity: 'success', summary: 'Deleted', detail: it.name }); this.load(); },
        error: err => this.toast.add({ severity: 'error', summary: 'Delete failed', detail: err?.error?.message }),
      }),
    });
  }
}

export function readHomeId(): number | null {
  try {
    const v = localStorage.getItem(HOME_DASHBOARD_KEY);
    return v ? Number(v) : null;
  } catch {
    return null;
  }
}
