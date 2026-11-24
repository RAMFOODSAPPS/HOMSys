import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../../core/services/auth.service';
import { TabBarService } from '../../../core/services/tab-bar.service';

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
            <p>Head Office Management System</p>
          </div>
        </ng-template>

        @if (!showForgotPassword()) {
        <form [formGroup]="form" (ngSubmit)="onSubmit()">
          @if (errorMessage()) {
            <p-message severity="error" styleClass="w-full mb-3">
              <span>{{ errorMessage() }}</span>
            </p-message>
          }
          <!-- After "Forgot password?": the email was sent — back to signing in. -->
          @if (infoMessage()) {
            <p-message severity="success" styleClass="w-full mb-3">
              <span>{{ infoMessage() }}</span>
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

          <div class="forgot-password">
            <a href="javascript:void(0)" (click)="openForgot()">Forgot password?</a>
          </div>
        </form>
        } @else {
          <!-- Forgot password (replaces the sign-in form): a temporary password is emailed to the user's address. -->
          <div class="forgot-box">
            <h3>Forgot password</h3>
            <p class="forgot-hint">Enter your username and we'll email you a temporary password.</p>
            <div class="field">
              <label for="forgotUser">Username (Employee ID)</label>
              <input id="forgotUser" pInputText [value]="forgotUser()" (input)="forgotUser.set($any($event.target).value)"
                (keydown.enter)="sendForgot()" placeholder="e.g. 03005185" class="w-full" autocomplete="username" />
            </div>
            @if (forgotResult(); as r) {
              <p-message [severity]="r.ok ? 'success' : 'warn'" styleClass="w-full mb-2"><span>{{ r.text }}</span></p-message>
            }
            <p-button label="Email me a temporary password" icon="pi pi-envelope" [loading]="forgotSending()"
              [disabled]="!forgotUser().trim() || forgotSending()" (onClick)="sendForgot()" styleClass="w-full mt-2" />
            <div class="forgot-password">
              <a href="javascript:void(0)" (click)="showForgotPassword.set(false)"><i class="pi pi-arrow-left"></i> Back to sign in</a>
            </div>
          </div>
        }
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
    .forgot-password { text-align: center; margin-top: 0.75rem; font-size: 0.875rem; }
    .forgot-password a { color: var(--p-primary-color); text-decoration: none; }
    .forgot-password a:hover { text-decoration: underline; }
    .forgot-box h3 { margin: 0 0 0.25rem; font-size: 1.05rem; }
    .forgot-hint { margin: 0 0 1rem; color: var(--p-text-muted-color); font-size: 0.875rem; }
    .forgot-password i { font-size: 0.75rem; }
  `]
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private tabBar = inject(TabBarService);
  private fb = inject(FormBuilder);

  protected form = this.fb.group({
    username: ['', [Validators.required]],
    password: ['', [Validators.required]]
  });

  protected loading = signal(false);
  protected errorMessage = signal<string | null>(null);
  protected showForgotPassword = signal(false);
  /** Shown above the form after a temporary password was emailed. */
  protected infoMessage   = signal<string | null>(null);
  protected forgotUser    = signal('');
  protected forgotSending = signal(false);
  protected forgotResult  = signal<{ ok: boolean; text: string } | null>(null);

  /** Opens the forgot-password box, starting from the username already typed. */
  openForgot() {
    this.infoMessage.set(null);
    this.forgotUser.set(this.form.value.username?.trim() ?? '');
    this.forgotResult.set(null);
    this.showForgotPassword.set(true);
  }

  sendForgot() {
    const username = this.forgotUser().trim();
    if (!username || this.forgotSending()) return;
    this.forgotSending.set(true);
    this.forgotResult.set(null);
    this.auth.forgotPassword(username).subscribe({
      next: res => {
        this.forgotSending.set(false);
        if (!res.success) { this.forgotResult.set({ ok: false, text: res.message }); return; }
        // Sent: close the box and go back to signing in, with the username filled and the password cleared.
        this.showForgotPassword.set(false);
        this.errorMessage.set(null);
        this.infoMessage.set(`${res.message} Sign in below with the temporary password from your email.`);
        this.form.patchValue({ username, password: '' });
        setTimeout(() => document.getElementById('password')?.focus());
      },
      error: err => {
        this.forgotSending.set(false);
        this.forgotResult.set({ ok: false, text: err?.error?.message ?? 'Could not send it. Please try again later.' });
      }
    });
  }

  onSubmit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.errorMessage.set(null);
    this.infoMessage.set(null);
    const { username, password } = this.form.value;
    this.auth.login({ username: username!, password: password! }).subscribe({
      next: () => {
        this.loading.set(false);
        this.tabBar.startSession(username!);
        // Back to where the user was going (guard's returnUrl) — same-site paths only.
        const ret = new URLSearchParams(location.search).get('returnUrl') ?? '';
        const back = ret.startsWith('/') && !ret.startsWith('//') && !ret.startsWith('/login') ? ret : '/';
        if (this.auth.user()?.mustChangePassword) this.router.navigate(['/change-password']);
        else this.router.navigateByUrl(back);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Login failed. Please check your credentials.');
      }
    });
  }
}
