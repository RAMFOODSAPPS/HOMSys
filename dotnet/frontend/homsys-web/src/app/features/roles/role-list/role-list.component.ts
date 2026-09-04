import { Component, inject, OnInit, signal, ViewChild } from '@angular/core';
import { Table, TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { TagModule } from 'primeng/tag';
import { RoleService } from '../../../core/services/role.service';
import { RoleDto } from '../../../core/models/user.model';

@Component({
  selector: 'app-role-list',
  standalone: true,
  imports: [TableModule, ButtonModule, InputTextModule, IconFieldModule, InputIconModule, TagModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h2 class="card-title">Role Management</h2>
      </div>

      <p-table
        #dt
        [value]="roles()"
        [loading]="loading()"
        [paginator]="true"
        [rows]="10"
        [rowsPerPageOptions]="[10, 25, 50]"
        [globalFilterFields]="['name', 'description']"
        dataKey="id"
        responsiveLayout="scroll"
      >
        <ng-template pTemplate="caption">
          <div class="table-toolbar">
            <p-iconfield>
              <p-inputicon class="pi pi-search" />
              <input pInputText type="text"
                (input)="dt.filterGlobal($any($event.target).value, 'contains')"
                placeholder="Search roles..." />
            </p-iconfield>
          </div>
        </ng-template>

        <ng-template pTemplate="header">
          <tr>
            <th pSortableColumn="id" style="width:80px">ID <p-sortIcon field="id" /></th>
            <th pSortableColumn="name">Role Name <p-sortIcon field="name" /></th>
            <th>Description</th>
          </tr>
        </ng-template>

        <ng-template pTemplate="body" let-role>
          <tr>
            <td>{{ role.id }}</td>
            <td><p-tag [value]="role.name" severity="info" /></td>
            <td>{{ role.description ?? '—' }}</td>
          </tr>
        </ng-template>

        <ng-template pTemplate="emptymessage">
          <tr><td colspan="3" class="text-center">No roles found.</td></tr>
        </ng-template>
      </p-table>
    </div>
  `,
  styles: [`
    .card { background: var(--p-surface-card); border-radius: 8px; padding: 1.25rem; }
    .card-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1rem; }
    .card-title { font-size: 1.25rem; font-weight: 600; margin: 0; }
    .table-toolbar { display: flex; justify-content: flex-end; }
  `]
})
export class RoleListComponent implements OnInit {
  private roleService = inject(RoleService);

  @ViewChild('dt') dt!: Table;

  protected roles = signal<RoleDto[]>([]);
  protected loading = signal(true);

  ngOnInit() { this.loadRoles(); }

  private loadRoles() {
    this.loading.set(true);
    this.roleService.getAll().subscribe({
      next: res => { this.roles.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }
}
