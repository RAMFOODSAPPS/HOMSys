import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { ToastModule } from 'primeng/toast';
import { DividerModule } from 'primeng/divider';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { RoleService } from '../../../core/services/role.service';
import { RoleDto } from '../../../core/models/user.model';

@Component({
  selector: 'app-role-page',
  standalone: true,
  providers: [MessageService, ConfirmationService],
  imports: [ReactiveFormsModule, InputTextModule, TextareaModule, ButtonModule, MessageModule, ToastModule, DividerModule, ConfirmDialogModule],
  template: `
    <p-toast position="top-right" />
    <p-confirmDialog />

    <div class="form-card">
      @if (successMessage()) {
        <p-message severity="success" styleClass="w-full mb-3">
          <span>{{ successMessage() }}</span>
        </p-message>
      }
      @if (errorMessage()) {
        <p-message severity="error" styleClass="w-full mb-3">
          <span>{{ errorMessage() }}</span>
        </p-message>
      }

      <form [formGroup]="form" (ngSubmit)="onSubmit()">
        <div class="field">
          <label>Role Name @if (!selectedRole()) { * }</label>
          @if (selectedRole()) {
            <input pInputText [value]="selectedRole()!.name" class="w-full" [disabled]="true" />
          } @else {
            <input pInputText formControlName="name" class="w-full" placeholder="e.g. Manager"
              (keydown.enter)="$event.preventDefault(); lookupRoleName()" />
          }
        </div>

        <div class="field">
          <label>Description</label>
          <textarea pTextarea formControlName="description" class="w-full" rows="3"
            placeholder="Optional description of this role"></textarea>
        </div>

        <p-divider />

        <div class="form-actions">
          @if (selectedRole()) {
            <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="clearSelection()" />
          }
          <p-button type="submit"
            [label]="selectedRole() ? 'Save Changes' : 'Create Role'"
            [loading]="loading()" [disabled]="form.invalid" />
        </div>
      </form>
    </div>
  `,
  styles: [`
    .form-card {
      background: var(--p-surface-card);
      border-radius: 8px;
      padding: 1.25rem;
      max-width: 480px;
    }

    .field { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 1rem; }
    .field label { font-weight: 500; font-size: 0.875rem; }
    .form-actions { display: flex; justify-content: flex-end; gap: 0.5rem; }
  `]
})
export class RolePageComponent implements OnInit, OnDestroy {
  private roleService   = inject(RoleService);
  private fb            = inject(FormBuilder);
  private router        = inject(Router);
  private globalToolbar = inject(GlobalToolbarService);
  private exportSvc     = inject(ExportService);
  private tabBar        = inject(TabBarService);
  private confirmSvc    = inject(ConfirmationService);
  private messageSvc    = inject(MessageService);

  private currentTabKey = '/roles';
  private navSub?: Subscription;

  protected selectedRole   = signal<RoleDto | undefined>(undefined);
  protected loading        = signal(false);
  protected deleting       = signal(false);
  protected refreshing     = signal(false);
  protected errorMessage   = signal<string | null>(null);
  protected successMessage = signal<string | null>(null);
  protected noSelection    = computed(() => !this.selectedRole());

  protected form = this.fb.group({
    name:        ['', [Validators.required, Validators.minLength(2)]],
    description: ['']
  });

  ngOnInit() {
    this.handleNavState();
    this.navSub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe(() => this.handleNavState());
    this.syncToolbar();
    this.tabBar.registerDirtyChecker('/roles', () => this.isNewFormDirty());
  }

  ngOnDestroy() {
    if (!this.selectedRole() && this.isNewFormDirty()) {
      this.tabBar.updateTabState('/roles', { draftForm: this.form.value });
    }
    this.tabBar.unregisterDirtyChecker('/roles');
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.globalToolbar.clear();
    this.navSub?.unsubscribe();
  }

  private isNewFormDirty(): boolean {
    const v = this.form.value;
    return !!(v.name?.trim() || v.description?.trim());
  }

  private handleNavState() {
    const state     = history.state as Record<string, unknown>;
    const roleId    = state?.['roleId']    as number | undefined;
    const draftForm = state?.['draftForm'] as Record<string, unknown> | undefined;
    if (roleId) {
      if (roleId !== this.selectedRole()?.id) {
        this.roleService.getById(roleId).subscribe({
          next: res => { if (res.data) this.loadRole(res.data); }
        });
      }
    } else {
      if (this.selectedRole()) this.resetFormOnly();
      if (draftForm) {
        this.form.patchValue(draftForm);
        this.tabBar.updateTabState('/roles', undefined);
      }
    }
  }

  private resetFormOnly() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.selectedRole.set(undefined);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.form.reset();
    this.currentTabKey = '/roles';
    this.tabBar.updateTabState('/roles', undefined);
    this.tabBar.registerDirtyChecker('/roles', () => this.isNewFormDirty());
    this.syncToolbar();
  }

  private refreshRecord() {
    const r = this.selectedRole();
    if (r) {
      this.refreshing.set(true);
      this.roleService.getById(r.id).subscribe({
        next: res => { this.refreshing.set(false); if (res.data) this.loadRole(res.data); },
        error: () => this.refreshing.set(false)
      });
    } else {
      this.resetFormOnly();
    }
  }

  private syncToolbar() {
    const r = this.selectedRole();
    this.globalToolbar.set({
      title:   r ? `Edit Role - ${r.name}` : 'Roles',
      save:    { onClick: () => this.onSubmit(), loading: this.loading },
      delete:  { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh: { onClick: () => this.refreshRecord(), loading: this.refreshing },
      list:    { onClick: () => this.router.navigate(['/roles/list']) },
      find:    r ? undefined : { onClick: () => this.lookupRoleName() },
      print:   { onClick: () => this.printRecord() },
      export:  { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv(), disabled: this.noSelection },
    });
  }

  private readonly exportCols: ExportColumn[] = [
    { header: 'Name',        field: 'name' },
    { header: 'Description', field: 'description' },
  ];

  private printRecord() { window.print(); }
  private exportPdf()   {
    const r = this.selectedRole();
    if (r) this.exportSvc.exportPdf(`Role — ${r.name}`, this.exportCols, [r as unknown as Record<string, unknown>]);
  }
  private exportExcel() {
    const r = this.selectedRole();
    if (r) this.exportSvc.exportExcel(`Role_${r.name}`, this.exportCols, [r as unknown as Record<string, unknown>]);
  }
  private exportCsv() {
    const r = this.selectedRole();
    if (r) this.exportSvc.exportCsv(`Role_${r.name}`, this.exportCols, [r as unknown as Record<string, unknown>]);
  }

  lookupRoleName() {
    const raw = (this.form.get('name')?.value ?? '').trim();
    if (!raw || !raw.includes('*')) return;
    const term = raw.replace(/\*/g, '').trim();
    if (!term) return;
    this.roleService.getAll().subscribe({
      next: res => {
        const exact = (res.data ?? []).find(r => r.name.toLowerCase() === term.toLowerCase());
        if (exact) {
          this.loadRole(exact);
        } else {
          this.router.navigate(['/roles/list'], { state: { searchTerm: term } });
        }
      }
    });
  }

  loadRole(role: RoleDto) {
    this.selectedRole.set(role);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.form.patchValue({ name: role.name, description: role.description ?? '' });
    this.form.markAsUntouched();
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = `/roles#${role.id}`;
    this.tabBar.openTab({
      key:   this.currentTabKey,
      label: `Edit Role - ${role.name}`,
      route: '/roles',
      icon:  'pi-shield',
      state: { roleId: role.id }
    });
    this.tabBar.registerDirtyChecker(this.currentTabKey, () => this.form.dirty);
    this.syncToolbar();
  }

  clearSelection() {
    const editTabKey = this.currentTabKey;
    this.resetFormOnly();
    if (editTabKey !== '/roles') {
      this.tabBar.switchToDefaultTab('/roles');
    }
  }

  onSubmit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    const v = this.form.value;
    const r = this.selectedRole();

    const req$ = r
      ? this.roleService.update(r.id, { name: v.name!, description: v.description || undefined })
      : this.roleService.create({ name: v.name!, description: v.description || undefined });

    req$.subscribe({
      next: () => {
        this.loading.set(false);
        this.successMessage.set(r ? 'Role updated successfully.' : 'Role created successfully.');
        if (!r) this.clearSelection();
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'An error occurred.');
      }
    });
  }

  confirmDelete() {
    const r = this.selectedRole();
    if (!r) return;
    this.confirmSvc.confirm({
      message: `Delete role <strong>${r.name}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.roleService.delete(r.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: `Role ${r.name} deleted.` });
            this.clearSelection();
          },
          error: (err) => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to delete role.' });
          }
        });
      }
    });
  }
}
