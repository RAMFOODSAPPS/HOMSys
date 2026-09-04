import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
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
import { CompanyService } from '../../../core/services/company.service';
import { CompanyDto, CreateCompanyDto } from '../../../core/models/company.model';

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Name',           field: 'name' },
  { header: 'Code',           field: 'code' },
  { header: 'Contact Person', field: 'contactPerson' },
  { header: 'Email',          field: 'email' },
  { header: 'Phone',          field: 'phone' },
  { header: 'Address',        field: 'address' },
  { header: 'Status',         field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
];

const IMPORT_COLS: ImportColumn[] = [
  { header: 'Name',           field: 'name',          required: true },
  { header: 'Code',           field: 'code' },
  { header: 'Contact Person', field: 'contactPerson' },
  { header: 'Email',          field: 'email' },
  { header: 'Phone',          field: 'phone' },
  { header: 'Address',        field: 'address' },
];

@Component({
  selector: 'app-company-list-page',
  standalone: true,
  imports: [TableModule, ButtonModule, TagModule, IconFieldModule, InputIconModule,
    InputTextModule, TooltipModule, ConfirmDialogModule, DatePipe, ImportDialogComponent],
  template: `
    <p-confirmDialog />
    <app-import-dialog
      [(visible)]="importVisible"
      [columns]="importCols"
      entityName="Companies"
      (importRows)="handleImportRows($event)" />

    <p-table
      [value]="filteredCompanies()"
      [loading]="loading()"
      [paginator]="true"
      [rows]="10"
      [rowsPerPageOptions]="[10, 25, 50]"
      [(selection)]="selectedCompany"
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
          <th pSortableColumn="contactPerson">Contact Person <p-sortIcon field="contactPerson" /></th>
          <th pSortableColumn="email">Email <p-sortIcon field="email" /></th>
          <th pSortableColumn="phone">Phone <p-sortIcon field="phone" /></th>
          <th pSortableColumn="isActive">Status <p-sortIcon field="isActive" /></th>
          <th pSortableColumn="createdAt">Created At <p-sortIcon field="createdAt" /></th>
          <th pSortableColumn="createdBy">Created By <p-sortIcon field="createdBy" /></th>
          <th pSortableColumn="updatedAt">Updated At <p-sortIcon field="updatedAt" /></th>
          <th pSortableColumn="updatedBy">Updated By <p-sortIcon field="updatedBy" /></th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-company>
        <tr [pSelectableRow]="company" [class.selected-row]="selectedCompany?.id === company.id"
          (dblclick)="editCompany(company)">
          <td>{{ company.name }}</td>
          <td>{{ company.code || '—' }}</td>
          <td>{{ company.contactPerson || '—' }}</td>
          <td>{{ company.email || '—' }}</td>
          <td>{{ company.phone || '—' }}</td>
          <td>
            <p-tag [value]="company.isActive ? 'Active' : 'Inactive'"
              [severity]="company.isActive ? 'success' : 'danger'" />
          </td>
          <td>{{ company.createdAt | date:'MM/dd/yyyy hh:mm a' }}</td>
          <td>{{ company.createdBy || '—' }}</td>
          <td>{{ company.updatedAt ? (company.updatedAt | date:'MM/dd/yyyy hh:mm a') : '—' }}</td>
          <td>{{ company.updatedBy || '—' }}</td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr><td colspan="10" class="text-center">No companies found.</td></tr>
      </ng-template>
    </p-table>
  `,
  styles: [`
    :host { display: block; }
    ::ng-deep .selected-row { background: rgba(128,0,0,0.06) !important; }
  `]
})
export class CompanyListPageComponent implements OnInit, OnDestroy {
  private companyService = inject(CompanyService);
  private router         = inject(Router);
  private toolbar        = inject(GlobalToolbarService);
  private exportSvc      = inject(ExportService);
  private confirmSvc     = inject(ConfirmationService);
  private messageSvc     = inject(MessageService);

  private initialSearch = (history.state as Record<string, unknown>)?.['searchTerm'] as string ?? '';

  protected allCompanies    = signal<CompanyDto[]>([]);
  protected searchTerm      = signal(this.initialSearch);
  protected loading         = signal(true);
  protected selectedCompany: CompanyDto | null = null;
  protected noSelection     = signal(true);
  protected deleting        = signal(false);
  protected importVisible   = false;
  protected readonly importCols = IMPORT_COLS;

  protected filteredCompanies = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    if (!term) return this.allCompanies();
    return this.allCompanies().filter(c =>
      c.name.toLowerCase().includes(term) ||
      c.code.toLowerCase().includes(term) ||
      c.contactPerson.toLowerCase().includes(term) ||
      c.email.toLowerCase().includes(term) ||
      c.phone.toLowerCase().includes(term)
    );
  });

  ngOnInit() {
    this.loadCompanies();
    this.setToolbar();
  }

  ngOnDestroy() {
    this.toolbar.clear();
  }

  private loadCompanies() {
    this.loading.set(true);
    this.companyService.getAll().subscribe({
      next: res => { this.allCompanies.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  private setToolbar() {
    this.toolbar.set({
      title:        'Companies — List',
      add:          { onClick: () => this.router.navigate(['/companies']) },
      edit:         { onClick: () => this.editSelected(), disabled: this.noSelection },
      delete:       { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh:      { onClick: () => this.loadCompanies(), loading: this.loading },
      search:       (term) => this.searchTerm.set(term),
      initialSearch: this.initialSearch,
      print:  { onClick: () => this.printList() },
      export: { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv() },
      import: { onClick: () => this.importVisible = true },
    });
  }

  private printList()   { window.print(); }
  private exportPdf()   { this.exportSvc.exportPdf('Companies', EXPORT_COLS, this.filteredCompanies() as unknown as Record<string, unknown>[]); }
  private exportExcel() { this.exportSvc.exportExcel('Companies', EXPORT_COLS, this.filteredCompanies() as unknown as Record<string, unknown>[]); }
  private exportCsv()   { this.exportSvc.exportCsv('Companies', EXPORT_COLS, this.filteredCompanies() as unknown as Record<string, unknown>[]); }

  handleImportRows(rows: Record<string, string>[]) {
    const dtos: CreateCompanyDto[] = rows
      .filter(r => r['name'])
      .map(r => ({
        name:          r['name'],
        code:          r['code']          || '',
        contactPerson: r['contactPerson'] || '',
        email:         r['email']         || '',
        phone:         r['phone']         || '',
        address:       r['address']       || '',
      }));

    if (!dtos.length) {
      this.messageSvc.add({ severity: 'warn', summary: 'Import', detail: 'No valid rows found (Name is required).' });
      return;
    }

    forkJoin(dtos.map(dto =>
      this.companyService.create(dto).pipe(map(() => true), catchError(() => of(false)))
    )).subscribe(results => {
      const done   = results.filter(Boolean).length;
      const failed = results.length - done;
      this.messageSvc.add({
        severity: done > 0 ? 'success' : 'error',
        summary:  'Import Complete',
        detail:   `${done} imported, ${failed} failed.`
      });
      if (done > 0) this.loadCompanies();
    });
  }

  editCompany(company: CompanyDto) {
    this.router.navigate(['/companies'], { state: { companyId: company.id } });
  }

  private editSelected() {
    if (this.selectedCompany) this.editCompany(this.selectedCompany);
  }

  private confirmDelete() {
    if (!this.selectedCompany) return;
    this.confirmSvc.confirm({
      message: `Delete company <strong>${this.selectedCompany.name}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.companyService.delete(this.selectedCompany!.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: `Company ${this.selectedCompany!.name} deleted.` });
            this.selectedCompany = null;
            this.noSelection.set(true);
            this.loadCompanies();
          },
          error: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete company.' });
          }
        });
      }
    });
  }
}
