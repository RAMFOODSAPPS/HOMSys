import { Injectable, inject, signal, effect } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';

export interface AppTab {
  key:    string;
  label:  string;
  route:  string;
  icon:   string;
  state?: Record<string, unknown>;
}

const ROUTE_META: Record<string, { label: string; icon: string }> = {
  '/users':         { label: 'New User',        icon: 'pi-user-plus' },
  '/users/list':    { label: 'Users List',      icon: 'pi-list'      },
  '/roles':         { label: 'New Role',        icon: 'pi-shield'    },
  '/roles/list':    { label: 'Roles List',      icon: 'pi-list'      },
  '/authorization': { label: 'Authorization',   icon: 'pi-lock'      },
  '/companies':         { label: 'New Company',      icon: 'pi-building' },
  '/companies/list':    { label: 'Companies List',   icon: 'pi-list'     },
  '/departments':       { label: 'New Department',   icon: 'pi-sitemap'  },
  '/departments/list':  { label: 'Departments List', icon: 'pi-list'     },
  '/sites':             { label: 'New Site',         icon: 'pi-map-marker' },
  '/sites/list':        { label: 'Sites List',       icon: 'pi-list'     },
  '/site-types':        { label: 'New Site Type',    icon: 'pi-tag'      },
  '/site-types/list':   { label: 'Site Types List',  icon: 'pi-list'     },
  '/sales-orders':      { label: 'New Sales Order',  icon: 'pi-file-edit' },
  '/sales-orders/list': { label: 'Sales Orders',     icon: 'pi-list'     },
  '/oos-report':        { label: 'OOS Report',       icon: 'pi-exclamation-triangle' },
  '/pricelist-export':  { label: 'Pricelist Export', icon: 'pi-file-export' },
  '/legacy-monitoring': { label: 'Legacy Monitoring', icon: 'pi-sync' },
};

const TABS_STORAGE_KEY   = 'homsys_tabs';
const ACTIVE_STORAGE_KEY = 'homsys_active_tab';

@Injectable({ providedIn: 'root' })
export class TabBarService {
  private router = inject(Router);

  tabs      = signal<AppTab[]>(this.loadTabs());
  activeKey = signal<string>(localStorage.getItem(ACTIVE_STORAGE_KEY) ?? '');

  constructor() {
    effect(() => localStorage.setItem(TABS_STORAGE_KEY,   JSON.stringify(this.tabs())));
    effect(() => localStorage.setItem(ACTIVE_STORAGE_KEY, this.activeKey()));

    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd)
    ).subscribe(e => {
      const url = e.urlAfterRedirects.split('?')[0];
      const meta = ROUTE_META[url];
      if (!meta) return;

      // If navigating with an entity state, the page component will call openTab explicitly
      const navState = history.state as Record<string, unknown>;
      if (navState?.['userId'] || navState?.['roleId'] || navState?.['companyId'] || navState?.['departmentId'] || navState?.['siteId'] || navState?.['siteTypeId'] || navState?.['soId'] || navState?.['draftKey']) return;

      const key = url;
      this.activeKey.set(key);
      if (!this.tabs().some(t => t.key === key)) {
        this.tabs.update(tabs => [...tabs, { key, label: meta.label, route: url, icon: meta.icon }]);
      }
    });
  }

  /** Persist arbitrary state onto an existing tab (e.g. draft form values). */
  updateTabState(key: string, state: Record<string, unknown> | undefined) {
    this.tabs.update(tabs => tabs.map(t => t.key === key ? { ...t, state } : t));
  }

  private dirtyCheckers = new Map<string, () => boolean>();

  registerDirtyChecker(key: string, fn: () => boolean) {
    this.dirtyCheckers.set(key, fn);
  }

  unregisterDirtyChecker(key: string) {
    this.dirtyCheckers.delete(key);
  }

  isTabDirty(key: string): boolean {
    return this.dirtyCheckers.get(key)?.() ?? false;
  }

  /** Open or activate a tab with a specific key (used for edit tabs). */
  openTab(tab: AppTab) {
    if (!this.tabs().some(t => t.key === tab.key)) {
      this.tabs.update(tabs => [...tabs, tab]);
    } else {
      this.tabs.update(tabs => tabs.map(t => t.key === tab.key ? { ...t, label: tab.label } : t));
    }
    this.activeKey.set(tab.key);
  }

  /** Replace the current active edit tab with a fresh default tab for the route (used on Cancel). */
  switchToDefaultTab(route: string) {
    const meta = ROUTE_META[route];
    if (!meta) return;
    const oldKey = this.activeKey();
    const defaultTab: AppTab = { key: route, label: meta.label, route, icon: meta.icon };

    const without = this.tabs().filter(t => t.key !== oldKey);
    if (without.some(t => t.key === route)) {
      this.tabs.set(without);
    } else {
      this.tabs.set([...without, defaultTab]);
    }
    this.activeKey.set(route);
  }

  private loadTabs(): AppTab[] {
    try {
      const raw = localStorage.getItem(TABS_STORAGE_KEY);
      return raw ? (JSON.parse(raw) as AppTab[]) : [];
    } catch {
      return [];
    }
  }

  closeTab(key: string) {
    const current = this.tabs();
    const idx = current.findIndex(t => t.key === key);
    if (idx === -1) return;

    const remaining = current.filter(t => t.key !== key);
    this.tabs.set(remaining);

    if (this.activeKey() === key && remaining.length) {
      const target = remaining[Math.min(idx, remaining.length - 1)];
      this.activeKey.set(target.key);
      this.router.navigate([target.route], target.state ? { state: target.state } : {});
    }
  }
}
