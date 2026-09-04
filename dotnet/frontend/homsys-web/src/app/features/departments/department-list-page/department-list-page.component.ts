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
import { DepartmentService } from '../../../core/services/department.service';
import { CompanyService } from '../../../core/services/company.service';
import { DepartmentDto, CreateDepartmentDto } from '../../../core/models/department.model';

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Name',        field: 'name' },
  { header: 'Code',        field: 'code' },
  { header: 'Company',     field: 'companyName' },
  { header: 'Description', field: 'description' },
  { header: 'Status',      field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
];

const IMPORT_COLS: ImportColumn[] = [
  { header: 'Name',        field: 'name',        required: true },
  { header: 'Code',        field: 'code' },
  { header: 'Company',     field: 'company',     required: true },
  { header: 'Description', field: 'description' },
];

@Component({
  selector: 'app-department-list-page',
  standalone: true,
  imports: [TableModule, ButtonModule, TagModule, IconFieldModule, InputIconModule,
    InputTextModule, TooltipModule, ConfirmDialogModule, DatePipe, ImportDialogComponent],
  template: `
    <p-confirmDialog />
    <app-import-dialog
      [(visible)]="importVisible"
      [columns]="importCols"
      entityName="Departments"
      (importRows)="handleImportRows($event)" />

    <p-table
      [value]="filteredDepartments()"
      [loading]="loading()"
      [paginator]="true"
      [rows]="10"
      [rowsPerPageOptions]="[10, 25, 50]"
      [(selection)]="selectedDepartment"
      (onRowSelect)="noSelection.set(false)"
      (onRowUnselect)="noSelection.set(true)"
      selectionMode="single"
      dataKey="id"
      responsiveLayout="scroll"
      styleClass="p-datatable-sm"
    >
      <ng-template pTemplate="header">
        <tr>
          <th pSortableColumn="name">Name <p-sortIcon field="name" /></th>
          <th pSortableColumn="code">Code <p-sortIcon field="code" /></th>
          <th pSortableColumn="companyName">Company <p-sortIcon field="companyName" /></th>
          <th pSortableColumn="description">Description <p-sortIcon field="description" /></th>
          <th pSortableColumn="isActive">Status <p-sortIcon field="isActive" /></th>
          <th pSortableColumn="createdAt">Created At <p-sortIcon field="createdAt" /></th>
          <th pSortableColumn="createdBy">Created By <p-sortIcon field="createdBy" /></th>
          <th pSortableColumn="updatedAt">Updated At <p-sortIcon field="updatedAt" /></th>
          <th pSortableColumn="updatedBy">Updated By <p-sortIcon field="updatedBy" /></th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-dept>
        <tr [pSelectableRow]="dept" [class.selected-row]="selectedDepartment?.id === dept.id"
          (dblclick)="editDepartment(dept)">
          <td>{{ dept.name }}</td>
          <td>{{ dept.code || '—' }}</td>
          <td>{{ dept.companyName }}</td>
          <td>{{ dept.description || '—' }}</td>
          <td>
            <p-tag [value]="dept.isActive ? 'Active' : 'Inactive'"
              [severity]="dept.isActive ? 'success' : 'danger'" />
          </td>
          <td>{{ dept.createdAt | date:'MM/dd/yyyy hh:mm a' }}</td>
          <td>{{ dept.createdBy || '—' }}</td>
          <td>{{ dept.updatedAt ? (dept.updatedAt | date:'MM/dd/yyyy hh:mm a') : '—' }}</td>
          <td>{{ dept.updatedBy || '—' }}</td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr><td colspan="9" class="text-center">No departments found.</td></tr>
      </ng-template>
    </p-table>
  `,
  styles: [`
    :host { display: block; }
    ::ng-deep .selected-row { background: rgba(128,0,0,0.06) !important; }
  `]
})
export class DepartmentListPageComponent implements OnInit, OnDestroy {
  private deptService    = inject(DepartmentService);
  private companyService = inject(CompanyService);
  private router         = inject(Router);
  private toolbar        = inject(GlobalToolbarService);
  private exportSvc      = inject(ExportService);
  private confirmSvc     = inject(ConfirmationService);
  private messageSvc     = inject(MessageService);

  private initialSearch = (history.state as Record<string, unknown>)?.['searchTerm'] as string ?? '';

  protected allDepartments  = signal<DepartmentDto[]>([]);
  protected searchTerm      = signal(this.initialSearch);
  protected loading         = signal(true);
  protected selectedDepartment: DepartmentDto | null = null;
  protected noSelection     = signal(true);
  protected deleting        = signal(false);
  protected importVisible   = false;
  protected readonly importCols = IMPORT_COLS;

  protected filteredDepartments = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    if (!term) return this.allDepartments();
    return this.allDepartments().filter(d =>
      d.name.toLowerCase().includes(term) ||
      d.code.toLowerCase().includes(term) ||
      d.companyName.toLowerCase().includes(term) ||
      (d.description ?? '').toLowerCase().includes(term)
    );
  });

  ngOnInit() {
    this.loadDepartments();
    this.setToolbar();
  }

  ngOnDestroy() {
    this.toolbar.clear();
  }

  private loadDepartments() {
    this.loading.set(true);
    this.deptService.getAll().subscribe({
      next: res => { this.allDepartments.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  private setToolbar() {
    this.toolbar.set({
      title:        'Departments — List',
      add:          { onClick: () => this.router.navigate(['/departments']) },
      edit:         { onClick: () => this.editSelected(), disabled: this.noSelection },
      delete:       { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh:      { onClick: () => this.loadDepartments(), loading: this.loading },
      search:       (term) => this.searchTerm.set(term),
      initialSearch: this.initialSearch,
      print:  { onClick: () => this.printList() },
      export: { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv() },
      import: { onClick: () => this.importVisible = true },
    });
  }

  private printList()   { window.print(); }
  private exportPdf()   { this.exportSvc.exportPdf('Departments', EXPORT_COLS, this.filteredDepartments() as unknown as Record<string, unknown>[]); }
  private exportExcel() { this.exportSvc.exportExcel('Departments', EXPORT_COLS, this.filteredDepartments() as unknown as Record<string, unknown>[]); }
  private exportCsv()   { this.exportSvc.exportCsv('Departments', EXPORT_COLS, this.filteredDepartments() as unknown as Record<string, unknown>[]); }

  handleImportRows(rows: Record<string, string>[]) {
    const validRows = rows.filter(r => r['name'] && r['company']);
    if (!validRows.length) {
      this.messageSvc.add({ severity: 'warn', summary: 'Import', detail: 'No valid rows found (Name and Company are required).' });
      return;
    }

    this.companyService.getAll().pipe(
      switchMap(res => {
        const companies = res.data ?? [];
        const dtos: CreateDepartmentDto[] = validRows
          .map(r => {
            const co = companies.find(c => c.name.toLowerCase() === r['company'].toLowerCase());
            if (!co) return null;
            return {
              name:        r['name'],
              code:        r['code']        || '',
              description: r['description'] || '',
              companyId:   co.id,
            };
          })
          .filter((d): d is CreateDepartmentDto => d !== null);

        const skipped = validRows.length - dtos.length;
        if (!dtos.length) {
          this.messageSvc.add({ severity: 'warn', summary: 'Import', detail: 'No rows could be matched to an existing company.' });
          return of([] as boolean[]);
        }
        if (skipped > 0) {
          this.messageSvc.add({ severity: 'warn', summary: 'Import', detail: `${skipped} row(s) skipped — company not found.` });
        }
        return forkJoin(dtos.map(dto =>
          this.deptService.create(dto).pipe(map(() => true), catchError(() => of(false)))
        ));
      })
    ).subscribe(results => {
      const done   = (results as boolean[]).filter(Boolean).length;
      const failed = results.length - done;
      if (done > 0 || failed > 0) {
        this.messageSvc.add({
          severity: done > 0 ? 'success' : 'error',
          summary:  'Import Complete',
          detail:   `${done} imported, ${failed} failed.`
        });
      }
      if (done > 0) this.loadDepartments();
    });
  }

  editDepartment(dept: DepartmentDto) {
    this.router.navigate(['/departments'], { state: { departmentId: dept.id } });
  }

  private editSelected() {
    if (this.selectedDepartment) this.editDepartment(this.selectedDepartment);
  }

  private confirmDelete() {
    if (!this.selectedDepartment) return;
    this.confirmSvc.confirm({
      message: `Delete department <strong>${this.selectedDepartment.name}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.deptService.delete(this.selectedDepartment!.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: `Department ${this.selectedDepartment!.name} deleted.` });
            this.selectedDepartment = null;
            this.noSelection.set(true);
            this.loadDepartments();
          },
          error: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete department.' });
          }
        });
      }
    });
  }
}
