import { Component, inject, OnInit, signal, ViewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Table, TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TooltipModule } from 'primeng/tooltip';
import { UserService } from '../../../core/services/user.service';
import { UserDto } from '../../../core/models/user.model';
import { UserFormComponent } from '../user-form/user-form.component';

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [TableModule, ButtonModule, TagModule, InputTextModule, IconFieldModule, InputIconModule, TooltipModule, UserFormComponent, DatePipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h2 class="card-title">User Management</h2>
        <p-button label="New User" icon="pi pi-plus" (onClick)="openForm()" />
      </div>

      <p-table
        #dt
        [value]="users()"
        [loading]="loading()"
        [paginator]="true"
        [rows]="10"
        [rowsPerPageOptions]="[10, 25, 50]"
        [globalFilterFields]="['username', 'email', 'firstName', 'lastName']"
        dataKey="id"
        responsiveLayout="scroll"
      >
        <ng-template pTemplate="caption">
          <div class="table-toolbar">
            <p-iconfield>
              <p-inputicon class="pi pi-search" />
              <input pInputText type="text"
                (input)="dt.filterGlobal($any($event.target).value, 'contains')"
                placeholder="Search users..." />
            </p-iconfield>
          </div>
        </ng-template>

        <ng-template pTemplate="header">
          <tr>
            <th pSortableColumn="username">Username <p-sortIcon field="username" /></th>
            <th pSortableColumn="firstName">Name <p-sortIcon field="firstName" /></th>
            <th pSortableColumn="email">Email <p-sortIcon field="email" /></th>
            <th>Roles</th>
            <th pSortableColumn="isActive">Status <p-sortIcon field="isActive" /></th>
            <th pSortableColumn="createdAt">Created <p-sortIcon field="createdAt" /></th>
            <th style="width:120px">Actions</th>
          </tr>
        </ng-template>

        <ng-template pTemplate="body" let-user>
          <tr>
            <td>{{ user.username }}</td>
            <td>{{ user.firstName }} {{ user.lastName }}</td>
            <td>{{ user.email }}</td>
            <td>
              @for (role of user.roles; track role) {
                <p-tag [value]="role" severity="info" styleClass="mr-1" />
              }
            </td>
            <td>
              <p-tag [value]="user.isActive ? 'Active' : 'Inactive'"
                [severity]="user.isActive ? 'success' : 'danger'" />
            </td>
            <td>{{ user.createdAt | date:'mediumDate' }}</td>
            <td>
              <p-button icon="pi pi-pencil" [text]="true" severity="secondary"
                (onClick)="openForm(user)" pTooltip="Edit" />
              <p-button icon="pi pi-trash" [text]="true" severity="danger"
                (onClick)="confirmDelete(user)" pTooltip="Delete" />
            </td>
          </tr>
        </ng-template>

        <ng-template pTemplate="emptymessage">
          <tr><td colspan="7" class="text-center">No users found.</td></tr>
        </ng-template>
      </p-table>
    </div>

    <app-user-form
      [(visible)]="formVisible"
      [editUser]="selectedUser()"
      (saved)="onUserSaved()"
    />
  `,
  styles: [`
    .card { background: var(--p-surface-card); border-radius: 8px; padding: 1.25rem; }
    .card-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 1rem; }
    .card-title { font-size: 1.25rem; font-weight: 600; margin: 0; }
    .table-toolbar { display: flex; justify-content: flex-end; }
  `]
})
export class UserListComponent implements OnInit {
  private userService = inject(UserService);
  private confirmationService = inject(ConfirmationService);
  private messageService = inject(MessageService);

  @ViewChild('dt') dt!: Table;

  protected users = signal<UserDto[]>([]);
  protected loading = signal(true);
  protected formVisible = signal(false);
  protected selectedUser = signal<UserDto | undefined>(undefined);

  ngOnInit() { this.loadUsers(); }

  loadUsers() {
    this.loading.set(true);
    this.userService.getAll().subscribe({
      next: res => { this.users.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  openForm(user?: UserDto) {
    this.selectedUser.set(user);
    this.formVisible.set(true);
  }

  onUserSaved() {
    this.loadUsers();
    this.messageService.add({
      severity: 'success', summary: 'Success',
      detail: this.selectedUser() ? 'User updated.' : 'User created.'
    });
  }

  confirmDelete(user: UserDto) {
    this.confirmationService.confirm({
      message: `Delete user <strong>${user.username}</strong>? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.userService.delete(user.id).subscribe({
          next: () => {
            this.loadUsers();
            this.messageService.add({ severity: 'success', summary: 'Deleted', detail: `User ${user.username} deleted.` });
          },
          error: () => this.messageService.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete user.' })
        });
      }
    });
  }
}
