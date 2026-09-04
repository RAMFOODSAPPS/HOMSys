import { Component, computed, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';
import { LayoutService } from '../../core/services/layout.service';
import { TabBarService } from '../../core/services/tab-bar.service';

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [],
  template: `
    <header class="topbar" [class.sb-open]="layout.sidebarVisible()">

      <!-- Brand — mirrors sidebar width so title aligns with modulebar -->
      <div class="topbar-brand">
        <button class="hamburger" (click)="layout.toggleSidebar()" aria-label="Toggle menu">
          <i class="pi pi-bars"></i>
        </button>
        <span class="app-logo">HOMSys</span>
      </div>

      <!-- Main area — starts at the same left edge as the modulebar -->
      <div class="topbar-main">
        @if (activeTab()) {
          <span class="topbar-page">{{ activeTab()!.label }}</span>
        }
      </div>

      <!-- User info -->
      <div class="topbar-right">
        <div class="user-avatar">{{ initials() }}</div>
        <div class="user-info">
          <span class="user-name">{{ auth.user()?.firstName }} {{ auth.user()?.lastName }}</span>
          <small class="user-role">{{ auth.user()?.roles?.[0] ?? '' }}</small>
        </div>
      </div>

    </header>
  `,
  styles: [`
    .topbar {
      height: 46px;
      background: #800000;
      display: flex;
      align-items: center;
      flex-shrink: 0;
      position: relative;
      z-index: 200;
    }

    /* Brand section — collapses/expands with the sidebar */
    .topbar-brand {
      display: flex;
      align-items: center;
      gap: 0.65rem;
      padding: 0 0.75rem 0 1rem;
      flex-shrink: 0;
      width: auto;
      transition: width 0.22s ease;
      overflow: hidden;
    }

    /* On desktop, brand widens to match the sidebar when it opens */
    @media (min-width: 961px) {
      .topbar.sb-open .topbar-brand {
        width: 220px;
      }
    }

    .app-logo {
      font-weight: 700;
      font-size: 1rem;
      color: #fff;
      letter-spacing: 0.04em;
      white-space: nowrap;
    }

    .hamburger {
      background: transparent;
      border: none;
      color: rgba(255,255,255,0.85);
      font-size: 1rem;
      cursor: pointer;
      padding: 0.3rem 0.4rem;
      border-radius: 3px;
      display: flex;
      align-items: center;
      flex-shrink: 0;
      transition: background 0.15s;
    }

    .hamburger:hover { background: rgba(255,255,255,0.1); color: #fff; }

    /* Main area — aligns with left edge of modulebar */
    .topbar-main {
      flex: 1;
      display: flex;
      align-items: center;
      padding: 0 0.75rem;
      min-width: 0;
    }

    .topbar-page {
      font-size: 15px;
      font-weight: 600;
      color: rgba(255,255,255,0.92);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .topbar-right {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding-right: 1rem;
      flex-shrink: 0;
    }

    .user-avatar {
      width: 26px;
      height: 26px;
      border-radius: 50%;
      background: rgba(255,255,255,0.2);
      color: #fff;
      font-size: 0.68rem;
      font-weight: 700;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .user-info {
      display: flex;
      flex-direction: column;
      line-height: 1.2;
    }

    .user-name {
      font-size: 0.78rem;
      font-weight: 600;
      color: #fff;
      white-space: nowrap;
    }

    .user-role {
      font-size: 0.65rem;
      color: rgba(255,255,255,0.55);
      white-space: nowrap;
    }

    @media (max-width: 960px) {
      .user-info { display: none; }
    }

    @media (max-width: 480px) {
      .user-avatar { display: none; }
    }
  `]
})
export class TopbarComponent {
  protected auth   = inject(AuthService);
  protected layout = inject(LayoutService);
  private   tabBar = inject(TabBarService);

  protected activeTab = computed(() =>
    this.tabBar.tabs().find(t => t.key === this.tabBar.activeKey())
  );

  initials() {
    const u = this.auth.user();
    if (!u) return '?';
    return `${u.firstName[0]}${u.lastName[0]}`.toUpperCase();
  }
}
