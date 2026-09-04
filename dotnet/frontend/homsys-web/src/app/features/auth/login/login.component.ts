import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, InputTextModule, PasswordModule, ButtonModule, CardModule, MessageModule],
  template: `
    <div class="login-page">
      <p-card styleClass="login-card">
        <ng-template pTemplate="header">
          <div class="login-header">
            <h1>HOMSys</h1>
            <p>Head Office Monitoring System</p>
          </div>
        </ng-template>

        <form [formGroup]="form" (ngSubmit)="onSubmit()">
          @if (errorMessage()) {
            <p-message severity="error" styleClass="w-full mb-3">
              <span>{{ errorMessage() }}</span>
            </p-message>
          }

          <div class="field">
            <label for="username">Username</label>
            <input id="username" pInputText formControlName="username"
              placeholder="Enter username" class="w-full" />
          </div>

          <div class="field">
            <label for="password">Password</label>
            <p-password inputId="password" formControlName="password"
              placeholder="Enter password" [feedback]="false" [toggleMask]="true"
              styleClass="w-full" inputStyleClass="w-full" />
          </div>

          <p-button type="submit" label="Sign In" icon="pi pi-sign-in"
            [loading]="loading()" [disabled]="form.invalid" styleClass="w-full mt-2" />
        </form>
      </p-card>
    </div>
  `,
  styles: [`
    .login-page { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: var(--p-surface-ground); }
    :host ::ng-deep .login-card { width: 380px; }
    .login-header { text-align: center; padding: 1.5rem 1rem 0; }
    .login-header h1 { font-size: 2rem; font-weight: 700; color: var(--p-primary-color); margin: 0; }
    .login-header p { color: var(--p-text-muted-color); margin: 0.25rem 0 0; font-size: 0.875rem; }
    .field { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 1rem; }
    .field label { font-weight: 500; font-size: 0.875rem; }
  `]
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private fb = inject(FormBuilder);

  protected form = this.fb.group({
    username: ['', [Validators.required]],
    password: ['', [Validators.required]]
  });

  protected loading = signal(false);
  protected errorMessage = signal<string | null>(null);

  onSubmit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.errorMessage.set(null);
    const { username, password } = this.form.value;
    this.auth.login({ username: username!, password: password! }).subscribe({
      next: () => { this.loading.set(false); this.router.navigate(['/']); },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Login failed. Please check your credentials.');
      }
    });
  }
}
