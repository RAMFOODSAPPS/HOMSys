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
import { SelectModule } from 'primeng/select';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { SiteService } from '../../../core/services/site.service';
import { CompanyService } from '../../../core/services/company.service';
import { SiteTypeService } from '../../../core/services/site-type.service';
import { SiteDto } from '../../../core/models/site.model';
import { CompanyDto } from '../../../core/models/company.model';
import { SiteTypeDto } from '../../../core/models/site-type.model';

@Component({
  selector: 'app-site-page',
  standalone: true,
  providers: [MessageService, ConfirmationService],
  imports: [
    ReactiveFormsModule, InputTextModule, ButtonModule,
    ToggleSwitchModule, MessageModule, ToastModule, DrawerModule,
    TagModule, DividerModule, TextareaModule, SelectModule, ConfirmDialogModule
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
            <label>Site Name @if (!selectedSite()) { <span class="required-star">*</span> }</label>
            @if (selectedSite()) {
              <input pInputText [value]="selectedSite()!.name" class="w-full" [disabled]="true" />
            } @else {
              <input pInputText formControlName="name" class="w-full" placeholder="e.g. Main Branch"
                (keydown.enter)="$event.preventDefault(); lookupSiteName()" />
            }
            @if (isInvalid('name')) {
              <small class="field-error">Site name is required.</small>
            }
          </div>
          <div class="field">
            <label>Code <span class="required-star">*</span></label>
            <input pInputText formControlName="code" class="w-full" placeholder="e.g. MB-01" />
            @if (isInvalid('code')) {
              <small class="field-error">Code is required.</small>
            }
          </div>
        </div>

        <div class="field-row">
          <div class="field">
            <label>Company <span class="required-star">*</span></label>
            <p-select
              formControlName="companyId"
              [options]="activeCompanies()"
              optionLabel="name"
              optionValue="id"
              placeholder="Select a company"
              class="w-full"
              [filter]="true"
              filterBy="name"
            />
            @if (isInvalid('companyId')) {
              <small class="field-error">Company is required.</small>
            }
          </div>
          <div class="field">
            <label>Site Type <span class="required-marker">*</span></label>
            <p-select
              formControlName="siteTypeId"
              [options]="activeSiteTypes()"
              optionLabel="name"
              optionValue="id"
              placeholder="Select a site type"
              class="w-full"
              [filter]="true"
              filterBy="name"
              [showClear]="false"
            />
            @if (isInvalid('siteTypeId')) {
              <small class="field-error">Site type is required.</small>
            }
          </div>
        </div>

        <div class="field-row">
          <div class="field">
            <label>Contact Person <span class="optional-label">(optional)</span></label>
            <input pInputText formControlName="contactPerson" class="w-full" placeholder="e.g. Juan dela Cruz" />
          </div>
          <div class="field">
            <label>Phone <span class="optional-label">(optional)</span></label>
            <input pInputText formControlName="phone" class="w-full" placeholder="e.g. 09XX-XXX-XXXX" />
          </div>
        </div>

        <div class="field">
          <label>Address <span class="optional-label">(optional)</span></label>
          <input pInputText formControlName="address" class="w-full" placeholder="e.g. 123 Main St, Cebu City" />
        </div>

        <div class="field">
          <label>Description <span class="optional-label">(optional)</span></label>
          <textarea pTextarea formControlName="description" class="w-full" rows="2"
            autoResize="true" placeholder="Brief description of this site"></textarea>
        </div>

        @if (selectedSite()) {
          <div class="field-inline">
            <label>Active</label>
            <p-toggleswitch formControlName="isActive" />
          </div>
        }

        <p-divider />

        <div class="form-actions">
          @if (selectedSite()) {
            <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="clearSelection()" />
          }
          <p-button type="submit"
            [label]="selectedSite() ? 'Save Changes' : 'Create Site'"
            [loading]="loading()" />
        </div>
      </form>
    </div>

    <p-drawer [(visible)]="drawerVisible" position="right" header="All Sites"
      [style]="{ width: '340px' }">
      <div style="padding-bottom:0.5rem">
        <input pInputText class="w-full" placeholder="Search..."
          (input)="drawerSearch.set($any($event.target).value.toLowerCase())" />
      </div>
      @if (drawerLoading()) {
        <div class="drawer-empty">Loading...</div>
      } @else if (filteredSites().length === 0) {
        <div class="drawer-empty">No sites found.</div>
      } @else {
        <div class="site-list">
          @for (site of filteredSites(); track site.id) {
            <div class="site-item" [class.selected]="selectedSite()?.id === site.id"
              (click)="loadSite(site)">
              <div class="site-avatar">{{ initials(site) }}</div>
              <div class="site-info">
                <span class="site-name">{{ site.name }}</span>
                <span class="site-sub">{{ site.companyName }}</span>
              </div>
              <p-tag [value]="site.isActive ? 'Active' : 'Inactive'"
                [severity]="site.isActive ? 'success' : 'danger'" />
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

    .site-list { display: flex; flex-direction: column; gap: 0.4rem; }

    .site-item {
      display: flex;
      align-items: center;
      gap: 0.65rem;
      padding: 0.55rem 0.65rem;
      border-radius: 6px;
      border: 1px solid var(--p-surface-border);
      cursor: pointer;
      transition: background 0.15s;
    }

    .site-item:hover { background: var(--p-surface-hover); }
    .site-item.selected { background: rgba(128, 0, 0, 0.07); border-color: #800000; }

    .site-avatar {
      width: 30px; height: 30px; border-radius: 50%;
      background: #800000; color: #fff;
      font-size: 0.68rem; font-weight: 700;
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
    }

    .site-info { flex: 1; display: flex; flex-direction: column; min-width: 0; }
    .site-name { font-size: 0.82rem; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .site-sub  { font-size: 0.72rem; color: var(--p-text-muted-color); }
  `]
})
export class SitePageComponent implements OnInit, OnDestroy {
  private siteService     = inject(SiteService);
  private companyService  = inject(CompanyService);
  private siteTypeService = inject(SiteTypeService);
  private fb              = inject(FormBuilder);
  private router         = inject(Router);
  private globalToolbar  = inject(GlobalToolbarService);
  private exportSvc      = inject(ExportService);
  private tabBar         = inject(TabBarService);
  private messageService = inject(MessageService);
  private confirmSvc     = inject(ConfirmationService);

  private currentTabKey = '/sites';
  private navSub?: Subscription;

  protected sites          = signal<SiteDto[]>([]);
  protected companies      = signal<CompanyDto[]>([]);
  protected siteTypes      = signal<SiteTypeDto[]>([]);
  protected selectedSite   = signal<SiteDto | undefined>(undefined);
  protected loading        = signal(false);
  protected deleting       = signal(false);
  protected refreshing     = signal(false);
  protected drawerLoading  = signal(false);
  protected drawerVisible  = false;
  protected apiError       = signal<string | null>(null);
  protected drawerSearch   = signal('');
  protected noSelection    = computed(() => !this.selectedSite());

  protected activeCompanies = computed(() =>
    this.companies().filter(c => c.isActive)
  );

  protected activeSiteTypes = computed(() =>
    this.siteTypes().filter(st => st.isActive)
  );

  protected filteredSites = computed(() => {
    const term = this.drawerSearch();
    if (!term) return this.sites();
    return this.sites().filter(s =>
      s.name.toLowerCase().includes(term) ||
      s.companyName.toLowerCase().includes(term) ||
      s.code.toLowerCase().includes(term)
    );
  });

  protected form = this.fb.group({
    name:          ['', Validators.required],
    code:          ['', Validators.required],
    companyId:     [null as number | null, Validators.required],
    siteTypeId:    [null as number | null, Validators.required],
    contactPerson: [''],
    phone:         [''],
    address:       [''],
    description:   [''],
    isActive:      [true]
  });

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  ngOnInit() {
    this.companyService.getAll().subscribe({
      next: res => this.companies.set(res.data ?? [])
    });
    this.siteTypeService.getAll().subscribe({
      next: res => this.siteTypes.set(res.data ?? [])
    });
    this.handleNavState();
    this.navSub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe(() => this.handleNavState());
    this.syncToolbar();
    this.tabBar.registerDirtyChecker('/sites', () => this.isNewFormDirty());
  }

  ngOnDestroy() {
    if (!this.selectedSite() && this.isNewFormDirty()) {
      this.tabBar.updateTabState('/sites', { draftForm: this.form.value });
    }
    this.tabBar.unregisterDirtyChecker('/sites');
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.globalToolbar.clear();
    this.navSub?.unsubscribe();
  }

  private isNewFormDirty(): boolean {
    const v = this.form.value;
    return !!(v.name?.trim() || v.code?.trim() || v.description?.trim() ||
              v.contactPerson?.trim() || v.phone?.trim() || v.address?.trim() || v.companyId);
  }

  private handleNavState() {
    const state     = history.state as Record<string, unknown>;
    const siteId    = state?.['siteId']    as number | undefined;
    const draftForm = state?.['draftForm'] as Record<string, unknown> | undefined;
    if (siteId) {
      if (siteId !== this.selectedSite()?.id) {
        this.siteService.getById(siteId).subscribe({
          next: res => { if (res.data) this.loadSite(res.data); }
        });
      }
    } else {
      if (this.selectedSite()) this.resetFormOnly();
      if (draftForm) {
        this.form.patchValue(draftForm);
        this.tabBar.updateTabState('/sites', undefined);
      }
    }
  }

  private resetFormOnly() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.selectedSite.set(undefined);
    this.apiError.set(null);
    this.form.reset({ isActive: true });
    this.form.markAsUntouched();
    this.currentTabKey = '/sites';
    this.tabBar.updateTabState('/sites', undefined);
    this.tabBar.registerDirtyChecker('/sites', () => this.isNewFormDirty());
    this.syncToolbar();
  }

  private refreshRecord() {
    const s = this.selectedSite();
    if (s) {
      this.refreshing.set(true);
      this.siteService.getById(s.id).subscribe({
        next: res => { this.refreshing.set(false); if (res.data) this.loadSite(res.data); },
        error: () => this.refreshing.set(false)
      });
    } else {
      this.resetFormOnly();
    }
  }

  private syncToolbar() {
    const s = this.selectedSite();
    this.globalToolbar.set({
      title:   s ? `Edit Site - ${s.name}` : 'Sites',
      save:    { onClick: () => this.onSubmit(), loading: this.loading },
      edit:    { onClick: () => this.openDrawer(), disabled: this.noSelection },
      delete:  { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh: { onClick: () => this.refreshRecord(), loading: this.refreshing },
      list:    { onClick: () => this.router.navigate(['/sites/list']) },
      find:    s ? undefined : { onClick: () => this.lookupSiteName() },
      print:   { onClick: () => this.printRecord() },
      export:  { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv(), disabled: this.noSelection },
    });
  }

  private readonly exportCols: ExportColumn[] = [
    { header: 'Name',           field: 'name' },
    { header: 'Code',           field: 'code' },
    { header: 'Company',        field: 'companyName' },
    { header: 'Contact Person', field: 'contactPerson' },
    { header: 'Phone',          field: 'phone' },
    { header: 'Address',        field: 'address' },
    { header: 'Description',    field: 'description' },
    { header: 'Status',         field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
  ];

  private printRecord() { window.print(); }
  private exportPdf()   {
    const s = this.selectedSite();
    if (s) this.exportSvc.exportPdf(`Site — ${s.name}`, this.exportCols, [s as unknown as Record<string, unknown>]);
  }
  private exportExcel() {
    const s = this.selectedSite();
    if (s) this.exportSvc.exportExcel(`Site_${s.name}`, this.exportCols, [s as unknown as Record<string, unknown>]);
  }
  private exportCsv() {
    const s = this.selectedSite();
    if (s) this.exportSvc.exportCsv(`Site_${s.name}`, this.exportCols, [s as unknown as Record<string, unknown>]);
  }

  lookupSiteName() {
    const raw = (this.form.get('name')?.value ?? '').trim();
    if (!raw || !raw.includes('*')) return;
    const term = raw.replace(/\*/g, '').trim();
    if (!term) return;
    this.siteService.getAll().subscribe({
      next: res => {
        const exact = (res.data ?? []).find(s => s.name.toLowerCase() === term.toLowerCase());
        if (exact) {
          this.loadSite(exact);
        } else {
          this.router.navigate(['/sites/list'], { state: { searchTerm: term } });
        }
      }
    });
  }

  openDrawer() {
    this.drawerVisible = true;
    this.drawerLoading.set(true);
    this.drawerSearch.set('');
    this.siteService.getAll().subscribe({
      next: res => { this.sites.set(res.data ?? []); this.drawerLoading.set(false); },
      error: () => this.drawerLoading.set(false)
    });
  }

  loadSite(site: SiteDto) {
    this.selectedSite.set(site);
    this.drawerVisible = false;
    this.apiError.set(null);
    this.form.patchValue({
      name:          site.name,
      code:          site.code,
      companyId:     site.companyId,
      siteTypeId:    site.siteTypeId ?? null,
      contactPerson: site.contactPerson,
      phone:         site.phone,
      address:       site.address,
      description:   site.description,
      isActive:      site.isActive
    });
    this.form.markAsUntouched();
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = `/sites#${site.id}`;
    this.tabBar.openTab({
      key:   this.currentTabKey,
      label: `Edit Site - ${site.name}`,
      route: '/sites',
      icon:  'pi-map-marker',
      state: { siteId: site.id }
    });
    this.tabBar.registerDirtyChecker(this.currentTabKey, () => this.form.dirty);
    this.syncToolbar();
  }

  clearSelection() {
    const editTabKey = this.currentTabKey;
    this.resetFormOnly();
    if (editTabKey !== '/sites') {
      this.tabBar.switchToDefaultTab('/sites');
    }
  }

  confirmDelete() {
    const s = this.selectedSite();
    if (!s) return;
    this.confirmSvc.confirm({
      message: `Delete site <strong>${s.name}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.siteService.delete(s.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageService.add({ severity: 'success', summary: 'Deleted', detail: `Site ${s.name} deleted.` });
            this.clearSelection();
          },
          error: (err) => {
            this.deleting.set(false);
            this.messageService.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to delete site.' });
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
    const v = this.form.value;
    const s = this.selectedSite();

    const req$ = s
      ? this.siteService.update(s.id, {
          name:          v.name!,
          code:          v.code ?? '',
          companyId:     v.companyId!,
          siteTypeId:    v.siteTypeId ?? null,
          contactPerson: v.contactPerson ?? '',
          phone:         v.phone ?? '',
          address:       v.address ?? '',
          description:   v.description ?? '',
          isActive:      v.isActive!
        })
      : this.siteService.create({
          name:          v.name!,
          code:          v.code ?? '',
          companyId:     v.companyId!,
          siteTypeId:    v.siteTypeId ?? null,
          contactPerson: v.contactPerson ?? '',
          phone:         v.phone ?? '',
          address:       v.address ?? '',
          description:   v.description ?? ''
        });

    req$.subscribe({
      next: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'success',
          summary: 'Success',
          detail: s ? 'Site updated successfully.' : 'Site created successfully.'
        });
        if (!s) this.clearSelection();
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err?.error?.message
          ?? err?.error?.title
          ?? err?.message
          ?? `Error ${err?.status ?? ''}: An error occurred. Please try again.`;
        this.apiError.set(msg);
        this.messageService.add({ severity: 'error', summary: `Error ${err?.status ?? ''}`, detail: msg });
        console.error('Site save error:', err);
      }
    });
  }

  initials(site: SiteDto) {
    const words = site.name.trim().split(' ');
    return words.length >= 2
      ? `${words[0][0]}${words[1][0]}`.toUpperCase()
      : site.name.substring(0, 2).toUpperCase();
  }
}
