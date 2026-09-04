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
import { DepartmentService } from '../../../core/services/department.service';
import { CompanyService } from '../../../core/services/company.service';
import { DepartmentDto } from '../../../core/models/department.model';
import { CompanyDto } from '../../../core/models/company.model';

@Component({
  selector: 'app-department-page',
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
            <label>Department Name @if (!selectedDepartment()) { <span class="required-star">*</span> }</label>
            @if (selectedDepartment()) {
              <input pInputText [value]="selectedDepartment()!.name" class="w-full" [disabled]="true" />
            } @else {
              <input pInputText formControlName="name" class="w-full" placeholder="e.g. Finance"
                (keydown.enter)="$event.preventDefault(); lookupDepartmentName()" />
            }
            @if (isInvalid('name')) {
              <small class="field-error">Department name is required.</small>
            }
          </div>
          <div class="field">
            <label>Code <span class="optional-label">(optional)</span></label>
            <input pInputText formControlName="code" class="w-full" placeholder="e.g. FIN" />
          </div>
        </div>

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
          <label>Description <span class="optional-label">(optional)</span></label>
          <textarea pTextarea formControlName="description" class="w-full" rows="2"
            autoResize="true" placeholder="Brief description of this department"></textarea>
        </div>

        @if (selectedDepartment()) {
          <div class="field-inline">
            <label>Active</label>
            <p-toggleswitch formControlName="isActive" />
          </div>
        }

        <p-divider />

        <div class="form-actions">
          @if (selectedDepartment()) {
            <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="clearSelection()" />
          }
          <p-button type="submit"
            [label]="selectedDepartment() ? 'Save Changes' : 'Create Department'"
            [loading]="loading()" />
        </div>
      </form>
    </div>

    <p-drawer [(visible)]="drawerVisible" position="right" header="All Departments"
      [style]="{ width: '340px' }">
      <div style="padding-bottom:0.5rem">
        <input pInputText class="w-full" placeholder="Search..."
          (input)="drawerSearch.set($any($event.target).value.toLowerCase())" />
      </div>
      @if (drawerLoading()) {
        <div class="drawer-empty">Loading...</div>
      } @else if (filteredDepartments().length === 0) {
        <div class="drawer-empty">No departments found.</div>
      } @else {
        <div class="dept-list">
          @for (dept of filteredDepartments(); track dept.id) {
            <div class="dept-item" [class.selected]="selectedDepartment()?.id === dept.id"
              (click)="loadDepartment(dept)">
              <div class="dept-avatar">{{ initials(dept) }}</div>
              <div class="dept-info">
                <span class="dept-name">{{ dept.name }}</span>
                <span class="dept-sub">{{ dept.companyName }}</span>
              </div>
              <p-tag [value]="dept.isActive ? 'Active' : 'Inactive'"
                [severity]="dept.isActive ? 'success' : 'danger'" />
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

    .dept-list { display: flex; flex-direction: column; gap: 0.4rem; }

    .dept-item {
      display: flex;
      align-items: center;
      gap: 0.65rem;
      padding: 0.55rem 0.65rem;
      border-radius: 6px;
      border: 1px solid var(--p-surface-border);
      cursor: pointer;
      transition: background 0.15s;
    }

    .dept-item:hover { background: var(--p-surface-hover); }
    .dept-item.selected { background: rgba(128, 0, 0, 0.07); border-color: #800000; }

    .dept-avatar {
      width: 30px; height: 30px; border-radius: 50%;
      background: #800000; color: #fff;
      font-size: 0.68rem; font-weight: 700;
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
    }

    .dept-info { flex: 1; display: flex; flex-direction: column; min-width: 0; }
    .dept-name { font-size: 0.82rem; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .dept-sub  { font-size: 0.72rem; color: var(--p-text-muted-color); }
  `]
})
export class DepartmentPageComponent implements OnInit, OnDestroy {
  private deptService    = inject(DepartmentService);
  private companyService = inject(CompanyService);
  private fb             = inject(FormBuilder);
  private router         = inject(Router);
  private globalToolbar  = inject(GlobalToolbarService);
  private exportSvc      = inject(ExportService);
  private tabBar         = inject(TabBarService);
  private messageService = inject(MessageService);
  private confirmSvc     = inject(ConfirmationService);

  private currentTabKey = '/departments';
  private navSub?: Subscription;

  protected departments        = signal<DepartmentDto[]>([]);
  protected companies          = signal<CompanyDto[]>([]);
  protected selectedDepartment = signal<DepartmentDto | undefined>(undefined);
  protected loading            = signal(false);
  protected deleting           = signal(false);
  protected refreshing         = signal(false);
  protected drawerLoading      = signal(false);
  protected drawerVisible      = false;
  protected apiError           = signal<string | null>(null);
  protected drawerSearch       = signal('');
  protected noSelection        = computed(() => !this.selectedDepartment());

  protected activeCompanies = computed(() =>
    this.companies().filter(c => c.isActive)
  );

  protected filteredDepartments = computed(() => {
    const term = this.drawerSearch();
    if (!term) return this.departments();
    return this.departments().filter(d =>
      d.name.toLowerCase().includes(term) ||
      d.companyName.toLowerCase().includes(term) ||
      d.code.toLowerCase().includes(term)
    );
  });

  protected form = this.fb.group({
    name:        ['', Validators.required],
    code:        [''],
    companyId:   [null as number | null, Validators.required],
    description: [''],
    isActive:    [true]
  });

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  ngOnInit() {
    this.companyService.getAll().subscribe({
      next: res => this.companies.set(res.data ?? [])
    });
    this.handleNavState();
    this.navSub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe(() => this.handleNavState());
    this.syncToolbar();
    this.tabBar.registerDirtyChecker('/departments', () => this.isNewFormDirty());
  }

  ngOnDestroy() {
    if (!this.selectedDepartment() && this.isNewFormDirty()) {
      this.tabBar.updateTabState('/departments', { draftForm: this.form.value });
    }
    this.tabBar.unregisterDirtyChecker('/departments');
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.globalToolbar.clear();
    this.navSub?.unsubscribe();
  }

  private isNewFormDirty(): boolean {
    const v = this.form.value;
    return !!(v.name?.trim() || v.code?.trim() || v.description?.trim() || v.companyId);
  }

  private handleNavState() {
    const state     = history.state as Record<string, unknown>;
    const deptId    = state?.['departmentId'] as number | undefined;
    const draftForm = state?.['draftForm']    as Record<string, unknown> | undefined;
    if (deptId) {
      if (deptId !== this.selectedDepartment()?.id) {
        this.deptService.getById(deptId).subscribe({
          next: res => { if (res.data) this.loadDepartment(res.data); }
        });
      }
    } else {
      if (this.selectedDepartment()) this.resetFormOnly();
      if (draftForm) {
        this.form.patchValue(draftForm);
        this.tabBar.updateTabState('/departments', undefined);
      }
    }
  }

  private resetFormOnly() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.selectedDepartment.set(undefined);
    this.apiError.set(null);
    this.form.reset({ isActive: true });
    this.form.markAsUntouched();
    this.currentTabKey = '/departments';
    this.tabBar.updateTabState('/departments', undefined);
    this.tabBar.registerDirtyChecker('/departments', () => this.isNewFormDirty());
    this.syncToolbar();
  }

  private refreshRecord() {
    const d = this.selectedDepartment();
    if (d) {
      this.refreshing.set(true);
      this.deptService.getById(d.id).subscribe({
        next: res => { this.refreshing.set(false); if (res.data) this.loadDepartment(res.data); },
        error: () => this.refreshing.set(false)
      });
    } else {
      this.resetFormOnly();
    }
  }

  private syncToolbar() {
    const d = this.selectedDepartment();
    this.globalToolbar.set({
      title:   d ? `Edit Department - ${d.name}` : 'Departments',
      save:    { onClick: () => this.onSubmit(), loading: this.loading },
      edit:    { onClick: () => this.openDrawer(), disabled: this.noSelection },
      delete:  { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh: { onClick: () => this.refreshRecord(), loading: this.refreshing },
      list:    { onClick: () => this.router.navigate(['/departments/list']) },
      find:    d ? undefined : { onClick: () => this.lookupDepartmentName() },
      print:   { onClick: () => this.printRecord() },
      export:  { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv(), disabled: this.noSelection },
    });
  }

  private readonly exportCols: ExportColumn[] = [
    { header: 'Name',        field: 'name' },
    { header: 'Code',        field: 'code' },
    { header: 'Company',     field: 'companyName' },
    { header: 'Description', field: 'description' },
    { header: 'Status',      field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
  ];

  private printRecord() { window.print(); }
  private exportPdf()   {
    const d = this.selectedDepartment();
    if (d) this.exportSvc.exportPdf(`Department — ${d.name}`, this.exportCols, [d as unknown as Record<string, unknown>]);
  }
  private exportExcel() {
    const d = this.selectedDepartment();
    if (d) this.exportSvc.exportExcel(`Department_${d.name}`, this.exportCols, [d as unknown as Record<string, unknown>]);
  }
  private exportCsv() {
    const d = this.selectedDepartment();
    if (d) this.exportSvc.exportCsv(`Department_${d.name}`, this.exportCols, [d as unknown as Record<string, unknown>]);
  }

  lookupDepartmentName() {
    const raw = (this.form.get('name')?.value ?? '').trim();
    if (!raw || !raw.includes('*')) return;
    const term = raw.replace(/\*/g, '').trim();
    if (!term) return;
    this.deptService.getAll().subscribe({
      next: res => {
        const exact = (res.data ?? []).find(d => d.name.toLowerCase() === term.toLowerCase());
        if (exact) {
          this.loadDepartment(exact);
        } else {
          this.router.navigate(['/departments/list'], { state: { searchTerm: term } });
        }
      }
    });
  }

  openDrawer() {
    this.drawerVisible = true;
    this.drawerLoading.set(true);
    this.drawerSearch.set('');
    this.deptService.getAll().subscribe({
      next: res => { this.departments.set(res.data ?? []); this.drawerLoading.set(false); },
      error: () => this.drawerLoading.set(false)
    });
  }

  loadDepartment(dept: DepartmentDto) {
    this.selectedDepartment.set(dept);
    this.drawerVisible = false;
    this.apiError.set(null);
    this.form.patchValue({
      name:        dept.name,
      code:        dept.code,
      companyId:   dept.companyId,
      description: dept.description,
      isActive:    dept.isActive
    });
    this.form.markAsUntouched();
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = `/departments#${dept.id}`;
    this.tabBar.openTab({
      key:   this.currentTabKey,
      label: `Edit Department - ${dept.name}`,
      route: '/departments',
      icon:  'pi-sitemap',
      state: { departmentId: dept.id }
    });
    this.tabBar.registerDirtyChecker(this.currentTabKey, () => this.form.dirty);
    this.syncToolbar();
  }

  clearSelection() {
    const editTabKey = this.currentTabKey;
    this.resetFormOnly();
    if (editTabKey !== '/departments') {
      this.tabBar.switchToDefaultTab('/departments');
    }
  }

  confirmDelete() {
    const d = this.selectedDepartment();
    if (!d) return;
    this.confirmSvc.confirm({
      message: `Delete department <strong>${d.name}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.deptService.delete(d.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageService.add({ severity: 'success', summary: 'Deleted', detail: `Department ${d.name} deleted.` });
            this.clearSelection();
          },
          error: (err) => {
            this.deleting.set(false);
            this.messageService.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to delete department.' });
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
    const d = this.selectedDepartment();

    const req$ = d
      ? this.deptService.update(d.id, {
          name:        v.name!,
          code:        v.code ?? '',
          companyId:   v.companyId!,
          description: v.description ?? '',
          isActive:    v.isActive!
        })
      : this.deptService.create({
          name:        v.name!,
          code:        v.code ?? '',
          companyId:   v.companyId!,
          description: v.description ?? ''
        });

    req$.subscribe({
      next: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'success',
          summary: 'Success',
          detail: d ? 'Department updated successfully.' : 'Department created successfully.'
        });
        if (!d) this.clearSelection();
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err?.error?.message ?? 'An error occurred. Please try again.';
        this.apiError.set(msg);
        this.messageService.add({ severity: 'error', summary: 'Error', detail: msg });
      }
    });
  }

  initials(dept: DepartmentDto) {
    const words = dept.name.trim().split(' ');
    return words.length >= 2
      ? `${words[0][0]}${words[1][0]}`.toUpperCase()
      : dept.name.substring(0, 2).toUpperCase();
  }
}
