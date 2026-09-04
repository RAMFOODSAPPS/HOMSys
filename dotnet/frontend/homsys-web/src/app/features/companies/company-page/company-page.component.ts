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
import { CompanyService } from '../../../core/services/company.service';
import { CompanyDto } from '../../../core/models/company.model';

@Component({
  selector: 'app-company-page',
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
            <label>Company Name @if (!selectedCompany()) { <span class="required-star">*</span> }</label>
            @if (selectedCompany()) {
              <input pInputText [value]="selectedCompany()!.name" class="w-full" [disabled]="true" />
            } @else {
              <input pInputText formControlName="name" class="w-full" placeholder="e.g. Ram Foods"
                (keydown.enter)="$event.preventDefault(); lookupCompanyName()" />
            }
            @if (isInvalid('name')) {
              <small class="field-error">Company name is required.</small>
            }
          </div>
          <div class="field">
            <label>Code <span class="optional-label">(optional)</span></label>
            <input pInputText formControlName="code" class="w-full" placeholder="e.g. RAMF" />
          </div>
        </div>

        <div class="field-row">
          <div class="field">
            <label>Contact Person <span class="optional-label">(optional)</span></label>
            <input pInputText formControlName="contactPerson" class="w-full" />
          </div>
          <div class="field">
            <label>Email <span class="optional-label">(optional)</span></label>
            <input pInputText formControlName="email" type="email" class="w-full" />
            @if (isInvalid('email')) {
              <small class="field-error">Enter a valid email address.</small>
            }
          </div>
        </div>

        <div class="field">
          <label>Phone <span class="optional-label">(optional)</span></label>
          <input pInputText formControlName="phone" class="w-full" />
        </div>

        <div class="field">
          <label>Address <span class="optional-label">(optional)</span></label>
          <textarea pTextarea formControlName="address" class="w-full" rows="2"
            autoResize="true"></textarea>
        </div>

        @if (selectedCompany()) {
          <div class="field-inline">
            <label>Active</label>
            <p-toggleswitch formControlName="isActive" />
          </div>
        }

        <p-divider />

        <div class="form-actions">
          @if (selectedCompany()) {
            <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="clearSelection()" />
          }
          <p-button type="submit"
            [label]="selectedCompany() ? 'Save Changes' : 'Create Company'"
            [loading]="loading()" />
        </div>
      </form>
    </div>

    <p-drawer [(visible)]="drawerVisible" position="right" header="All Companies"
      [style]="{ width: '340px' }">
      <div style="padding-bottom:0.5rem">
        <input pInputText class="w-full" placeholder="Search..."
          (input)="drawerSearch.set($any($event.target).value.toLowerCase())" />
      </div>
      @if (drawerLoading()) {
        <div class="drawer-empty">Loading...</div>
      } @else if (filteredCompanies().length === 0) {
        <div class="drawer-empty">No companies found.</div>
      } @else {
        <div class="company-list">
          @for (company of filteredCompanies(); track company.id) {
            <div class="company-item" [class.selected]="selectedCompany()?.id === company.id"
              (click)="loadCompany(company)">
              <div class="company-avatar">{{ initials(company) }}</div>
              <div class="company-info">
                <span class="company-name">{{ company.name }}</span>
                <span class="company-sub">{{ company.code || '—' }}</span>
              </div>
              <p-tag [value]="company.isActive ? 'Active' : 'Inactive'"
                [severity]="company.isActive ? 'success' : 'danger'" />
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

    .company-list { display: flex; flex-direction: column; gap: 0.4rem; }

    .company-item {
      display: flex;
      align-items: center;
      gap: 0.65rem;
      padding: 0.55rem 0.65rem;
      border-radius: 6px;
      border: 1px solid var(--p-surface-border);
      cursor: pointer;
      transition: background 0.15s;
    }

    .company-item:hover { background: var(--p-surface-hover); }
    .company-item.selected { background: rgba(128, 0, 0, 0.07); border-color: #800000; }

    .company-avatar {
      width: 30px; height: 30px; border-radius: 50%;
      background: #800000; color: #fff;
      font-size: 0.68rem; font-weight: 700;
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
    }

    .company-info { flex: 1; display: flex; flex-direction: column; min-width: 0; }
    .company-name { font-size: 0.82rem; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .company-sub  { font-size: 0.72rem; color: var(--p-text-muted-color); }
  `]
})
export class CompanyPageComponent implements OnInit, OnDestroy {
  private companyService = inject(CompanyService);
  private fb             = inject(FormBuilder);
  private router         = inject(Router);
  private globalToolbar  = inject(GlobalToolbarService);
  private exportSvc      = inject(ExportService);
  private tabBar         = inject(TabBarService);
  private messageService = inject(MessageService);
  private confirmSvc     = inject(ConfirmationService);

  private currentTabKey = '/companies';
  private navSub?: Subscription;

  protected companies         = signal<CompanyDto[]>([]);
  protected selectedCompany   = signal<CompanyDto | undefined>(undefined);
  protected loading           = signal(false);
  protected deleting          = signal(false);
  protected refreshing        = signal(false);
  protected drawerLoading     = signal(false);
  protected drawerVisible     = false;
  protected apiError          = signal<string | null>(null);
  protected drawerSearch      = signal('');
  protected noSelection       = computed(() => !this.selectedCompany());

  protected filteredCompanies = computed(() => {
    const term = this.drawerSearch();
    if (!term) return this.companies();
    return this.companies().filter(c =>
      c.name.toLowerCase().includes(term) ||
      c.code.toLowerCase().includes(term) ||
      c.contactPerson.toLowerCase().includes(term)
    );
  });

  protected form = this.fb.group({
    name:          ['', Validators.required],
    code:          [''],
    contactPerson: [''],
    email:         ['', Validators.email],
    phone:         [''],
    address:       [''],
    isActive:      [true]
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
    this.tabBar.registerDirtyChecker('/companies', () => this.isNewFormDirty());
  }

  ngOnDestroy() {
    if (!this.selectedCompany() && this.isNewFormDirty()) {
      this.tabBar.updateTabState('/companies', { draftForm: this.form.value });
    }
    this.tabBar.unregisterDirtyChecker('/companies');
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.globalToolbar.clear();
    this.navSub?.unsubscribe();
  }

  private isNewFormDirty(): boolean {
    const v = this.form.value;
    return !!(v.name?.trim() || v.code?.trim() || v.contactPerson?.trim() ||
              v.email?.trim() || v.phone?.trim() || v.address?.trim());
  }

  private handleNavState() {
    const state     = history.state as Record<string, unknown>;
    const companyId = state?.['companyId'] as number | undefined;
    const draftForm = state?.['draftForm'] as Record<string, unknown> | undefined;
    if (companyId) {
      if (companyId !== this.selectedCompany()?.id) {
        this.companyService.getById(companyId).subscribe({
          next: res => { if (res.data) this.loadCompany(res.data); }
        });
      }
    } else {
      if (this.selectedCompany()) this.resetFormOnly();
      if (draftForm) {
        this.form.patchValue(draftForm);
        this.tabBar.updateTabState('/companies', undefined);
      }
    }
  }

  private resetFormOnly() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.selectedCompany.set(undefined);
    this.apiError.set(null);
    this.form.reset({ isActive: true });
    this.form.markAsUntouched();
    this.currentTabKey = '/companies';
    this.tabBar.updateTabState('/companies', undefined);
    this.tabBar.registerDirtyChecker('/companies', () => this.isNewFormDirty());
    this.syncToolbar();
  }

  private refreshRecord() {
    const c = this.selectedCompany();
    if (c) {
      this.refreshing.set(true);
      this.companyService.getById(c.id).subscribe({
        next: res => { this.refreshing.set(false); if (res.data) this.loadCompany(res.data); },
        error: () => this.refreshing.set(false)
      });
    } else {
      this.resetFormOnly();
    }
  }

  private syncToolbar() {
    const c = this.selectedCompany();
    this.globalToolbar.set({
      title:   c ? `Edit Company - ${c.name}` : 'Companies',
      save:    { onClick: () => this.onSubmit(), loading: this.loading },
      edit:    { onClick: () => this.openDrawer(), disabled: this.noSelection },
      delete:  { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh: { onClick: () => this.refreshRecord(), loading: this.refreshing },
      list:    { onClick: () => this.router.navigate(['/companies/list']) },
      find:    c ? undefined : { onClick: () => this.lookupCompanyName() },
      print:   { onClick: () => this.printRecord() },
      export:  { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv(), disabled: this.noSelection },
    });
  }

  private readonly exportCols: ExportColumn[] = [
    { header: 'Name',           field: 'name' },
    { header: 'Code',           field: 'code' },
    { header: 'Contact Person', field: 'contactPerson' },
    { header: 'Email',          field: 'email' },
    { header: 'Phone',          field: 'phone' },
    { header: 'Address',        field: 'address' },
    { header: 'Status',         field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
  ];

  private printRecord() { window.print(); }
  private exportPdf()   {
    const c = this.selectedCompany();
    if (c) this.exportSvc.exportPdf(`Company — ${c.name}`, this.exportCols, [c as unknown as Record<string, unknown>]);
  }
  private exportExcel() {
    const c = this.selectedCompany();
    if (c) this.exportSvc.exportExcel(`Company_${c.name}`, this.exportCols, [c as unknown as Record<string, unknown>]);
  }
  private exportCsv() {
    const c = this.selectedCompany();
    if (c) this.exportSvc.exportCsv(`Company_${c.name}`, this.exportCols, [c as unknown as Record<string, unknown>]);
  }

  lookupCompanyName() {
    const raw = (this.form.get('name')?.value ?? '').trim();
    if (!raw || !raw.includes('*')) return;
    const term = raw.replace(/\*/g, '').trim();
    if (!term) return;
    this.companyService.getAll().subscribe({
      next: res => {
        const exact = (res.data ?? []).find(c => c.name.toLowerCase() === term.toLowerCase());
        if (exact) {
          this.loadCompany(exact);
        } else {
          this.router.navigate(['/companies/list'], { state: { searchTerm: term } });
        }
      }
    });
  }

  openDrawer() {
    this.drawerVisible = true;
    this.drawerLoading.set(true);
    this.drawerSearch.set('');
    this.companyService.getAll().subscribe({
      next: res => { this.companies.set(res.data ?? []); this.drawerLoading.set(false); },
      error: () => this.drawerLoading.set(false)
    });
  }

  loadCompany(company: CompanyDto) {
    this.selectedCompany.set(company);
    this.drawerVisible = false;
    this.apiError.set(null);
    this.form.patchValue({
      name: company.name, code: company.code,
      contactPerson: company.contactPerson, email: company.email,
      phone: company.phone, address: company.address, isActive: company.isActive
    });
    this.form.markAsUntouched();
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = `/companies#${company.id}`;
    this.tabBar.openTab({
      key:   this.currentTabKey,
      label: `Edit Company - ${company.name}`,
      route: '/companies',
      icon:  'pi-building',
      state: { companyId: company.id }
    });
    this.tabBar.registerDirtyChecker(this.currentTabKey, () => this.form.dirty);
    this.syncToolbar();
  }

  clearSelection() {
    const editTabKey = this.currentTabKey;
    this.resetFormOnly();
    if (editTabKey !== '/companies') {
      this.tabBar.switchToDefaultTab('/companies');
    }
  }

  confirmDelete() {
    const c = this.selectedCompany();
    if (!c) return;
    this.confirmSvc.confirm({
      message: `Delete company <strong>${c.name}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.companyService.delete(c.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageService.add({ severity: 'success', summary: 'Deleted', detail: `Company ${c.name} deleted.` });
            this.clearSelection();
          },
          error: (err) => {
            this.deleting.set(false);
            this.messageService.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to delete company.' });
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
    const c = this.selectedCompany();

    const req$ = c
      ? this.companyService.update(c.id, {
          name: v.name!, code: v.code ?? '',
          contactPerson: v.contactPerson ?? '', email: v.email ?? '',
          phone: v.phone ?? '', address: v.address ?? '',
          isActive: v.isActive!
        })
      : this.companyService.create({
          name: v.name!, code: v.code ?? '',
          contactPerson: v.contactPerson ?? '', email: v.email ?? '',
          phone: v.phone ?? '', address: v.address ?? ''
        });

    req$.subscribe({
      next: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'success',
          summary: 'Success',
          detail: c ? 'Company updated successfully.' : 'Company created successfully.'
        });
        if (!c) this.clearSelection();
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err?.error?.message ?? 'An error occurred. Please try again.';
        this.apiError.set(msg);
        this.messageService.add({ severity: 'error', summary: 'Error', detail: msg });
      }
    });
  }

  initials(company: CompanyDto) {
    const words = company.name.trim().split(' ');
    return words.length >= 2
      ? `${words[0][0]}${words[1][0]}`.toUpperCase()
      : company.name.substring(0, 2).toUpperCase();
  }
}
