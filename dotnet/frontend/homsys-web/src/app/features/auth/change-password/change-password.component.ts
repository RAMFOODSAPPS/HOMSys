import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../../core/services/auth.service';

function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  const newPassword = control.get('newPassword')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return newPassword && confirmPassword && newPassword !== confirmPassword
    ? { mismatch: true }
    : null;
}

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [ReactiveFormsModule, InputTextModule, PasswordModule, ButtonModule, CardModule, MessageModule],
  template: `
    <div class="change-password-page">
      <p-card styleClass="change-password-card">
        <ng-template pTemplate="header">
          <div class="header">
            <h1>Change Password</h1>
            <p>You must set a new password before continuing.</p>
          </div>
        </ng-template>

        <form [formGroup]="form" (ngSubmit)="onSubmit()">
          @if (errorMessage()) {
            <p-message severity="error" styleClass="w-full mb-3">
              <span>{{ errorMessage() }}</span>
            </p-message>
          }

          <div class="field">
            <label for="currentPassword">Current (temporary) Password</label>
            <p-password inputId="currentPassword" formControlName="currentPassword"
              placeholder="Enter current password" [feedback]="false" [toggleMask]="true"
              styleClass="w-full" inputStyleClass="w-full" />
          </div>

          <div class="field">
            <label for="newPassword">New Password</label>
            <p-password inputId="newPassword" formControlName="newPassword"
              placeholder="Enter new password" [toggleMask]="true"
              styleClass="w-full" inputStyleClass="w-full" />
          </div>

          <div class="field">
            <label for="confirmPassword">Confirm New Password</label>
            <p-password inputId="confirmPassword" formControlName="confirmPassword"
              placeholder="Re-enter new password" [feedback]="false" [toggleMask]="true"
              styleClass="w-full" inputStyleClass="w-full" />
          </div>

          @if (form.errors?.['mismatch'] && form.get('confirmPassword')?.touched) {
            <p-message severity="warn" styleClass="w-full mb-3">
              <span>New password and confirmation do not match.</span>
            </p-message>
          }

          <p-button type="submit" label="Change Password" icon="pi pi-key"
            [loading]="loading()" [disabled]="form.invalid" styleClass="w-full mt-2" />
        </form>
      </p-card>
    </div>
  `,
  styles: [`
    .change-password-page { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: var(--p-surface-ground); }
    :host ::ng-deep .change-password-card { width: 400px; }
    .header { text-align: center; padding: 1.5rem 1rem 0; }
    .header h1 { font-size: 1.5rem; font-weight: 700; color: var(--p-primary-color); margin: 0; }
    .header p { color: var(--p-text-muted-color); margin: 0.25rem 0 0; font-size: 0.875rem; }
    .field { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 1rem; }
    .field label { font-weight: 500; font-size: 0.875rem; }
  `]
})
export class ChangePasswordComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private fb = inject(FormBuilder);

  protected form = this.fb.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: passwordsMatch });

  protected loading = signal(false);
  protected errorMessage = signal<string | null>(null);

  onSubmit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.errorMessage.set(null);
    const { currentPassword, newPassword } = this.form.value;
    this.auth.changePassword({ currentPassword: currentPassword!, newPassword: newPassword! }).subscribe({
      next: () => { this.loading.set(false); this.router.navigate(['/']); },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Unable to change password.');
      }
    });
  }
}
