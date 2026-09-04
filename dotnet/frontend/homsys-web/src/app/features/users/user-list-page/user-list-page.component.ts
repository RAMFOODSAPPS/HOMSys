import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { map, catchError, switchMap } from 'rxjs/operators';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { ImportDialogComponent, ImportColumn } from '../../../shared/import-dialog/import-dialog.component';
import { UserService } from '../../../core/services/user.service';
import { CompanyService } from '../../../core/services/company.service';
import { DepartmentService } from '../../../core/services/department.service';
import { SiteService } from '../../../core/services/site.service';
import { UserDto, CreateUserDto } from '../../../core/models/user.model';

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Username',   field: 'username' },
  { header: 'First Name', field: 'firstName' },
  { header: 'Last Name',  field: 'lastName' },
  { header: 'Email',      field: 'email' },
  { header: 'Company',    field: 'companyName' },
  { header: 'Department', field: 'departmentName' },
  { header: 'Site',       field: 'siteName' },
  { header: 'Branch',     field: 'branchCode' },
  { header: 'Roles',      field: 'roles', formatter: v => Array.isArray(v) ? (v as string[]).join(', ') : '' },
  { header: 'Status',     field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
];

const IMPORT_COLS: ImportColumn[] = [
  { header: 'Username',   field: 'username',   required: true },
  { header: 'First Name', field: 'firstName',  required: true },
  { header: 'Last Name',  field: 'lastName',   required: true },
  { header: 'Email',      field: 'email',      required: true },
  { header: 'Password',   field: 'password',   required: true },
  { header: 'Company',    field: 'company' },
  { header: 'Department', field: 'department' },
  { header: 'Site',       field: 'site' },
  { header: 'Roles',      field: 'roles' },
];

@Component({
  selector: 'app-user-list-page',
  standalone: true,
  imports: [TableModule, ButtonModule, TagModule, IconFieldModule, InputIconModule,
    InputTextModule, TooltipModule, ConfirmDialogModule, DatePipe, ImportDialogComponent],
  template: `
    <p-confirmDialog />
    <app-import-dialog
      [(visible)]="importVisible"
      [columns]="importCols"
      entityName="Users"
      (importRows)="handleImportRows($event)" />

    <p-table
      [value]="filteredUsers()"
      [loading]="loading()"
      [paginator]="true"
      [rows]="10"
      [rowsPerPageOptions]="[10, 25, 50]"
      [(selection)]="selectedUser"
      (onRowSelect)="noSelection.set(false)"
      (onRowUnselect)="noSelection.set(true)"
      selectionMode="single"
      dataKey="id"
      responsiveLayout="scroll"
      styleClass="p-datatable-sm"
    >
      <ng-template pTemplate="header">
        <tr>
          <th pSortableColumn="username">Username <p-sortIcon field="username" /></th>
          <th pSortableColumn="firstName">Name <p-sortIcon field="firstName" /></th>
          <th pSortableColumn="email">Email <p-sortIcon field="email" /></th>
          <th pSortableColumn="companyName">Company <p-sortIcon field="companyName" /></th>
          <th pSortableColumn="departmentName">Department <p-sortIcon field="departmentName" /></th>
          <th pSortableColumn="siteName">Site <p-sortIcon field="siteName" /></th>
          <th pSortableColumn="branchCode">Branch <p-sortIcon field="branchCode" /></th>
          <th>Roles</th>
          <th pSortableColumn="isActive">Status <p-sortIcon field="isActive" /></th>
          <th pSortableColumn="createdAt">Created At <p-sortIcon field="createdAt" /></th>
          <th pSortableColumn="createdBy">Created By <p-sortIcon field="createdBy" /></th>
          <th pSortableColumn="updatedAt">Updated At <p-sortIcon field="updatedAt" /></th>
          <th pSortableColumn="updatedBy">Updated By <p-sortIcon field="updatedBy" /></th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-user>
        <tr [pSelectableRow]="user" [class.selected-row]="selectedUser?.id === user.id"
          (dblclick)="editUser(user)">
          <td>{{ user.username }}</td>
          <td>{{ user.firstName }} {{ user.lastName }}</td>
          <td>{{ user.email }}</td>
          <td>{{ user.companyName }}</td>
          <td>{{ user.departmentName }}</td>
          <td>{{ user.siteName }}</td>
          <td>{{ user.branchCode }}</td>
          <td>
            @for (role of user.roles; track role) {
              <p-tag [value]="role" severity="info" styleClass="mr-1" />
            }
          </td>
          <td>
            <p-tag [value]="user.isActive ? 'Active' : 'Inactive'"
              [severity]="user.isActive ? 'success' : 'danger'" />
          </td>
          <td>{{ user.createdAt | date:'MM/dd/yyyy hh:mm a' }}</td>
          <td>{{ user.createdBy || '—' }}</td>
          <td>{{ user.updatedAt ? (user.updatedAt | date:'MM/dd/yyyy hh:mm a') : '—' }}</td>
          <td>{{ user.updatedBy || '—' }}</td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr><td colspan="12" class="text-center">No users found.</td></tr>
      </ng-template>
    </p-table>
  `,
  styles: [`
    :host { display: block; }
    ::ng-deep .selected-row { background: rgba(128,0,0,0.06) !important; }
  `]
})
export class UserListPageComponent implements OnInit, OnDestroy {
  private userService   = inject(UserService);
  private companyService    = inject(CompanyService);
  private departmentService = inject(DepartmentService);
  private siteService       = inject(SiteService);
  private router        = inject(Router);
  private toolbar       = inject(GlobalToolbarService);
  private exportSvc     = inject(ExportService);
  private confirmSvc    = inject(ConfirmationService);
  private messageSvc    = inject(MessageService);

  protected allUsers     = signal<UserDto[]>([]);
  protected loading      = signal(true);
  protected selectedUser: UserDto | null = null;
  protected noSelection  = signal(true);
  protected deleting     = signal(false);
  protected importVisible = false;
  protected readonly importCols = IMPORT_COLS;
  private  initialSearch = (history.state as Record<string, unknown>)?.['searchTerm'] as string ?? '';
  protected searchTerm   = signal(this.initialSearch);

  protected filteredUsers = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    if (!term) return this.allUsers();
    return this.allUsers().filter(u =>
      u.username.toLowerCase().includes(term) ||
      u.firstName.toLowerCase().includes(term) ||
      u.lastName.toLowerCase().includes(term) ||
      u.email.toLowerCase().includes(term) ||
      (u.companyName ?? '').toLowerCase().includes(term) ||
      (u.departmentName ?? '').toLowerCase().includes(term) ||
      (u.siteName ?? '').toLowerCase().includes(term) ||
      (u.branchCode ?? '').toLowerCase().includes(term)
    );
  });

  ngOnInit() {
    this.loadUsers();
    this.setToolbar();
  }

  ngOnDestroy() {
    this.toolbar.clear();
  }

  private loadUsers() {
    this.loading.set(true);
    this.userService.getAll().subscribe({
      next: res => { this.allUsers.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  private setToolbar() {
    this.toolbar.set({
      title:        'Users — List',
      add:          { onClick: () => this.router.navigate(['/users']) },
      edit:         { onClick: () => this.editSelected(), disabled: this.noSelection },
      delete:       { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh:      { onClick: () => this.loadUsers(), loading: this.loading },
      search:       (term) => this.searchTerm.set(term),
      initialSearch: this.initialSearch,
      print:  { onClick: () => this.printList() },
      export: { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv() },
      import: { onClick: () => this.importVisible = true },
    });
  }

  private printList()   { window.print(); }
  private exportPdf()   { this.exportSvc.exportPdf('Users', EXPORT_COLS, this.filteredUsers() as unknown as Record<string, unknown>[]); }
  private exportExcel() { this.exportSvc.exportExcel('Users', EXPORT_COLS, this.filteredUsers() as unknown as Record<string, unknown>[]); }
  private exportCsv()   { this.exportSvc.exportCsv('Users', EXPORT_COLS, this.filteredUsers() as unknown as Record<string, unknown>[]); }

  handleImportRows(rows: Record<string, string>[]) {
    const validRows = rows.filter(r => r['username'] && r['firstName'] && r['lastName'] && r['email'] && r['password']);
    if (!validRows.length) {
      this.messageSvc.add({ severity: 'warn', summary: 'Import', detail: 'No valid rows (Username, First Name, Last Name, Email, Password are required).' });
      return;
    }

    forkJoin([
      this.companyService.getAll(),
      this.departmentService.getAll(),
      this.siteService.getAll(),
      this.userService.getRoles(),
    ]).pipe(
      switchMap(([coRes, deptRes, siteRes, roleRes]) => {
        const companies   = coRes.data   ?? [];
        const departments = deptRes.data ?? [];
        const sites       = siteRes.data ?? [];
        const allRoles    = roleRes.data  ?? [];

        const dtos: CreateUserDto[] = validRows.map(r => {
          const co   = companies.find(c => c.name.toLowerCase()   === r['company']?.toLowerCase());
          const dept = departments.find(d => d.name.toLowerCase() === r['department']?.toLowerCase());
          const site = sites.find(s => s.name.toLowerCase()       === r['site']?.toLowerCase());
          const roleNames = r['roles'] ? r['roles'].split(',').map(n => n.trim().toLowerCase()) : [];
          const roleIds = allRoles
            .filter(role => roleNames.includes(role.name.toLowerCase()))
            .map(role => role.id);

          return {
            username:     r['username'],
            firstName:    r['firstName'],
            lastName:     r['lastName'],
            email:        r['email'],
            password:     r['password'],
            companyId:    co?.id,
            departmentId: dept?.id,
            siteId:       site?.id,
            roleIds,
          };
        });

        return forkJoin(dtos.map(dto =>
          this.userService.create(dto).pipe(map(() => true), catchError(() => of(false)))
        ));
      })
    ).subscribe(results => {
      const done   = (results as boolean[]).filter(Boolean).length;
      const failed = results.length - done;
      this.messageSvc.add({
        severity: done > 0 ? 'success' : 'error',
        summary:  'Import Complete',
        detail:   `${done} imported, ${failed} failed.`
      });
      if (done > 0) this.loadUsers();
    });
  }

  editUser(user: UserDto) {
    this.router.navigate(['/users'], { state: { userId: user.id } });
  }

  private editSelected() {
    if (this.selectedUser) this.editUser(this.selectedUser);
  }

  private confirmDelete() {
    if (!this.selectedUser) return;
    this.confirmSvc.confirm({
      message: `Delete user <strong>${this.selectedUser.username}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.userService.delete(this.selectedUser!.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: `User ${this.selectedUser!.username} deleted.` });
            this.selectedUser = null;
            this.noSelection.set(true);
            this.loadUsers();
          },
          error: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete user.' });
          }
        });
      }
    });
  }
}
