import { Component, inject, OnInit, signal } from '@angular/core';
import { AnalyticsService } from '../../core/services/analytics.service';
import { AnalyticsDashboardComponent } from '../analytics/analytics-dashboard.component';

/**
 * Collections Dashboard page: the Finance Manager's "Collections Overview" (a shared Data Analytics dashboard, so it
 * stays customisable there) shown in place, like Home shows its dashboard. Uses the newest one with that name the user can see.
 */
@Component({
  selector: 'app-collection-dashboard-redirect',
  standalone: true,
  imports: [AnalyticsDashboardComponent],
  template: `
    @if (dashboardId(); as id) { <app-analytics-dashboard [homeMode]="true" [embedId]="id" /> }
    @else { <div class="muted">{{ message() }}</div> }
  `,
  styles: [`.muted { color: var(--p-text-muted-color); padding: 1rem; }`]
})
export class CollectionDashboardRedirectComponent implements OnInit {
  private analytics = inject(AnalyticsService);
  dashboardId = signal<number | null>(null);
  message = signal('Loading the Collections dashboard…');

  ngOnInit() {
    this.analytics.items().subscribe({
      next: items => {
        const d = items.filter(i => i.kind === 'dashboard' && i.name.startsWith('Collections Overview')).sort((a, b) => b.id - a.id)[0];
        if (d) this.dashboardId.set(d.id);
        else this.message.set('No "Collections Overview" dashboard is shared with you — ask the admin to share it with your role.');
      },
      error: () => this.message.set('Could not load dashboards.'),
    });
  }
}