import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageModule } from 'primeng/message';
import { ToastModule } from 'primeng/toast';
import { DrawerModule } from 'primeng/drawer';
import { TagModule } from 'primeng/tag';
import { DividerModule } from 'primeng/divider';
import { TextareaModule } from 'primeng/textarea';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { SiteTypeService } from '../../../core/services/site-type.service';
import { SiteTypeDto } from '../../../core/models/site-type.model';

@Component({
  selector: 'app-site-type-page',
  standalone: true,
  providers: [MessageService, ConfirmationService],
  imports: [
    ReactiveFormsModule, InputTextModule, ButtonModule,
    ToggleSwitchModule, MessageModule, ToastModule, DrawerModule,
    TagModule, DividerModule, TextareaModule, ConfirmDialogModule
  ],
  template: `
    <p-toast position="top-right" />
    <p-confirmDialog />

    <div class="form-card">
      @if (apiError()) {
        <p-message severity="error" styleClass="w-full mb-3">
          <span>{{ apiError() }}</span>
        </p-message>
      }

      <form [formGroup]="form" (ngSubmit)="onSubmit()" autocomplete="off">

        <div class="field-row">
          <div class="field">
            <label>Site Type Name @if (!selectedSiteType()) { <span class="required-star">*</span> }</label>
            @if (selectedSiteType()) {
              <input pInputText [value]="selectedSiteType()!.name" class="w-full" [disabled]="true" />
            } @else {
              <input pInputText formControlName="name" class="w-full" placeholder="e.g. Branch"
                (keydown.enter)="$event.preventDefault(); lookupSiteTypeName()" />
            }
            @if (isInvalid('name')) {
              <small class="field-error">Site type name is required.</small>
            }
          </div>
          <div class="field">
            <label>Code <span class="optional-label">(optional)</span></label>
            <input pInputText formControlName="code" class="w-full" placeholder="e.g. BR" />
          </div>
        </div>

        <div class="field">
          <label>Description <span class="optional-label">(optional)</span></label>
          <textarea pTextarea formControlName="description" class="w-full" rows="2"
            autoResize="true" placeholder="Brief description of this site type"></textarea>
        </div>

        @if (selectedSiteType()) {
          <div class="field-inline">
            <label>Active</label>
            <p-toggleswitch formControlName="isActive" />
          </div>
        }

        <p-divider />

        <div class="form-actions">
          @if (selectedSiteType()) {
            <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="clearSelection()" />
          }
          <p-button type="submit"
            [label]="selectedSiteType() ? 'Save Changes' : 'Create Site Type'"
            [loading]="loading()" />
        </div>
      </form>
    </div>

    <p-drawer [(visible)]="drawerVisible" position="right" header="All Site Types"
      [style]="{ width: '320px' }">
      <div style="padding-bottom:0.5rem">
        <input pInputText class="w-full" placeholder="Search..."
          (input)="drawerSearch.set($any($event.target).value.toLowerCase())" />
      </div>
      @if (drawerLoading()) {
        <div class="drawer-empty">Loading...</div>
      } @else if (filteredSiteTypes().length === 0) {
        <div class="drawer-empty">No site types found.</div>
      } @else {
        <div class="st-list">
          @for (st of filteredSiteTypes(); track st.id) {
            <div class="st-item" [class.selected]="selectedSiteType()?.id === st.id"
              (click)="loadSiteType(st)">
              <div class="st-avatar">{{ initials(st) }}</div>
              <div class="st-info">
                <span class="st-name">{{ st.name }}</span>
                @if (st.code) { <span class="st-sub">{{ st.code }}</span> }
              </div>
              <p-tag [value]="st.isActive ? 'Active' : 'Inactive'"
                [severity]="st.isActive ? 'success' : 'danger'" />
            </div>
          }
        </div>
      }
    </p-drawer>
  `,
  styles: [`
    .form-card {
      background: var(--p-surface-card);
      border-radius: 8px;
      padding: 1.25rem;
      max-width: 580px;
    }

    .field { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 1rem; }
    .field label { font-weight: 500; font-size: 0.875rem; }
    .field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .field-inline { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 1rem; }
    .form-actions { display: flex; justify-content: flex-end; gap: 0.5rem; }

    .required-star { color: var(--p-red-500, #ef4444); margin-left: 2px; }
    .optional-label { font-weight: 400; font-size: 0.75rem; color: var(--p-text-muted-color); }
    .field-error { color: var(--p-red-500, #ef4444); font-size: 0.78rem; }

    .drawer-empty { text-align: center; color: var(--p-text-muted-color); padding: 2rem 0; font-size: 0.85rem; }

    .st-list { display: flex; flex-direction: column; gap: 0.4rem; }

    .st-item {
      display: flex;
      align-items: center;
      gap: 0.65rem;
      padding: 0.55rem 0.65rem;
      border-radius: 6px;
      border: 1px solid var(--p-surface-border);
      cursor: pointer;
      transition: background 0.15s;
    }

    .st-item:hover { background: var(--p-surface-hover); }
    .st-item.selected { background: rgba(128, 0, 0, 0.07); border-color: #800000; }

    .st-avatar {
      width: 30px; height: 30px; border-radius: 50%;
      background: #800000; color: #fff;
      font-size: 0.68rem; font-weight: 700;
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
    }

    .st-info { flex: 1; display: flex; flex-direction: column; min-width: 0; }
    .st-name { font-size: 0.82rem; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .st-sub  { font-size: 0.72rem; color: var(--p-text-muted-color); }
  `]
})
export class SiteTypePageComponent implements OnInit, OnDestroy {
  private siteTypeService = inject(SiteTypeService);
  private fb              = inject(FormBuilder);
  private router          = inject(Router);
  private globalToolbar   = inject(GlobalToolbarService);
  private exportSvc       = inject(ExportService);
  private tabBar          = inject(TabBarService);
  private messageService  = inject(MessageService);
  private confirmSvc      = inject(ConfirmationService);

  private currentTabKey = '/site-types';
  private navSub?: Subscription;

  protected siteTypes          = signal<SiteTypeDto[]>([]);
  protected selectedSiteType   = signal<SiteTypeDto | undefined>(undefined);
  protected loading            = signal(false);
  protected deleting           = signal(false);
  protected refreshing         = signal(false);
  protected drawerLoading      = signal(false);
  protected drawerVisible      = false;
  protected apiError           = signal<string | null>(null);
  protected drawerSearch       = signal('');
  protected noSelection        = computed(() => !this.selectedSiteType());

  protected filteredSiteTypes = computed(() => {
    const term = this.drawerSearch();
    if (!term) return this.siteTypes();
    return this.siteTypes().filter(st =>
      st.name.toLowerCase().includes(term) ||
      st.code.toLowerCase().includes(term)
    );
  });

  protected form = this.fb.group({
    name:        ['', Validators.required],
    code:        [''],
    description: [''],
    isActive:    [true]
  });

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  ngOnInit() {
    this.handleNavState();
    this.navSub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe(() => this.handleNavState());
    this.syncToolbar();
    this.tabBar.registerDirtyChecker('/site-types', () => this.isNewFormDirty());
  }

  ngOnDestroy() {
    if (!this.selectedSiteType() && this.isNewFormDirty()) {
      this.tabBar.updateTabState('/site-types', { draftForm: this.form.value });
    }
    this.tabBar.unregisterDirtyChecker('/site-types');
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.globalToolbar.clear();
    this.navSub?.unsubscribe();
  }

  private isNewFormDirty(): boolean {
    const v = this.form.value;
    return !!(v.name?.trim() || v.code?.trim() || v.description?.trim());
  }

  private handleNavState() {
    const state       = history.state as Record<string, unknown>;
    const siteTypeId  = state?.['siteTypeId'] as number | undefined;
    const draftForm   = state?.['draftForm']  as Record<string, unknown> | undefined;
    if (siteTypeId) {
      if (siteTypeId !== this.selectedSiteType()?.id) {
        this.siteTypeService.getById(siteTypeId).subscribe({
          next: res => { if (res.data) this.loadSiteType(res.data); }
        });
      }
    } else {
      if (this.selectedSiteType()) this.resetFormOnly();
      if (draftForm) {
        this.form.patchValue(draftForm);
        this.tabBar.updateTabState('/site-types', undefined);
      }
    }
  }

  private resetFormOnly() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.selectedSiteType.set(undefined);
    this.apiError.set(null);
    this.form.reset({ isActive: true });
    this.form.markAsUntouched();
    this.currentTabKey = '/site-types';
    this.tabBar.updateTabState('/site-types', undefined);
    this.tabBar.registerDirtyChecker('/site-types', () => this.isNewFormDirty());
    this.syncToolbar();
  }

  private refreshRecord() {
    const st = this.selectedSiteType();
    if (st) {
      this.refreshing.set(true);
      this.siteTypeService.getById(st.id).subscribe({
        next: res => { this.refreshing.set(false); if (res.data) this.loadSiteType(res.data); },
        error: () => this.refreshing.set(false)
      });
    } else {
      this.resetFormOnly();
    }
  }

  private syncToolbar() {
    const st = this.selectedSiteType();
    this.globalToolbar.set({
      title:   st ? `Edit Site Type - ${st.name}` : 'Site Types',
      save:    { onClick: () => this.onSubmit(), loading: this.loading },
      edit:    { onClick: () => this.openDrawer(), disabled: this.noSelection },
      delete:  { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh: { onClick: () => this.refreshRecord(), loading: this.refreshing },
      list:    { onClick: () => this.router.navigate(['/site-types/list']) },
      find:    st ? undefined : { onClick: () => this.lookupSiteTypeName() },
      print:   { onClick: () => this.printRecord() },
      export:  { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv(), disabled: this.noSelection },
    });
  }

  private readonly exportCols: ExportColumn[] = [
    { header: 'Name',        field: 'name' },
    { header: 'Code',        field: 'code' },
    { header: 'Description', field: 'description' },
    { header: 'Status',      field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
  ];

  private printRecord() { window.print(); }
  private exportPdf() {
    const st = this.selectedSiteType();
    if (st) this.exportSvc.exportPdf(`Site Type — ${st.name}`, this.exportCols, [st as unknown as Record<string, unknown>]);
  }
  private exportExcel() {
    const st = this.selectedSiteType();
    if (st) this.exportSvc.exportExcel(`SiteType_${st.name}`, this.exportCols, [st as unknown as Record<string, unknown>]);
  }
  private exportCsv() {
    const st = this.selectedSiteType();
    if (st) this.exportSvc.exportCsv(`SiteType_${st.name}`, this.exportCols, [st as unknown as Record<string, unknown>]);
  }

  lookupSiteTypeName() {
    const raw = (this.form.get('name')?.value ?? '').trim();
    if (!raw || !raw.includes('*')) return;
    const term = raw.replace(/\*/g, '').trim();
    if (!term) return;
    this.siteTypeService.getAll().subscribe({
      next: res => {
        const exact = (res.data ?? []).find(st => st.name.toLowerCase() === term.toLowerCase());
        if (exact) {
          this.loadSiteType(exact);
        } else {
          this.router.navigate(['/site-types/list'], { state: { searchTerm: term } });
        }
      }
    });
  }

  openDrawer() {
    this.drawerVisible = true;
    this.drawerLoading.set(true);
    this.drawerSearch.set('');
    this.siteTypeService.getAll().subscribe({
      next: res => { this.siteTypes.set(res.data ?? []); this.drawerLoading.set(false); },
      error: () => this.drawerLoading.set(false)
    });
  }

  loadSiteType(st: SiteTypeDto) {
    this.selectedSiteType.set(st);
    this.drawerVisible = false;
    this.apiError.set(null);
    this.form.patchValue({
      name:        st.name,
      code:        st.code,
      description: st.description,
      isActive:    st.isActive
    });
    this.form.markAsUntouched();
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = `/site-types#${st.id}`;
    this.tabBar.openTab({
      key:   this.currentTabKey,
      label: `Edit Site Type - ${st.name}`,
      route: '/site-types',
      icon:  'pi-tag',
      state: { siteTypeId: st.id }
    });
    this.tabBar.registerDirtyChecker(this.currentTabKey, () => this.form.dirty);
    this.syncToolbar();
  }

  clearSelection() {
    const editTabKey = this.currentTabKey;
    this.resetFormOnly();
    if (editTabKey !== '/site-types') {
      this.tabBar.switchToDefaultTab('/site-types');
    }
  }

  confirmDelete() {
    const st = this.selectedSiteType();
    if (!st) return;
    this.confirmSvc.confirm({
      message: `Delete site type <strong>${st.name}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.siteTypeService.delete(st.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageService.add({ severity: 'success', summary: 'Deleted', detail: `Site type ${st.name} deleted.` });
            this.clearSelection();
          },
          error: (err) => {
            this.deleting.set(false);
            this.messageService.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to delete site type.' });
          }
        });
      }
    });
  }

  onSubmit() {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Validation',
        detail: 'Please fill in all required fields correctly.'
      });
      return;
    }

    this.loading.set(true);
    this.apiError.set(null);
    const v  = this.form.value;
    const st = this.selectedSiteType();

    const req$ = st
      ? this.siteTypeService.update(st.id, {
          name:        v.name!,
          code:        v.code ?? '',
          description: v.description ?? '',
          isActive:    v.isActive!
        })
      : this.siteTypeService.create({
          name:        v.name!,
          code:        v.code ?? '',
          description: v.description ?? ''
        });

    req$.subscribe({
      next: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'success',
          summary: 'Success',
          detail: st ? 'Site type updated successfully.' : 'Site type created successfully.'
        });
        if (!st) this.clearSelection();
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err?.error?.message
          ?? err?.error?.title
          ?? err?.message
          ?? `Error ${err?.status ?? ''}: An error occurred. Please try again.`;
        this.apiError.set(msg);
        this.messageService.add({ severity: 'error', summary: `Error ${err?.status ?? ''}`, detail: msg });
        console.error('Site type save error:', err);
      }
    });
  }

  initials(st: SiteTypeDto) {
    const words = st.name.trim().split(' ');
    return words.length >= 2
      ? `${words[0][0]}${words[1][0]}`.toUpperCase()
      : st.name.substring(0, 2).toUpperCase();
  }
}
