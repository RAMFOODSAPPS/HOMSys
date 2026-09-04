import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MenuModule } from 'primeng/menu';
import { ButtonModule } from 'primeng/button';
import { MenuItem } from 'primeng/api';
import { AuthService } from '../../core/services/auth.service';

interface ModuleGroup {
  label: string;
  icon: string;
  items: (MenuItem & { permission?: string })[];
  menuRef?: any;
}

@Component({
  selector: 'app-modulebar',
  standalone: true,
  imports: [MenuModule, ButtonModule],
  template: `
    <div class="modulebar">
      @for (mod of visibleModules(); track mod.label) {
        <div class="module-item">
          <button class="module-btn" [class.active]="isActive(mod)" (click)="mod.menuRef.toggle($event)">
            <i class="pi {{ mod.icon }}"></i>
            <span>{{ mod.label }}</span>
            <i class="pi pi-chevron-down chevron"></i>
          </button>
          <p-menu #modMenu [model]="mod.items" [popup]="true" appendTo="body" />
          {{ assignRef(mod, modMenu) }}
        </div>
      }
    </div>
  `,
  styles: [`
    .modulebar {
      height: 34px;
      background: var(--p-surface-card);
      display: flex;
      align-items: center;
      padding: 0 0.75rem;
      gap: 0.25rem;
      flex-shrink: 0;
      position: relative;
      z-index: 200;
    }

    .module-item { position: relative; }

    .module-btn {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      height: 28px;
      padding: 0 0.6rem;
      background: transparent;
      border: none;
      border-radius: 4px;
      font-family: inherit;
      font-size: 0.78rem;
      font-weight: 500;
      color: var(--p-text-color);
      cursor: pointer;
      transition: background 0.15s, color 0.15s;
      white-space: nowrap;
    }

    .module-btn:hover { background: var(--p-surface-hover); }

    .module-btn.active {
      background: rgba(128, 0, 0, 0.08);
      color: #800000;
      font-weight: 600;
    }

    .module-btn .pi:first-child { font-size: 0.72rem; }

    .chevron {
      font-size: 0.55rem !important;
      opacity: 0.5;
      margin-left: 0.1rem;
    }
  `]
})
export class ModulebarComponent {
  private router = inject(Router);
  protected auth = inject(AuthService);

  modules: ModuleGroup[] = [
    {
      label: 'Master Data',
      icon: 'pi-database',
      items: [
        { label: 'New Company',       icon: 'pi pi-building', routerLink: '/companies',    permission: 'companies' },
        { label: 'Companies List',    icon: 'pi pi-list',     routerLink: '/companies/list', permission: 'companies' },
        { separator: true, permission: 'companies' },
        { label: 'New Department',    icon: 'pi pi-sitemap',     routerLink: '/departments',    permission: 'departments' },
        { label: 'Departments List',  icon: 'pi pi-list',        routerLink: '/departments/list', permission: 'departments' },
        { separator: true, permission: 'departments' },
        { label: 'New Site',          icon: 'pi pi-map-marker',  routerLink: '/sites',    permission: 'sites' },
        { label: 'Sites List',        icon: 'pi pi-list',        routerLink: '/sites/list', permission: 'sites' },
        { separator: true, permission: 'sites' },
        { label: 'New Site Type',     icon: 'pi pi-tag',         routerLink: '/site-types',    permission: 'site-types' },
        { label: 'Site Types List',   icon: 'pi pi-list',        routerLink: '/site-types/list', permission: 'site-types' }
      ]
    },
    {
      label: 'Sales',
      icon: 'pi-shopping-cart',
      items: [
        { label: 'New Sales Order',   icon: 'pi pi-file-edit',   routerLink: '/sales-orders',    permission: 'sales-orders' },
        { label: 'Sales Orders',      icon: 'pi pi-list',        routerLink: '/sales-orders/list', permission: 'sales-orders' },
        { separator: true, permission: 'oos-report' },
        { label: 'OOS Report',        icon: 'pi pi-exclamation-triangle', routerLink: '/oos-report', permission: 'oos-report' },
        { separator: true, permission: 'pricelist-export' },
        { label: 'Pricelist Export',  icon: 'pi pi-file-excel',  routerLink: '/pricelist-export', permission: 'pricelist-export' }
      ]
    },
    {
      label: 'User Management',
      icon: 'pi-users',
      items: [
        { label: 'New User',    icon: 'pi pi-user-plus', routerLink: '/users',    permission: 'users' },
        { label: 'Users List',  icon: 'pi pi-list',      routerLink: '/users/list', permission: 'users' },
        { separator: true, permission: 'users' },
        { label: 'New Role',    icon: 'pi pi-shield',    routerLink: '/roles',    permission: 'roles' },
        { label: 'Roles List',  icon: 'pi pi-list',      routerLink: '/roles/list', permission: 'roles' }
      ]
    }
  ];

  visibleModules(): ModuleGroup[] {
    return this.modules
      .map(mod => ({
        ...mod,
        items: mod.items.filter(item => !item.permission || this.auth.isAdmin() || this.auth.hasPermission(item.permission))
      }))
      .filter(mod => mod.items.some(item => !item.separator));
  }

  assignRef(mod: ModuleGroup, ref: any) {
    mod.menuRef = ref;
    return '';
  }

  isActive(mod: ModuleGroup) {
    return mod.items.some(item => item.routerLink && this.router.isActive(item.routerLink, { paths: 'subset', queryParams: 'ignored', fragment: 'ignored', matrixParams: 'ignored' }));
  }
}
