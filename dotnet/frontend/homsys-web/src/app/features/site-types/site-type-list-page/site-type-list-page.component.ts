import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
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
import { SiteTypeService } from '../../../core/services/site-type.service';
import { SiteTypeDto } from '../../../core/models/site-type.model';

const EXPORT_COLS: ExportColumn[] = [
  { header: 'Name',        field: 'name' },
  { header: 'Code',        field: 'code' },
  { header: 'Description', field: 'description' },
  { header: 'Status',      field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
];

@Component({
  selector: 'app-site-type-list-page',
  standalone: true,
  providers: [ConfirmationService, MessageService],
  imports: [TableModule, ButtonModule, TagModule, IconFieldModule, InputIconModule,
    InputTextModule, TooltipModule, ConfirmDialogModule, DatePipe],
  template: `
    <p-confirmDialog />

    <p-table
      [value]="filteredSiteTypes()"
      [loading]="loading()"
      [paginator]="true"
      [rows]="10"
      [rowsPerPageOptions]="[10, 25, 50]"
      [(selection)]="selectedSiteType"
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
          <th pSortableColumn="description">Description <p-sortIcon field="description" /></th>
          <th pSortableColumn="isActive">Status <p-sortIcon field="isActive" /></th>
          <th pSortableColumn="createdAt">Created At <p-sortIcon field="createdAt" /></th>
          <th pSortableColumn="createdBy">Created By <p-sortIcon field="createdBy" /></th>
          <th pSortableColumn="updatedAt">Updated At <p-sortIcon field="updatedAt" /></th>
          <th pSortableColumn="updatedBy">Updated By <p-sortIcon field="updatedBy" /></th>
        </tr>
      </ng-template>

      <ng-template pTemplate="body" let-st>
        <tr [pSelectableRow]="st" [class.selected-row]="selectedSiteType?.id === st.id"
          (dblclick)="editSiteType(st)">
          <td>{{ st.name }}</td>
          <td>{{ st.code || '—' }}</td>
          <td>{{ st.description || '—' }}</td>
          <td>
            <p-tag [value]="st.isActive ? 'Active' : 'Inactive'"
              [severity]="st.isActive ? 'success' : 'danger'" />
          </td>
          <td>{{ st.createdAt | date:'MM/dd/yyyy hh:mm a' }}</td>
          <td>{{ st.createdBy || '—' }}</td>
          <td>{{ st.updatedAt ? (st.updatedAt | date:'MM/dd/yyyy hh:mm a') : '—' }}</td>
          <td>{{ st.updatedBy || '—' }}</td>
        </tr>
      </ng-template>

      <ng-template pTemplate="emptymessage">
        <tr><td colspan="8" class="text-center">No site types found.</td></tr>
      </ng-template>
    </p-table>
  `,
  styles: [`
    :host { display: block; }
    ::ng-deep .selected-row { background: rgba(128,0,0,0.06) !important; }
  `]
})
export class SiteTypeListPageComponent implements OnInit, OnDestroy {
  private siteTypeService = inject(SiteTypeService);
  private router          = inject(Router);
  private toolbar         = inject(GlobalToolbarService);
  private exportSvc       = inject(ExportService);
  private confirmSvc      = inject(ConfirmationService);
  private messageSvc      = inject(MessageService);

  private initialSearch = (history.state as Record<string, unknown>)?.['searchTerm'] as string ?? '';

  protected allSiteTypes    = signal<SiteTypeDto[]>([]);
  protected searchTerm      = signal(this.initialSearch);
  protected loading         = signal(true);
  protected selectedSiteType: SiteTypeDto | null = null;
  protected noSelection     = signal(true);
  protected deleting        = signal(false);

  protected filteredSiteTypes = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    if (!term) return this.allSiteTypes();
    return this.allSiteTypes().filter(st =>
      st.name.toLowerCase().includes(term) ||
      st.code.toLowerCase().includes(term) ||
      (st.description ?? '').toLowerCase().includes(term)
    );
  });

  ngOnInit() {
    this.loadSiteTypes();
    this.setToolbar();
  }

  ngOnDestroy() {
    this.toolbar.clear();
  }

  private loadSiteTypes() {
    this.loading.set(true);
    this.siteTypeService.getAll().subscribe({
      next: res => { this.allSiteTypes.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  private setToolbar() {
    this.toolbar.set({
      title:         'Site Types — List',
      add:           { onClick: () => this.router.navigate(['/site-types']) },
      edit:          { onClick: () => this.editSelected(), disabled: this.noSelection },
      delete:        { onClick: () => this.confirmDelete(), disabled: this.noSelection, loading: this.deleting },
      refresh:       { onClick: () => this.loadSiteTypes(), loading: this.loading },
      search:        (term) => this.searchTerm.set(term),
      initialSearch: this.initialSearch,
      print:         { onClick: () => window.print() },
      export:        { pdf: () => this.exportPdf(), excel: () => this.exportExcel(), csv: () => this.exportCsv() },
    });
  }

  private exportPdf()   { this.exportSvc.exportPdf('Site Types', EXPORT_COLS, this.filteredSiteTypes() as unknown as Record<string, unknown>[]); }
  private exportExcel() { this.exportSvc.exportExcel('Site Types', EXPORT_COLS, this.filteredSiteTypes() as unknown as Record<string, unknown>[]); }
  private exportCsv()   { this.exportSvc.exportCsv('Site Types', EXPORT_COLS, this.filteredSiteTypes() as unknown as Record<string, unknown>[]); }

  editSiteType(st: SiteTypeDto) {
    this.router.navigate(['/site-types'], { state: { siteTypeId: st.id } });
  }

  private editSelected() {
    if (this.selectedSiteType) this.editSiteType(this.selectedSiteType);
  }

  private confirmDelete() {
    if (!this.selectedSiteType) return;
    this.confirmSvc.confirm({
      message: `Delete site type <strong>${this.selectedSiteType.name}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.siteTypeService.delete(this.selectedSiteType!.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: `Site type ${this.selectedSiteType!.name} deleted.` });
            this.selectedSiteType = null;
            this.noSelection.set(true);
            this.loadSiteTypes();
          },
          error: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete site type.' });
          }
        });
      }
    });
  }
}
