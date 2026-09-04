import { Component, inject, input, model, OnChanges, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { MultiSelectModule } from 'primeng/multiselect';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';
import { UserService } from '../../../core/services/user.service';
import { UserDto, RoleDto } from '../../../core/models/user.model';

@Component({
  selector: 'app-user-form',
  standalone: true,
  imports: [ReactiveFormsModule, DialogModule, InputTextModule, PasswordModule,
    ButtonModule, MultiSelectModule, ToggleSwitchModule, MessageModule],
  template: `
    <p-dialog
      [visible]="visible()"
      (visibleChange)="visible.set($event)"
      [header]="editUser() ? 'Edit User - ' + editUser()!.firstName + ' ' + editUser()!.lastName : 'New User'"
      [modal]="true"
      [style]="{ width: '480px' }"
      [draggable]="false"
      (onHide)="onClose()"
    >
      <form [formGroup]="form" (ngSubmit)="onSubmit()" id="userForm">
        @if (errorMessage()) {
          <p-message severity="error" styleClass="w-full mb-3">
            <span>{{ errorMessage() }}</span>
          </p-message>
        }

        @if (!editUser()) {
          <div class="field">
            <label>Username *</label>
            <input pInputText formControlName="username" class="w-full" placeholder="e.g. jdelacruz" />
          </div>
        }

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
          <label>Roles *</label>
          <p-multiselect formControlName="roleIds" [options]="roles()"
            optionLabel="name" optionValue="id"
            placeholder="Select roles" styleClass="w-full" />
        </div>

        <div class="field">
          <label>{{ editUser() ? 'New Password (leave blank to keep current)' : 'Password *' }}</label>
          <p-password formControlName="password" [feedback]="true"
            [toggleMask]="true" styleClass="w-full" inputStyleClass="w-full" />
        </div>

        @if (editUser()) {
          <div class="field-inline">
            <label>Active</label>
            <p-toggleswitch formControlName="isActive" />
          </div>
        }
      </form>

      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="onClose()" />
        <p-button type="submit" form="userForm"
          [label]="editUser() ? 'Save Changes' : 'Create User'"
          [loading]="loading()" [disabled]="form.invalid" />
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .field { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 1rem; }
    .field label { font-weight: 500; font-size: 0.875rem; }
    .field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .field-inline { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 1rem; }
  `]
})
export class UserFormComponent implements OnChanges {
  private userService = inject(UserService);
  private messageService = inject(MessageService);
  private fb = inject(FormBuilder);

  visible = model(false);
  editUser = input<UserDto | undefined>(undefined);
  saved = output<void>();

  protected roles = signal<RoleDto[]>([]);
  protected loading = signal(false);
  protected errorMessage = signal<string | null>(null);

  protected form = this.fb.group({
    username: ['', [Validators.required, Validators.minLength(3)]],
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    roleIds: [[] as number[], [Validators.required]],
    password: [''],
    isActive: [true]
  });

  ngOnChanges() {
    this.loadRoles();
    this.populateForm();
    this.errorMessage.set(null);
  }

  private loadRoles() {
    this.userService.getRoles().subscribe({ next: res => this.roles.set(res.data ?? []) });
  }

  private populateForm() {
    const u = this.editUser();
    if (u) {
      this.form.patchValue({ firstName: u.firstName, lastName: u.lastName, email: u.email, roleIds: u.roleIds ?? [], isActive: u.isActive, password: '' });
      this.form.get('username')?.clearValidators();
      this.form.get('password')?.clearValidators();
    } else {
      this.form.reset({ isActive: true, roleIds: [] });
      this.form.get('username')?.setValidators([Validators.required, Validators.minLength(3)]);
      this.form.get('password')?.setValidators([Validators.required, Validators.minLength(8)]);
    }
    this.form.get('username')?.updateValueAndValidity();
    this.form.get('password')?.updateValueAndValidity();
  }

  onSubmit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.errorMessage.set(null);
    const v = this.form.value;
    const u = this.editUser();
    const req$ = u
      ? this.userService.update(u.id, { email: v.email!, firstName: v.firstName!, lastName: v.lastName!, isActive: v.isActive!, roleIds: v.roleIds ?? [], newPassword: v.password || undefined })
      : this.userService.create({ username: v.username!, email: v.email!, firstName: v.firstName!, lastName: v.lastName!, password: v.password!, roleIds: v.roleIds ?? [] });

    req$.subscribe({
      next: () => { this.loading.set(false); this.visible.set(false); this.saved.emit(); },
      error: (err) => { this.loading.set(false); this.errorMessage.set(err?.error?.message ?? 'An error occurred.'); }
    });
  }

  onClose() {
    this.visible.set(false);
    this.form.reset({ isActive: true, roleIds: [] });
    this.errorMessage.set(null);
  }
}
