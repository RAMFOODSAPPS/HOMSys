/**
 * How the Authorization page groups the permissions (Permissions table keys). A permission is a
 * "page" (it opens pages — listed as chips) or an "action" (a switch inside a page, shown in the
 * right-hand Actions panel). A key missing here still shows, under "Other" — add it when a module
 * gets a new permission.
 */
export interface AuthPermDef {
  key: string;
  icon: string;
  /** Pages this permission opens (ROUTE_META labels). */
  pages?: string[];
}

export interface AuthModule {
  key: string;
  label: string;
  icon: string;
  pages: AuthPermDef[];
  actions?: AuthPermDef[];
}

export const AUTH_MODULES: AuthModule[] = [
  {
    key: 'admin', label: 'Administration', icon: 'pi-shield',
    pages: [
      { key: 'users',         icon: 'pi-user',       pages: ['New User', 'Users List'] },
      { key: 'roles',         icon: 'pi-shield',     pages: ['New Role', 'Roles List'] },
      { key: 'authorization', icon: 'pi-lock',       pages: ['Authorization'] },
      { key: 'companies',     icon: 'pi-building',   pages: ['New Company', 'Companies List'] },
      { key: 'departments',   icon: 'pi-sitemap',    pages: ['New Department', 'Departments List'] },
      { key: 'sites',         icon: 'pi-map-marker', pages: ['New Site', 'Sites List'] },
      { key: 'site-types',    icon: 'pi-tag',        pages: ['New Site Type', 'Site Types List'] },
    ],
  },
  {
    key: 'sales', label: 'Sales', icon: 'pi-shopping-cart',
    pages: [
      { key: 'sales-orders',      icon: 'pi-file-edit',            pages: ['New Sales Order', 'Sales Orders'] },
      { key: 'oos-report',        icon: 'pi-exclamation-triangle', pages: ['OOS Report'] },
      { key: 'pricelist-export',  icon: 'pi-file-export',          pages: ['Pricelist Export'] },
      { key: 'legacy-monitoring', icon: 'pi-sync',                 pages: ['Legacy Monitoring'] },
    ],
    actions: [
      { key: 'offshore-encode',       icon: 'pi-globe' },
      { key: 'pricelist-zone-export', icon: 'pi-map' },
    ],
  },
  {
    key: 'trade', label: 'Trade Marketing', icon: 'pi-megaphone',
    pages: [
      { key: 'tasaf',         icon: 'pi-file-check', pages: ['New TASAF', 'TASAF List'] },
      { key: 'tasaf-masters', icon: 'pi-cog',        pages: ['TASAF Setup', 'Product Categories'] },
    ],
    actions: [
      { key: 'tasaf-view-all', icon: 'pi-eye' },
    ],
  },
  {
    key: 'collections', label: 'Collections & PDC', icon: 'pi-wallet',
    pages: [
      { key: 'collections-encode',    icon: 'pi-wallet',        pages: ['New Collection', 'Collections'] },
      { key: 'collections-verify',    icon: 'pi-check-square',  pages: ['Verification Queue'] },
      { key: 'collections-manage',    icon: 'pi-building-columns', pages: ['Bank Statements', 'Collections Dashboard'] },
      { key: 'collections-ra-encode', icon: 'pi-file-import',   pages: ['Remittance Advice'] },
      { key: 'pdc-view',              icon: 'pi-calendar-clock', pages: ['PDC Monitor'] },
    ],
    actions: [
      { key: 'pdc-tag',     icon: 'pi-tag' },
      { key: 'pdc-confirm', icon: 'pi-verified' },
    ],
  },
  {
    key: 'masters', label: 'Master Data', icon: 'pi-database',
    pages: [
      { key: 'products',  icon: 'pi-box',     pages: ['Products'] },
      { key: 'customers', icon: 'pi-id-card', pages: ['Customers'] },
    ],
  },
  {
    key: 'analytics', label: 'Analytics', icon: 'pi-chart-bar',
    pages: [
      { key: 'data-analytics', icon: 'pi-chart-bar', pages: ['Data Analytics', 'New Report', 'New Dashboard'] },
    ],
    actions: [
      { key: 'data-analytics-admin', icon: 'pi-star' },
    ],
  },
];
