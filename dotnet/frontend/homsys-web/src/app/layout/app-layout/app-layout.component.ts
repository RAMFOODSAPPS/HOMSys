import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { TopbarComponent } from '../topbar/topbar.component';
import { ModulebarComponent } from '../modulebar/modulebar.component';
import { PageToolbarComponent } from '../page-toolbar/page-toolbar.component';
import { AppTabBarComponent } from '../app-tab-bar/app-tab-bar.component';
import { FooterComponent } from '../footer/footer.component';
import { LayoutService } from '../../core/services/layout.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, TopbarComponent, ModulebarComponent, PageToolbarComponent, AppTabBarComponent, FooterComponent],
  template: `
    <div class="layout-wrapper" [class.sb-open]="layout.sidebarVisible()">
      <app-topbar />
      <div class="layout-body">
        <div class="sidebar-backdrop" (click)="layout.toggleSidebar()"></div>
        <app-sidebar />
        <div class="main-area">
          <app-modulebar />
          <div class="bar-separator"></div>
          <app-page-toolbar />
          <div class="bar-separator bar-separator--thin"></div>
          <app-tab-bar />
          <div class="layout-content">
            <router-outlet />
          </div>
        </div>
      </div>
      <app-footer />
    </div>
  `,
  styles: [`
    .layout-wrapper {
      display: flex;
      flex-direction: column;
      width: 100vw;
      height: 100vh;
      overflow: hidden;
      position: fixed;
      top: 0;
      left: 0;
    }

    .layout-body {
      flex: 1;
      display: flex;
      overflow: hidden;
      position: relative;
    }

    .main-area {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    .bar-separator {
      height: 1px;
      background: #d1d5db;
      flex-shrink: 0;
    }

    .bar-separator--thin {
      height: 0.5px;
    }

    .layout-content {
      flex: 1;
      overflow-y: auto;
      overflow-x: hidden;
      padding: 1.25rem;
      background: var(--p-surface-ground);
    }

    .sidebar-backdrop { display: none; }

    /* ── Sidebar hidden by default, slides in as overlay over main-area only ── */
    ::ng-deep app-sidebar .sidebar {
      position: absolute !important;
      top: 0;
      left: -220px;
      height: 100% !important;
      z-index: 100;
      transition: left 0.22s ease;
      box-shadow: none;
    }

    .layout-wrapper.sb-open ::ng-deep app-sidebar .sidebar {
      left: 0;
      box-shadow: 4px 0 16px rgba(0,0,0,0.25);
    }

    .sidebar-backdrop {
      display: block;
      position: absolute;
      inset: 0;
      z-index: 99;
      background: transparent;
      pointer-events: none;
      transition: background 0.22s;
    }

    .layout-wrapper.sb-open .sidebar-backdrop {
      background: rgba(0,0,0,0.35);
      pointer-events: auto;
    }

    /* ── Desktop — sidebar pushes main-area ── */
    @media (min-width: 961px) {
      ::ng-deep app-sidebar .sidebar {
        position: relative !important;
        left: 0 !important;
        height: 100% !important;
        width: 0 !important;
        min-width: 0 !important;
        box-shadow: none !important;
        transition: width 0.22s ease !important;
        overflow: hidden !important;
      }

      .layout-wrapper.sb-open ::ng-deep app-sidebar .sidebar {
        width: 220px !important;
        box-shadow: none !important;
      }

      .sidebar-backdrop { display: none !important; }
    }

    /* ── Mobile / Tablet ── */
    @media (max-width: 960px) {
      .layout-content { padding: 0.875rem; }
    }

    @media (max-width: 600px) {
      .layout-content { padding: 0.625rem; }
    }
  `]
})
export class AppLayoutComponent {
  protected layout = inject(LayoutService);
}
