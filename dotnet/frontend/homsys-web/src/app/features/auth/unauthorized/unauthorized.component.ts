import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [ButtonModule],
  template: `
    <div class="unauthorized-wrapper">
      <div class="unauthorized-card">
        <i class="pi pi-lock unauthorized-icon"></i>
        <h1 class="unauthorized-title">Access Denied</h1>
        <p class="unauthorized-message">
          You don't have permission to access this page.<br />
          Contact your administrator if you believe this is an error.
        </p>
        <p-button label="Go to Dashboard" icon="pi pi-home" (onClick)="goHome()" />
      </div>
    </div>
  `,
  styles: [`
    .unauthorized-wrapper {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100%;
      padding: 2rem;
    }

    .unauthorized-card {
      text-align: center;
      background: var(--p-surface-card);
      border-radius: 10px;
      padding: 3rem 2.5rem;
      max-width: 420px;
      width: 100%;
    }

    .unauthorized-icon {
      font-size: 3rem;
      color: #800000;
      margin-bottom: 1rem;
    }

    .unauthorized-title {
      font-size: 1.5rem;
      font-weight: 700;
      margin: 0 0 0.75rem;
      color: var(--p-text-color);
    }

    .unauthorized-message {
      font-size: 0.9rem;
      color: var(--p-text-muted-color);
      line-height: 1.6;
      margin-bottom: 1.75rem;
    }
  `]
})
export class UnauthorizedComponent {
  private router = inject(Router);

  goHome() {
    this.router.navigate(['/']);
  }
}
