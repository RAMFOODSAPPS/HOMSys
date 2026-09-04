import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { DatePipe } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { ExportService, ExportColumn } from '../../../core/services/export.service';
import { ImportDialogComponent, ImportColumn } from '../../../shared/import-dialog/import-dialog.component';
import { RoleService } from '../../../core/services/role.service';
import { RoleDto, CreateRoleDto } from '../../../core/models/user.model';

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Name',        field: 'name' },
  { header: 'Description', field: 'description' },
];

const IMPORT_COLS: ImportColumn[] = [
  { header: 'Name',        field: 'name',        required: true },
  { header: 'Description', field: 'description' },
];

@Component({
  selector: 'app-role-list-page',
  standalone: true,
  imports: [TableModule, ButtonModule, InputTextModule, IconFieldModule,
    InputIconModule, TooltipModule, ConfirmDialogModule, ImportDialogComponent, DatePipe],
  template: `
    <p-confirmDialog />
    <app-import-dialog
      [(visible)]="importVisible"
      [columns]="importCols"
      entityName="Roles"
      (importRows)="handleImportRows($event)" />

    <p-table
      [value]="filteredRoles()"
      [loading]="loading()"
      [paginator]="true"
      [rows]="10"
      [rowsPerPageOptions]="[10, 25, 50]"
      [(selection)]="selectedRole"
      (onRowSelect)="noSelection.set(false)"
      (onRowUnselect)="noSelection.set(true)"
      selectionMode="single"
      dataKey="id"
      responsiveLayout="scroll"
      styleClass="p-datatable-sm"
    >
      <ng-template pTemplate="header">
        <tr>
          <th pSortableColumn="id" style="width:80px">ID <p-sortIcon field="id" /></th>
          <th pSortableColumn="name">Role Name <p-sortIcon field="name" /></th>
          <th>Description</th>
          <th pSortableColumn="createdAt">Created At <p-sortIcon field="createdAt" /></th>
          <th pSortableColumn="createdBy">Created By <p-sortIcon field="createdBy" /></th>
          <th pSortableColumn="updatedAt">Updated At <p-sortIcon field="updatedAt" /></th>
          <th pSortableColumn="updatedBy">Updated By <p-sortIcon field="updatedBy" /></th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-role>
        <tr [pSelectableRow]="role" [class.selected-row]="selectedRole?.id === role.id"
          (dblclick)="editRole(role)">
          <td>{{ role.id }}</td>
          <td>{{ role.name }}</td>
          <td>{{ role.description ?? '—' }}</td>
          <td>{{ role.createdAt | date:'MM/dd/yyyy hh:mm a' }}</td>
          <td>{{ role.createdBy || '—' }}</td>
          <td>{{ role.updatedAt ? (role.updatedAt | date:'MM/dd/yyyy hh:mm a') : '—' }}</td>
          <td>{{ role.updatedBy || '—' }}</td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr><td colspan="7" class="text-center">No roles found.</td></tr>
      </ng-template>
    </p-table>
  `,
  styles: [`
    :host { display: block; }
    ::ng-deep .selected-row { background: rgba(128,0,0,0.06) !important; }
  `]
})
export class RoleListPageComponent implements OnInit, OnDestroy {
  private roleService = inject(RoleService);
  private router      = inject(Router);
  private toolbar     = inject(GlobalToolbarService);
  private exportSvc   = inject(ExportService);
  private confirmSvc  = inject(ConfirmationService);
  private messageSvc  = inject(MessageService);

  private initialSearch = (history.state as Record<string, unknown>)?.['searchTerm'] as string ?? '';

  protected allRoles    = signal<RoleDto[]>([]);
  protected searchTerm  = signal(this.initialSearch);
  protected loading     = signal(true);
  protected selectedRole: RoleDto | null = null;
  protected noSelection = signal(true);
  protected deleting    = signal(false);
  protected importVisible = false;
  protected readonly importCols = IMPORT_COLS;

  protected filteredRoles = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    if (!term) return this.allRoles();
    return this.allRoles().filter(r =>
      r.name.toLowerCase().includes(term) ||
      (r.description ?? '').toLowerCase().includes(term)
    );
  });

  ngOnInit() {
    this.loadRoles();
    this.setToolbar();
  }

  ngOnDestroy() {
    this.toolbar.clear();
  }

  private loadRoles() {
    this.loading.set(true);
    this.roleService.getAll().subscribe({
      next: res => { this.allRoles.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  private setToolbar() {
    this.toolbar.set({
      title:        'Roles — List',
      add:          { onClick: () => this.router.navigate(['/roles']) },
      edit:         { onClick: () => this.editSelected(), disabled: this.noSelection },
      delete:       { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh:      { onClick: () => this.loadRoles(), loading: this.loading },
      search:       (term) => this.searchTerm.set(term),
      initialSearch: this.initialSearch,
      print:  { onClick: () => this.printList() },
      export: { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv() },
      import: { onClick: () => this.importVisible = true },
    });
  }

  private printList()   { window.print(); }
  private exportPdf()   { this.exportSvc.exportPdf('Roles', EXPORT_COLS, this.filteredRoles() as unknown as Record<string, unknown>[]); }
  private exportExcel() { this.exportSvc.exportExcel('Roles', EXPORT_COLS, this.filteredRoles() as unknown as Record<string, unknown>[]); }
  private exportCsv()   { this.exportSvc.exportCsv('Roles', EXPORT_COLS, this.filteredRoles() as unknown as Record<string, unknown>[]); }

  handleImportRows(rows: Record<string, string>[]) {
    const dtos: CreateRoleDto[] = rows
      .filter(r => r['name'])
      .map(r => ({ name: r['name'], description: r['description'] || undefined }));

    if (!dtos.length) {
      this.messageSvc.add({ severity: 'warn', summary: 'Import', detail: 'No valid rows found (Name is required).' });
      return;
    }

    forkJoin(dtos.map(dto =>
      this.roleService.create(dto).pipe(map(() => true), catchError(() => of(false)))
    )).subscribe(results => {
      const done   = results.filter(Boolean).length;
      const failed = results.length - done;
      this.messageSvc.add({
        severity: done > 0 ? 'success' : 'error',
        summary:  'Import Complete',
        detail:   `${done} imported, ${failed} failed.`
      });
      if (done > 0) this.loadRoles();
    });
  }

  editRole(role: RoleDto) {
    this.router.navigate(['/roles'], { state: { roleId: role.id } });
  }

  private editSelected() {
    if (this.selectedRole) this.editRole(this.selectedRole);
  }

  private confirmDelete() {
    if (!this.selectedRole) return;
    this.confirmSvc.confirm({
      message: `Delete role <strong>${this.selectedRole.name}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.roleService.delete(this.selectedRole!.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: `Role ${this.selectedRole!.name} deleted.` });
            this.selectedRole = null;
            this.noSelection.set(true);
            this.loadRoles();
          },
          error: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete role.' });
          }
        });
      }
    });
  }
}
