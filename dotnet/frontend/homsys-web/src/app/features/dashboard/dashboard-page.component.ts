import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

interface DashboardCard {
  label: string;
  icon: string;
  route: string;
  permission?: string;
}

interface DashboardGroup {
  title: string;
  cards: DashboardCard[];
}

const GROUPS: DashboardGroup[] = [
  {
    title: 'Master Data',
    cards: [
      { label: 'Companies',         icon: 'pi-building',           route: '/companies/list',    permission: 'companies' },
      { label: 'Departments',       icon: 'pi-sitemap',            route: '/departments/list',  permission: 'departments' },
      { label: 'Sites',             icon: 'pi-map-marker',         route: '/sites/list',        permission: 'sites' },
      { label: 'Site Types',        icon: 'pi-tag',                route: '/site-types/list',   permission: 'site-types' },
      { label: 'Legacy Monitoring', icon: 'pi-sync',               route: '/legacy-monitoring', permission: 'legacy-monitoring' }
    ]
  },
  {
    title: 'Sales',
    cards: [
      { label: 'Sales Orders',      icon: 'pi-file-edit',              route: '/sales-orders/list', permission: 'sales-orders' },
      { label: 'OOS Report',        icon: 'pi-exclamation-triangle',   route: '/oos-report',        permission: 'oos-report' },
      { label: 'Pricelist Export',  icon: 'pi-file-excel',             route: '/pricelist-export',  permission: 'pricelist-export' }
    ]
  },
  {
    title: 'User Management',
    cards: [
      { label: 'Users',         icon: 'pi-users',  route: '/users/list',   permission: 'users' },
      { label: 'Roles',         icon: 'pi-shield', route: '/roles/list',   permission: 'roles' },
      { label: 'Authorization', icon: 'pi-lock',   route: '/authorization', permission: 'authorization' }
    ]
  }
];

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="dashboard">
      <div class="welcome">
        <h1>Welcome, {{ auth.user()?.firstName }}</h1>
        <p>Pick up where you left off, or jump into a module below.</p>
      </div>

      @for (group of visibleGroups(); track group.title) {
        <div class="group">
          <div class="group-title">{{ group.title }}</div>
          <div class="card-grid">
            @for (card of group.cards; track card.route) {
              <a class="card" [routerLink]="card.route">
                <i class="pi" [class]="card.icon"></i>
                <span>{{ card.label }}</span>
              </a>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .dashboard {
      padding: 1.5rem 2rem;
      max-width: 1100px;
    }

    .welcome h1 {
      margin: 0 0 0.25rem;
      font-size: 1.4rem;
      color: #333;
    }

    .welcome p {
      margin: 0 0 1.75rem;
      color: #777;
      font-size: 0.9rem;
    }

    .group { margin-bottom: 1.75rem; }

    .group-title {
      font-size: 0.7rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: #999;
      margin-bottom: 0.6rem;
    }

    .card-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
      gap: 0.75rem;
    }

    .card {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.6rem;
      padding: 1rem;
      background: #fff;
      border: 1px solid #eee;
      border-radius: 6px;
      text-decoration: none;
      color: #333;
      transition: border-color 0.15s, box-shadow 0.15s;
    }

    .card:hover {
      border-color: #800000;
      box-shadow: 0 2px 8px rgba(128,0,0,0.1);
    }

    .card i {
      font-size: 1.3rem;
      color: #800000;
    }

    .card span {
      font-size: 0.85rem;
      font-weight: 600;
    }
  `]
})
export class DashboardPageComponent {
  protected auth = inject(AuthService);

  protected visibleGroups = computed<DashboardGroup[]>(() => {
    const isAdmin = this.auth.isAdmin();
    return GROUPS
      .map(group => ({
        title: group.title,
        cards: group.cards.filter(c => isAdmin || (c.permission && this.auth.hasPermission(c.permission)))
      }))
      .filter(group => group.cards.length > 0);
  });
}
