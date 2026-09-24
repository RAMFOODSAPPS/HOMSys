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

        @if (editUser()) {
          <div class="field">
            <label>New Password (leave blank to keep current)</label>
            <p-password formControlName="password" [feedback]="true"
              [toggleMask]="true" styleClass="w-full" inputStyleClass="w-full" />
          </div>

          <div class="field-inline">
            <label>Active</label>
            <p-toggleswitch formControlName="isActive" />
          </div>
        } @else {
          <p-message severity="info" styleClass="w-full mb-3">
            <span>A password will be generated automatically. The user must change it on first login.</span>
          </p-message>
        }
      </form>

      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="onClose()" />
        <p-button type="submit" form="userForm"
          [label]="editUser() ? 'Save Changes' : 'Create User'"
          [loading]="loading()" [disabled]="form.invalid" />
      </ng-template>
    </p-dialog>

    <p-dialog
      [visible]="generatedPassword() !== null"
      (visibleChange)="!$event && acknowledgeGeneratedPassword()"
      header="User Created"
      [modal]="true"
      [style]="{ width: '420px' }"
      [draggable]="false"
      [closable]="false"
    >
      <p>Share this temporary password with the user. It will not be shown again — they must change it on first login.</p>
      <div class="generated-password">{{ generatedPassword() }}</div>
      <ng-template pTemplate="footer">
        <p-button label="Copy" icon="pi pi-copy" [text]="true" (onClick)="copyGeneratedPassword()" />
        <p-button label="Done" (onClick)="acknowledgeGeneratedPassword()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .field { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 1rem; }
    .field label { font-weight: 500; font-size: 0.875rem; }
    .field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .field-inline { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 1rem; }
    .generated-password { font-family: monospace; font-size: 1.1rem; font-weight: 600;
      background: var(--p-surface-100); border-radius: 6px; padding: 0.75rem; text-align: center;
      letter-spacing: 0.05em; margin: 0.75rem 0; }
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
  protected generatedPassword = signal<string | null>(null);

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
      this.form.get('password')?.clearValidators();
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
    if (u) {
      this.userService.update(u.id, { email: v.email!, firstName: v.firstName!, lastName: v.lastName!, isActive: v.isActive!, roleIds: v.roleIds ?? [], newPassword: v.password || undefined })
        .subscribe({
          next: () => { this.loading.set(false); this.visible.set(false); this.saved.emit(); },
          error: (err) => { this.loading.set(false); this.errorMessage.set(err?.error?.message ?? 'An error occurred.'); }
        });
      return;
    }

    this.userService.create({ username: v.username!, email: v.email!, firstName: v.firstName!, lastName: v.lastName!, roleIds: v.roleIds ?? [] })
      .subscribe({
        next: (res) => { this.loading.set(false); this.generatedPassword.set(res.generatedPassword); },
        error: (err) => { this.loading.set(false); this.errorMessage.set(err?.error?.message ?? 'An error occurred.'); }
      });
  }

  acknowledgeGeneratedPassword() {
    this.generatedPassword.set(null);
    this.visible.set(false);
    this.saved.emit();
  }

  copyGeneratedPassword() {
    const pwd = this.generatedPassword();
    if (pwd) navigator.clipboard?.writeText(pwd);
  }

  onClose() {
    this.visible.set(false);
    this.form.reset({ isActive: true, roleIds: [] });
    this.errorMessage.set(null);
  }
}
