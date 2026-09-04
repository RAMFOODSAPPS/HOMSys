import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { PermissionService, PermissionDto } from '../../core/services/permission.service';
import { RoleService } from '../../core/services/role.service';
import { MessageService } from 'primeng/api';
import { TableModule } from 'primeng/table';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';

interface RoleRow {
  id: number;
  name: string;
  description?: string;
  permissionIds: Set<number>;
  dirty: boolean;
  saving: boolean;
}

@Component({
  selector: 'app-authorization',
  standalone: true,
  imports: [TableModule, CheckboxModule, ButtonModule, TagModule, TooltipModule, FormsModule],
  template: `
    <div class="auth-card">
      <div class="auth-header">
        <div>
          <h2 class="auth-title">Authorization Management</h2>
          <p class="auth-subtitle">Configure which pages each role can access.</p>
        </div>
      </div>

      @if (loading()) {
        <div class="loading-state">Loading...</div>
      } @else {
        <div class="matrix-wrapper">
          <table class="matrix-table">
            <thead>
              <tr>
                <th class="role-col">Role</th>
                @for (perm of permissions(); track perm.id) {
                  <th class="perm-col" [pTooltip]="perm.description ?? ''" tooltipPosition="top">
                    {{ perm.name }}
                  </th>
                }
                <th class="action-col">Action</th>
              </tr>
            </thead>
            <tbody>
              @for (role of roles(); track role.id) {
                <tr [class.dirty-row]="role.dirty">
                  <td class="role-name-cell">
                    <span class="role-name">{{ role.name }}</span>
                    @if (role.description) {
                      <span class="role-desc">{{ role.description }}</span>
                    }
                  </td>
                  @for (perm of permissions(); track perm.id) {
                    <td class="check-cell">
                      <p-checkbox
                        [ngModel]="role.permissionIds.has(perm.id)"
                        (ngModelChange)="toggle(role, perm.id, $event)"
                        [binary]="true"
                      />
                    </td>
                  }
                  <td class="action-cell">
                    <p-button
                      label="Save"
                      size="small"
                      [loading]="role.saving"
                      [disabled]="!role.dirty"
                      (onClick)="save(role)"
                    />
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .auth-card {
      background: var(--p-surface-card);
      border-radius: 8px;
      padding: 1.25rem;
    }

    .auth-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      margin-bottom: 1.25rem;
    }

    .auth-title { font-size: 1.25rem; font-weight: 600; margin: 0 0 0.25rem; }
    .auth-subtitle { font-size: 0.82rem; color: var(--p-text-muted-color); margin: 0; }

    .loading-state { text-align: center; padding: 3rem; color: var(--p-text-muted-color); }

    .matrix-wrapper { overflow-x: auto; }

    .matrix-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
    }

    .matrix-table th, .matrix-table td {
      padding: 0.6rem 0.85rem;
      border-bottom: 1px solid var(--p-surface-border);
      text-align: left;
    }

    .matrix-table thead th {
      background: var(--p-surface-ground);
      font-weight: 600;
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--p-text-muted-color);
      white-space: nowrap;
    }

    .role-col   { width: 200px; min-width: 160px; }
    .perm-col   { text-align: center !important; min-width: 150px; }
    .action-col { width: 100px; text-align: right !important; }

    .check-cell { text-align: center !important; }
    .action-cell { text-align: right !important; }

    .role-name-cell { vertical-align: middle; }
    .role-name { font-weight: 600; display: block; }
    .role-desc { font-size: 0.75rem; color: var(--p-text-muted-color); }

    .dirty-row { background: rgba(128, 0, 0, 0.04); }

    .matrix-table tbody tr:hover { background: var(--p-surface-hover); }
    .dirty-row:hover { background: rgba(128, 0, 0, 0.07) !important; }
  `]
})
export class AuthorizationComponent implements OnInit, OnDestroy {
  private permSvc  = inject(PermissionService);
  private roleSvc  = inject(RoleService);
  private toolbar  = inject(GlobalToolbarService);
  private msgSvc   = inject(MessageService);

  protected permissions = signal<PermissionDto[]>([]);
  protected roles       = signal<RoleRow[]>([]);
  protected loading     = signal(true);

  ngOnInit() {
    this.toolbar.set({ title: 'Authorization Management' });
    this.load();
  }

  ngOnDestroy() {
    this.toolbar.clear();
  }

  private load() {
    this.loading.set(true);
    this.roleSvc.getAll().subscribe({
      next: rolesRes => {
        const roleList = rolesRes.data ?? [];
        this.permSvc.getAll().subscribe({
          next: permsRes => {
            this.permissions.set(permsRes.data ?? []);
            forkJoin(roleList.map(r => this.permSvc.getRolePermissions(r.id))).subscribe({
              next: results => {
                this.roles.set(roleList.map((r, i) => ({
                  id: r.id,
                  name: r.name,
                  description: r.description,
                  permissionIds: new Set<number>(results[i]?.data ?? []),
                  dirty: false,
                  saving: false
                })));
                this.loading.set(false);
              }
            });
          }
        });
      }
    });
  }

  toggle(role: RoleRow, permId: number, checked: boolean) {
    if (checked) role.permissionIds.add(permId);
    else role.permissionIds.delete(permId);
    role.dirty = true;
    this.roles.update(rows => [...rows]);
  }

  save(role: RoleRow) {
    role.saving = true;
    this.roles.update(rows => [...rows]);
    this.permSvc.setRolePermissions(role.id, [...role.permissionIds]).subscribe({
      next: () => {
        role.saving = false;
        role.dirty  = false;
        this.roles.update(rows => [...rows]);
        this.msgSvc.add({ severity: 'success', summary: 'Saved', detail: `Permissions updated for ${role.name}.` });
      },
      error: () => {
        role.saving = false;
        this.roles.update(rows => [...rows]);
        this.msgSvc.add({ severity: 'error', summary: 'Error', detail: 'Failed to save permissions.' });
      }
    });
  }
}
