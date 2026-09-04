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
import { SiteService } from '../../../core/services/site.service';
import { CompanyService } from '../../../core/services/company.service';
import { SiteDto, CreateSiteDto } from '../../../core/models/site.model';

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Name',           field: 'name' },
  { header: 'Code',           field: 'code' },
  { header: 'Company',        field: 'companyName' },
  { header: 'Contact Person', field: 'contactPerson' },
  { header: 'Phone',          field: 'phone' },
  { header: 'Address',        field: 'address' },
  { header: 'Description',    field: 'description' },
  { header: 'Status',         field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
];

const IMPORT_COLS: ImportColumn[] = [
  { header: 'Name',           field: 'name',          required: true },
  { header: 'Code',           field: 'code',          required: true },
  { header: 'Company',        field: 'company',       required: true },
  { header: 'Contact Person', field: 'contactPerson' },
  { header: 'Phone',          field: 'phone' },
  { header: 'Address',        field: 'address' },
  { header: 'Description',    field: 'description' },
];

@Component({
  selector: 'app-site-list-page',
  standalone: true,
  imports: [TableModule, ButtonModule, TagModule, IconFieldModule, InputIconModule,
    InputTextModule, TooltipModule, ConfirmDialogModule, DatePipe, ImportDialogComponent],
  template: `
    <p-confirmDialog />
    <app-import-dialog
      [(visible)]="importVisible"
      [columns]="importCols"
      entityName="Sites"
      (importRows)="handleImportRows($event)" />

    <p-table
      [value]="filteredSites()"
      [loading]="loading()"
      [paginator]="true"
      [rows]="10"
      [rowsPerPageOptions]="[10, 25, 50]"
      [(selection)]="selectedSite"
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
          <th pSortableColumn="address">Address <p-sortIcon field="address" /></th>
          <th pSortableColumn="contactPerson">Contact <p-sortIcon field="contactPerson" /></th>
          <th pSortableColumn="isActive">Status <p-sortIcon field="isActive" /></th>
          <th pSortableColumn="createdAt">Created At <p-sortIcon field="createdAt" /></th>
          <th pSortableColumn="createdBy">Created By <p-sortIcon field="createdBy" /></th>
          <th pSortableColumn="updatedAt">Updated At <p-sortIcon field="updatedAt" /></th>
          <th pSortableColumn="updatedBy">Updated By <p-sortIcon field="updatedBy" /></th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-site>
        <tr [pSelectableRow]="site" [class.selected-row]="selectedSite?.id === site.id"
          (dblclick)="editSite(site)">
          <td>{{ site.name }}</td>
          <td>{{ site.code || '—' }}</td>
          <td>{{ site.companyName }}</td>
          <td>{{ site.address || '—' }}</td>
          <td>{{ site.contactPerson || '—' }}</td>
          <td>
            <p-tag [value]="site.isActive ? 'Active' : 'Inactive'"
              [severity]="site.isActive ? 'success' : 'danger'" />
          </td>
          <td>{{ site.createdAt | date:'MM/dd/yyyy hh:mm a' }}</td>
          <td>{{ site.createdBy || '—' }}</td>
          <td>{{ site.updatedAt ? (site.updatedAt | date:'MM/dd/yyyy hh:mm a') : '—' }}</td>
          <td>{{ site.updatedBy || '—' }}</td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr><td colspan="10" class="text-center">No sites found.</td></tr>
      </ng-template>
    </p-table>
  `,
  styles: [`
    :host { display: block; }
    ::ng-deep .selected-row { background: rgba(128,0,0,0.06) !important; }
  `]
})
export class SiteListPageComponent implements OnInit, OnDestroy {
  private siteService    = inject(SiteService);
  private companyService = inject(CompanyService);
  private router         = inject(Router);
  private toolbar        = inject(GlobalToolbarService);
  private exportSvc      = inject(ExportService);
  private confirmSvc     = inject(ConfirmationService);
  private messageSvc     = inject(MessageService);

  private initialSearch = (history.state as Record<string, unknown>)?.['searchTerm'] as string ?? '';

  protected allSites    = signal<SiteDto[]>([]);
  protected searchTerm  = signal(this.initialSearch);
  protected loading     = signal(true);
  protected selectedSite: SiteDto | null = null;
  protected noSelection = signal(true);
  protected deleting    = signal(false);
  protected importVisible = false;
  protected readonly importCols = IMPORT_COLS;

  protected filteredSites = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    if (!term) return this.allSites();
    return this.allSites().filter(s =>
      s.name.toLowerCase().includes(term) ||
      s.code.toLowerCase().includes(term) ||
      s.companyName.toLowerCase().includes(term) ||
      (s.address ?? '').toLowerCase().includes(term) ||
      (s.contactPerson ?? '').toLowerCase().includes(term)
    );
  });

  ngOnInit() {
    this.loadSites();
    this.setToolbar();
  }

  ngOnDestroy() {
    this.toolbar.clear();
  }

  private loadSites() {
    this.loading.set(true);
    this.siteService.getAll().subscribe({
      next: res => { this.allSites.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  private setToolbar() {
    this.toolbar.set({
      title:         'Sites — List',
      add:           { onClick: () => this.router.navigate(['/sites']) },
      edit:          { onClick: () => this.editSelected(), disabled: this.noSelection },
      delete:        { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh:       { onClick: () => this.loadSites(), loading: this.loading },
      search:        (term) => this.searchTerm.set(term),
      initialSearch: this.initialSearch,
      print:  { onClick: () => this.printList() },
      export: { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv() },
      import: { onClick: () => this.importVisible = true },
    });
  }

  private printList()   { window.print(); }
  private exportPdf()   { this.exportSvc.exportPdf('Sites', EXPORT_COLS, this.filteredSites() as unknown as Record<string, unknown>[]); }
  private exportExcel() { this.exportSvc.exportExcel('Sites', EXPORT_COLS, this.filteredSites() as unknown as Record<string, unknown>[]); }
  private exportCsv()   { this.exportSvc.exportCsv('Sites', EXPORT_COLS, this.filteredSites() as unknown as Record<string, unknown>[]); }

  handleImportRows(rows: Record<string, string>[]) {
    const validRows = rows.filter(r => r['name'] && r['code'] && r['company']);
    if (!validRows.length) {
      this.messageSvc.add({ severity: 'warn', summary: 'Import', detail: 'No valid rows found (Name, Code, and Company are required).' });
      return;
    }

    this.companyService.getAll().pipe(
      switchMap(res => {
        const companies = res.data ?? [];
        const dtos: CreateSiteDto[] = validRows
          .map(r => {
            const co = companies.find(c => c.name.toLowerCase() === r['company'].toLowerCase());
            if (!co) return null;
            return {
              name:          r['name'],
              code:          r['code'],
              companyId:     co.id,
              contactPerson: r['contactPerson'] || '',
              phone:         r['phone']         || '',
              address:       r['address']       || '',
              description:   r['description']   || '',
            };
          })
          .filter((d): d is CreateSiteDto => d !== null);

        const skipped = validRows.length - dtos.length;
        if (!dtos.length) {
          this.messageSvc.add({ severity: 'warn', summary: 'Import', detail: 'No rows could be matched to an existing company.' });
          return of([] as boolean[]);
        }
        if (skipped > 0) {
          this.messageSvc.add({ severity: 'warn', summary: 'Import', detail: `${skipped} row(s) skipped — company not found.` });
        }
        return forkJoin(dtos.map(dto =>
          this.siteService.create(dto).pipe(map(() => true), catchError(() => of(false)))
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
      if (done > 0) this.loadSites();
    });
  }

  editSite(site: SiteDto) {
    this.router.navigate(['/sites'], { state: { siteId: site.id } });
  }

  private editSelected() {
    if (this.selectedSite) this.editSite(this.selectedSite);
  }

  private confirmDelete() {
    if (!this.selectedSite) return;
    this.confirmSvc.confirm({
      message: `Delete site <strong>${this.selectedSite.name}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.siteService.delete(this.selectedSite!.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: `Site ${this.selectedSite!.name} deleted.` });
            this.selectedSite = null;
            this.noSelection.set(true);
            this.loadSites();
          },
          error: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete site.' });
          }
        });
      }
    });
  }
}
