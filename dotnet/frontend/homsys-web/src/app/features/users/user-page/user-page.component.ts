import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageModule } from 'primeng/message';
import { ToastModule } from 'primeng/toast';
import { DrawerModule } from 'primeng/drawer';
import { TagModule } from 'primeng/tag';
import { DividerModule } from 'primeng/divider';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { UserService } from '../../../core/services/user.service';
import { CompanyService } from '../../../core/services/company.service';
import { DepartmentService } from '../../../core/services/department.service';
import { SiteService } from '../../../core/services/site.service';
import { UserDto, RoleDto } from '../../../core/models/user.model';
import { CompanyDto } from '../../../core/models/company.model';
import { DepartmentDto } from '../../../core/models/department.model';
import { SiteDto } from '../../../core/models/site.model';

@Component({
  selector: 'app-user-page',
  standalone: true,
  providers: [MessageService, ConfirmationService],
  imports: [
    ReactiveFormsModule, InputTextModule, PasswordModule, ButtonModule,
    MultiSelectModule, SelectModule, ToggleSwitchModule, MessageModule, ToastModule,
    DrawerModule, TagModule, DividerModule, ConfirmDialogModule
  ],
  template: `
    <p-toast position="top-right" />
    <p-confirmDialog />

    <!-- Form card -->
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

      <form [formGroup]="form" (ngSubmit)="onSubmit()" autocomplete="off">
        <div class="field">
          <label>Username @if (!selectedUser()) { * }</label>
          @if (selectedUser()) {
            <input pInputText [value]="selectedUser()!.username" class="w-full" [disabled]="true" />
          } @else {
            <input pInputText formControlName="username" class="w-full" placeholder="e.g. jdelacruz" autocomplete="off"
              (keydown.enter)="$event.preventDefault(); lookupUsername()" />
          }
        </div>

        <div class="field-row">
          <div class="field">
            <label>First Name *</label>
            <input pInputText formControlName="firstName" class="w-full" />
          </div>
          <div class="field">
            <label>Last Name *</label>
            <input pInputText formControlName="lastName" class="w-full" />
          </div>
        </div>

        <div class="field">
          <label>Email *</label>
          <input pInputText formControlName="email" type="email" class="w-full" />
        </div>

        <div class="field">
          <label>Company</label>
          <p-select formControlName="companyId" [options]="companies()"
            optionLabel="name" optionValue="id" [showClear]="true"
            placeholder="Select company" styleClass="w-full"
            (onChange)="onCompanyChange()" />
        </div>

        <div class="field-row">
          <div class="field">
            <label>Department</label>
            <p-select formControlName="departmentId" [options]="filteredDepartments()"
              optionLabel="name" optionValue="id" [showClear]="true"
              placeholder="Select department" styleClass="w-full" />
          </div>
          <div class="field">
            <label>Site</label>
            <p-select formControlName="siteId" [options]="filteredSites()"
              optionLabel="name" optionValue="id" [showClear]="true"
              placeholder="Select site" styleClass="w-full" />
          </div>
        </div>

        <div class="field">
          <label>Branch Code</label>
          <input pInputText formControlName="branchCode" class="w-full"
            placeholder="e.g. luc — blank means HO / unscoped, sees all branches" />
        </div>

        <div class="field">
          <label>Roles *</label>
          <p-multiselect formControlName="roleIds" [options]="roles()"
            optionLabel="name" optionValue="id"
            placeholder="Select roles" styleClass="w-full" />
        </div>

        <div class="field">
          <label>{{ selectedUser() ? 'New Password (leave blank to keep current)' : 'Password *' }}</label>
          <p-password formControlName="password" [feedback]="true"
            [toggleMask]="true" styleClass="w-full" inputStyleClass="w-full"
            autocomplete="new-password" />
        </div>

        @if (selectedUser()) {
          <div class="field-inline">
            <label>Active</label>
            <p-toggleswitch formControlName="isActive" />
          </div>
        }

        <p-divider />

        <div class="form-actions">
          @if (selectedUser()) {
            <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="clearSelection()" />
          }
          <p-button type="submit"
            [label]="selectedUser() ? 'Save Changes' : 'Create User'"
            [loading]="loading()" [disabled]="form.invalid" />
        </div>
      </form>
    </div>

    <!-- Users drawer -->
    <p-drawer [(visible)]="drawerVisible" position="right" header="All Users"
      [style]="{ width: '340px' }">
      @if (drawerLoading()) {
        <div class="drawer-empty">Loading...</div>
      } @else if (users().length === 0) {
        <div class="drawer-empty">No users found.</div>
      } @else {
        <div class="user-list">
          @for (user of users(); track user.id) {
            <div class="user-item" [class.selected]="selectedUser()?.id === user.id"
              (click)="loadUser(user)">
              <div class="user-avatar">{{ initials(user) }}</div>
              <div class="user-info">
                <span class="user-name">{{ user.firstName }} {{ user.lastName }}</span>
                <span class="user-sub">{{ user.username }}</span>
              </div>
              <p-tag [value]="user.isActive ? 'Active' : 'Inactive'"
                [severity]="user.isActive ? 'success' : 'danger'" />
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

    .drawer-empty { text-align: center; color: var(--p-text-muted-color); padding: 2rem 0; font-size: 0.85rem; }

    .user-list { display: flex; flex-direction: column; gap: 0.4rem; }

    .user-item {
      display: flex;
      align-items: center;
      gap: 0.65rem;
      padding: 0.55rem 0.65rem;
      border-radius: 6px;
      border: 1px solid var(--p-surface-border);
      cursor: pointer;
      transition: background 0.15s;
    }

    .user-item:hover { background: var(--p-surface-hover); }
    .user-item.selected { background: rgba(128, 0, 0, 0.07); border-color: #800000; }

    .user-avatar {
      width: 30px; height: 30px; border-radius: 50%;
      background: #800000; color: #fff;
      font-size: 0.68rem; font-weight: 700;
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
    }

    .user-info { flex: 1; display: flex; flex-direction: column; min-width: 0; }
    .user-name { font-size: 0.82rem; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .user-sub  { font-size: 0.72rem; color: var(--p-text-muted-color); }
  `]
})
export class UserPageComponent implements OnInit, OnDestroy {
  private userService       = inject(UserService);
  private companyService    = inject(CompanyService);
  private departmentService = inject(DepartmentService);
  private siteService       = inject(SiteService);
  private fb                = inject(FormBuilder);
  private router            = inject(Router);
  private globalToolbar     = inject(GlobalToolbarService);
  private exportSvc         = inject(ExportService);
  private tabBar            = inject(TabBarService);
  private confirmSvc        = inject(ConfirmationService);
  private messageSvc        = inject(MessageService);

  private currentTabKey = '/users';
  private navSub?: Subscription;

  protected users        = signal<UserDto[]>([]);
  protected roles        = signal<RoleDto[]>([]);
  protected companies    = signal<CompanyDto[]>([]);
  protected departments  = signal<DepartmentDto[]>([]);
  protected sites        = signal<SiteDto[]>([]);

  protected filteredDepartments = computed(() => {
    const cid = this.form.get('companyId')?.value as number | null;
    return cid ? this.departments().filter(d => d.companyId === cid) : this.departments();
  });

  protected filteredSites = computed(() => {
    const cid = this.form.get('companyId')?.value as number | null;
    return cid ? this.sites().filter(s => s.companyId === cid) : this.sites();
  });
  protected selectedUser = signal<UserDto | undefined>(undefined);
  protected loading      = signal(false);
  protected deleting     = signal(false);
  protected refreshing   = signal(false);
  protected drawerLoading = signal(false);
  protected drawerVisible = false;
  protected errorMessage  = signal<string | null>(null);
  protected successMessage = signal<string | null>(null);
  protected noSelection   = computed(() => !this.selectedUser());

  protected form = this.fb.group({
    username:     ['', [Validators.required, Validators.minLength(3)]],
    firstName:    ['', Validators.required],
    lastName:     ['', Validators.required],
    email:        ['', [Validators.required, Validators.email]],
    companyId:    [null as number | null],
    departmentId: [null as number | null],
    siteId:       [null as number | null],
    branchCode:   [null as string | null],
    roleIds:      [[] as number[], Validators.required],
    password:     ['', [Validators.required, Validators.minLength(8)]],
    isActive:     [true]
  });

  ngOnInit() {
    this.userService.getRoles().subscribe({ next: res => this.roles.set(res.data ?? []) });
    this.companyService.getAll().subscribe({ next: res => this.companies.set(res.data ?? []) });
    this.departmentService.getAll().subscribe({ next: res => this.departments.set(res.data ?? []) });
    this.siteService.getAll().subscribe({ next: res => this.sites.set(res.data ?? []) });
    this.handleNavState();
    this.navSub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe(() => this.handleNavState());
    this.syncToolbar();
    this.tabBar.registerDirtyChecker('/users', () => this.isNewFormDirty());
  }

  ngOnDestroy() {
    if (!this.selectedUser() && this.isNewFormDirty()) {
      this.tabBar.updateTabState('/users', { draftForm: this.form.value });
    }
    this.tabBar.unregisterDirtyChecker('/users');
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.globalToolbar.clear();
    this.navSub?.unsubscribe();
  }

  private isNewFormDirty(): boolean {
    const v = this.form.value;
    return !!(v.username?.trim() || v.firstName?.trim() || v.lastName?.trim() ||
              v.email?.trim() || v.password?.trim() || v.roleIds?.length);
  }

  private handleNavState() {
    const state     = history.state as Record<string, unknown>;
    const userId    = state?.['userId']    as number | undefined;
    const draftForm = state?.['draftForm'] as Record<string, unknown> | undefined;
    if (userId) {
      if (userId !== this.selectedUser()?.id) {
        this.userService.getById(userId).subscribe({
          next: res => { if (res.data) this.loadUser(res.data); }
        });
      }
    } else {
      if (this.selectedUser()) this.resetFormOnly();
      if (draftForm) {
        this.form.patchValue(draftForm);
        this.tabBar.updateTabState('/users', undefined);
      }
    }
  }

  private resetFormOnly() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.selectedUser.set(undefined);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.form.reset({ isActive: true, roleIds: [], companyId: null, departmentId: null, siteId: null, branchCode: null });
    this.form.get('username')?.setValidators([Validators.required, Validators.minLength(3)]);
    this.form.get('password')?.setValidators([Validators.required, Validators.minLength(8)]);
    this.form.get('username')?.updateValueAndValidity();
    this.form.get('password')?.updateValueAndValidity();
    this.currentTabKey = '/users';
    this.tabBar.updateTabState('/users', undefined);
    this.tabBar.registerDirtyChecker('/users', () => this.isNewFormDirty());
    this.syncToolbar();
  }

  private refreshRecord() {
    const u = this.selectedUser();
    if (u) {
      this.refreshing.set(true);
      this.userService.getById(u.id).subscribe({
        next: res => { this.refreshing.set(false); if (res.data) this.loadUser(res.data); },
        error: () => this.refreshing.set(false)
      });
    } else {
      this.resetFormOnly();
    }
  }

  private syncToolbar() {
    const u = this.selectedUser();
    this.globalToolbar.set({
      title:   u ? `Edit User - ${u.firstName} ${u.lastName}` : 'Users',
      save:    { onClick: () => this.onSubmit(), loading: this.loading },
      edit:    { onClick: () => this.openDrawer(), disabled: this.noSelection },
      delete:  { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh: { onClick: () => this.refreshRecord(), loading: this.refreshing },
      list:    { onClick: () => this.router.navigate(['/users/list']) },
      find:    u ? undefined : { onClick: () => this.lookupUsername() },
      print:   { onClick: () => this.printRecord() },
      export:  { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv(), disabled: this.noSelection },
    });
  }

  private readonly exportCols: ExportColumn[] = [
    { header: 'Username',   field: 'username' },
    { header: 'First Name', field: 'firstName' },
    { header: 'Last Name',  field: 'lastName' },
    { header: 'Email',      field: 'email' },
    { header: 'Company',    field: 'companyName' },
    { header: 'Department', field: 'departmentName' },
    { header: 'Site',       field: 'siteName' },
    { header: 'Roles',      field: 'roles', formatter: v => Array.isArray(v) ? (v as string[]).join(', ') : '' },
    { header: 'Status',     field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
  ];

  private printRecord() { window.print(); }
  private exportPdf()   {
    const u = this.selectedUser();
    if (u) this.exportSvc.exportPdf(`User — ${u.username}`, this.exportCols, [u as unknown as Record<string, unknown>]);
  }
  private exportExcel() {
    const u = this.selectedUser();
    if (u) this.exportSvc.exportExcel(`User_${u.username}`, this.exportCols, [u as unknown as Record<string, unknown>]);
  }
  private exportCsv() {
    const u = this.selectedUser();
    if (u) this.exportSvc.exportCsv(`User_${u.username}`, this.exportCols, [u as unknown as Record<string, unknown>]);
  }

  onCompanyChange() {
    this.form.patchValue({ departmentId: null, siteId: null });
  }

  lookupUsername() {
    const raw = (this.form.get('username')?.value ?? '').trim();
    if (!raw || !raw.includes('*')) return;
    const term = raw.replace(/\*/g, '').trim();
    if (!term) return;
    this.userService.getAll().subscribe({
      next: res => {
        const exact = (res.data ?? []).find(u => u.username.toLowerCase() === term.toLowerCase());
        if (exact) {
          this.loadUser(exact);
        } else {
          this.router.navigate(['/users/list'], { state: { searchTerm: term } });
        }
      }
    });
  }

  confirmDelete() {
    const u = this.selectedUser();
    if (!u) return;
    this.confirmSvc.confirm({
      message: `Delete user <strong>${u.username}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.userService.delete(u.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: `User ${u.username} deleted.` });
            this.clearSelection();
          },
          error: (err) => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to delete user.' });
          }
        });
      }
    });
  }

  openDrawer() {
    this.drawerVisible = true;
    this.drawerLoading.set(true);
    this.userService.getAll().subscribe({
      next: res => { this.users.set(res.data ?? []); this.drawerLoading.set(false); },
      error: () => this.drawerLoading.set(false)
    });
  }

  loadUser(user: UserDto) {
    this.selectedUser.set(user);
    this.drawerVisible = false;
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.form.patchValue({
      firstName: user.firstName, lastName: user.lastName, email: user.email,
      companyId: user.companyId ?? null, departmentId: user.departmentId ?? null,
      siteId: user.siteId ?? null, branchCode: user.branchCode ?? null,
      roleIds: user.roleIds ?? [],
      isActive: user.isActive, password: ''
    });
    this.form.get('username')?.clearValidators();
    this.form.get('password')?.clearValidators();
    this.form.get('username')?.updateValueAndValidity();
    this.form.get('password')?.updateValueAndValidity();
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = `/users#${user.id}`;
    this.tabBar.openTab({
      key:   this.currentTabKey,
      label: `Edit User - ${user.firstName} ${user.lastName}`,
      route: '/users',
      icon:  'pi-user-edit',
      state: { userId: user.id }
    });
    this.tabBar.registerDirtyChecker(this.currentTabKey, () => this.form.dirty);
    this.syncToolbar();
  }

  clearSelection() {
    const editTabKey = this.currentTabKey;
    this.resetFormOnly();
    if (editTabKey !== '/users') {
      this.tabBar.switchToDefaultTab('/users');
    }
  }

  onSubmit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    const v = this.form.value;
    const u = this.selectedUser();

    const req$ = u
      ? this.userService.update(u.id, {
          email: v.email!, firstName: v.firstName!, lastName: v.lastName!,
          companyId: v.companyId ?? undefined, departmentId: v.departmentId ?? undefined,
          siteId: v.siteId ?? undefined, branchCode: v.branchCode ?? undefined,
          isActive: v.isActive!, roleIds: v.roleIds ?? [],
          newPassword: v.password || undefined
        })
      : this.userService.create({
          username: v.username!, email: v.email!,
          firstName: v.firstName!, lastName: v.lastName!,
          companyId: v.companyId ?? undefined, departmentId: v.departmentId ?? undefined,
          siteId: v.siteId ?? undefined, branchCode: v.branchCode ?? undefined,
          password: v.password!, roleIds: v.roleIds ?? []
        });

    req$.subscribe({
      next: () => {
        this.loading.set(false);
        this.successMessage.set(u ? 'User updated successfully.' : 'User created successfully.');
        if (!u) this.clearSelection();
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'An error occurred.');
      }
    });
  }

  initials(user: UserDto) {
    return `${user.firstName[0]}${user.lastName[0]}`.toUpperCase();
  }
}
