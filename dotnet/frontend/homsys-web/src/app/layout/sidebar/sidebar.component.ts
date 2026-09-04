import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { LayoutService } from '../../core/services/layout.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="sidebar">

      <div class="sidebar-header">
        <span class="sidebar-section">Navigation</span>
        <button class="pin-btn"
                [class.pinned]="layout.sidebarPinned()"
                (click)="layout.togglePin()"
                [title]="layout.sidebarPinned() ? 'Unpin sidebar' : 'Pin sidebar'">
          <i class="pi" [class.pi-lock]="layout.sidebarPinned()" [class.pi-lock-open]="!layout.sidebarPinned()"></i>
        </button>
      </div>

      <ul class="sidebar-menu">

        @if (auth.isAdmin() || auth.hasPermission('companies') || auth.hasPermission('departments') || auth.hasPermission('sites') || auth.hasPermission('site-types') || auth.hasPermission('legacy-monitoring')) {
          <li class="menu-group">Master Data</li>
        }
        @if (auth.isAdmin() || auth.hasPermission('companies')) {
          <li>
            <a routerLink="/companies" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="onNavClick()">
              <i class="pi pi-building"></i>
              <span>New Company</span>
            </a>
          </li>
          <li>
            <a routerLink="/companies/list" routerLinkActive="active" (click)="onNavClick()">
              <i class="pi pi-list"></i>
              <span>Companies List</span>
            </a>
          </li>
        }
        @if (auth.isAdmin() || auth.hasPermission('departments')) {
          <li class="menu-sub-divider"></li>
          <li>
            <a routerLink="/departments" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="onNavClick()">
              <i class="pi pi-sitemap"></i>
              <span>New Department</span>
            </a>
          </li>
          <li>
            <a routerLink="/departments/list" routerLinkActive="active" (click)="onNavClick()">
              <i class="pi pi-list"></i>
              <span>Departments List</span>
            </a>
          </li>
        }
        @if (auth.isAdmin() || auth.hasPermission('sites')) {
          <li class="menu-sub-divider"></li>
          <li>
            <a routerLink="/sites" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="onNavClick()">
              <i class="pi pi-map-marker"></i>
              <span>New Site</span>
            </a>
          </li>
          <li>
            <a routerLink="/sites/list" routerLinkActive="active" (click)="onNavClick()">
              <i class="pi pi-list"></i>
              <span>Sites List</span>
            </a>
          </li>
        }
        @if (auth.isAdmin() || auth.hasPermission('site-types')) {
          <li class="menu-sub-divider"></li>
          <li>
            <a routerLink="/site-types" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="onNavClick()">
              <i class="pi pi-tag"></i>
              <span>New Site Type</span>
            </a>
          </li>
          <li>
            <a routerLink="/site-types/list" routerLinkActive="active" (click)="onNavClick()">
              <i class="pi pi-list"></i>
              <span>Site Types List</span>
            </a>
          </li>
        }
        @if (auth.isAdmin() || auth.hasPermission('legacy-monitoring')) {
          <li class="menu-sub-divider"></li>
          <li>
            <a routerLink="/legacy-monitoring" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="onNavClick()">
              <i class="pi pi-sync"></i>
              <span>Legacy Monitoring</span>
            </a>
          </li>
        }

        @if (auth.isAdmin() || auth.hasPermission('sales-orders') || auth.hasPermission('oos-report') || auth.hasPermission('pricelist-export')) {
          <li class="menu-group">Sales</li>
        }
        @if (auth.isAdmin() || auth.hasPermission('sales-orders')) {
          <li>
            <a routerLink="/sales-orders" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="onNavClick()">
              <i class="pi pi-file-edit"></i>
              <span>New Sales Order</span>
            </a>
          </li>
          <li>
            <a routerLink="/sales-orders/list" routerLinkActive="active" (click)="onNavClick()">
              <i class="pi pi-list"></i>
              <span>Sales Orders</span>
            </a>
          </li>
        }
        @if (auth.isAdmin() || auth.hasPermission('oos-report')) {
          <li class="menu-sub-divider"></li>
          <li>
            <a routerLink="/oos-report" routerLinkActive="active" (click)="onNavClick()">
              <i class="pi pi-exclamation-triangle"></i>
              <span>OOS Report</span>
            </a>
          </li>
        }
        @if (auth.isAdmin() || auth.hasPermission('pricelist-export')) {
          <li class="menu-sub-divider"></li>
          <li>
            <a routerLink="/pricelist-export" routerLinkActive="active" (click)="onNavClick()">
              <i class="pi pi-file-excel"></i>
              <span>Pricelist Export</span>
            </a>
          </li>
        }

        @if (auth.isAdmin() || auth.hasPermission('users') || auth.hasPermission('roles') || auth.hasPermission('authorization')) {
          <li class="menu-group">User Management</li>
        }
        @if (auth.isAdmin() || auth.hasPermission('users')) {
          <li>
            <a routerLink="/users" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="onNavClick()">
              <i class="pi pi-user-plus"></i>
              <span>New User</span>
            </a>
          </li>
          <li>
            <a routerLink="/users/list" routerLinkActive="active" (click)="onNavClick()">
              <i class="pi pi-list"></i>
              <span>Users List</span>
            </a>
          </li>
        }
        @if (auth.isAdmin() || auth.hasPermission('roles')) {
          <li class="menu-sub-divider"></li>
          <li>
            <a routerLink="/roles" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="onNavClick()">
              <i class="pi pi-shield"></i>
              <span>New Role</span>
            </a>
          </li>
          <li>
            <a routerLink="/roles/list" routerLinkActive="active" (click)="onNavClick()">
              <i class="pi pi-list"></i>
              <span>Roles List</span>
            </a>
          </li>
        }
        @if (auth.isAdmin() || auth.hasPermission('authorization')) {
          <li class="menu-sub-divider"></li>
          <li>
            <a routerLink="/authorization" routerLinkActive="active" (click)="onNavClick()">
              <i class="pi pi-lock"></i>
              <span>Authorization</span>
            </a>
          </li>
        }

      </ul>

      <div class="sidebar-footer">
        <button class="logout-btn" (click)="auth.logout()">
          <i class="pi pi-sign-out"></i>
          <span>Logout</span>
        </button>
      </div>
    </nav>
  `,
  styles: [`
    .sidebar {
      width: 220px;
      height: 100%;
      background: #800000;
      display: flex;
      flex-direction: column;
      flex-shrink: 0;
      overflow: hidden;
    }

    .sidebar-header {
      padding: 0.875rem 0.75rem 0.875rem 1rem;
      border-bottom: 1px solid rgba(255,255,255,0.12);
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-shrink: 0;
    }

    .sidebar-section {
      font-size: 0.68rem;
      font-weight: 600;
      color: rgba(255,255,255,0.45);
      letter-spacing: 0.05em;
      text-transform: uppercase;
    }

    .pin-btn {
      background: transparent;
      border: none;
      color: rgba(255,255,255,0.35);
      font-size: 0.72rem;
      cursor: pointer;
      padding: 0.25rem;
      border-radius: 3px;
      display: flex;
      align-items: center;
      transition: color 0.15s, background 0.15s;
      flex-shrink: 0;
    }

    .pin-btn:hover { color: #fff; background: rgba(255,255,255,0.1); }
    .pin-btn.pinned { color: rgba(255,255,255,0.85); }

    .sidebar-menu {
      list-style: none;
      margin: 0;
      padding: 0.5rem 0;
      flex: 1;
      overflow-y: auto;
    }

    .menu-group {
      font-size: 0.62rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: rgba(255,255,255,0.35);
      padding: 0.75rem 1rem 0.25rem;
    }

    .sidebar-menu a {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.55rem 0.875rem;
      color: rgba(255,255,255,0.82);
      text-decoration: none;
      border-radius: 3px;
      margin: 0.1rem 0.5rem;
      font-size: 0.83rem;
      transition: background 0.15s;
    }

    .sidebar-menu a:hover  { background: rgba(255,255,255,0.1); color: #fff; }
    .sidebar-menu a.active { background: rgba(255,255,255,0.18); color: #fff; font-weight: 600; }

    .menu-sub-divider { height: 1px; background: rgba(255,255,255,0.08); margin: 0.3rem 0.75rem; }

    .sidebar-footer {
      border-top: 1px solid rgba(255,255,255,0.12);
      padding: 0.75rem;
      flex-shrink: 0;
    }

    .logout-btn {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      width: 100%;
      padding: 0.45rem 0.5rem;
      background: transparent;
      border: 1px solid rgba(255,255,255,0.2);
      border-radius: 3px;
      color: rgba(255,255,255,0.75);
      font-size: 0.8rem;
      font-family: inherit;
      cursor: pointer;
      transition: background 0.15s, color 0.15s;
    }

    .logout-btn:hover { background: rgba(255,255,255,0.1); color: #fff; }
  `]
})
export class SidebarComponent {
  protected auth   = inject(AuthService);
  protected layout = inject(LayoutService);

  onNavClick() {
    this.layout.closeIfUnpinned();
  }
}
