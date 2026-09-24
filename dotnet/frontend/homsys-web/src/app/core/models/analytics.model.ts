// Data Analytics — mirrors backend HOMSys.Application.DTOs.Analytics.

export type Visual = 'table' | 'pivot' | 'kpi' | 'bar' | 'line' | 'pie' | 'doughnut';
export type Bucket = 'day' | 'week' | 'month' | 'quarter' | 'year';
export type FilterOp = 'eq' | 'neq' | 'in' | 'notIn' | 'gt' | 'gte' | 'lt' | 'lte' | 'between'
  | 'contains' | 'startsWith' | 'isNull' | 'notNull' | 'preset' | 'any';

export interface SpecDim { field: string; bucket?: Bucket | null; }
export interface SpecMeasure { field: string; agg?: string | null; label?: string | null; }
export interface SpecFilter { field: string; op: FilterOp; values?: (string | null)[]; }
export interface SpecSort { by: string; dir: 'asc' | 'desc'; }

export interface ReportSpec {
  v: 1;
  dataset: string;
  type: Visual;
  stacked?: boolean;
  horizontal?: boolean;
  dimensions: SpecDim[];
  measures: SpecMeasure[];
  filters: SpecFilter[];
  sort?: SpecSort | null;
  limit?: number | null;
  others?: boolean;
  fill?: boolean;
  compare?: 'previousPeriod' | null;
}

export interface ColumnMeta {
  key: string; field: string | null; label: string; type: string;
  role: 'dim' | 'caption' | 'measure'; format?: string | null; additive: boolean; of?: string | null; bucket?: Bucket | null;
}

export interface QueryResult {
  columns: ColumnMeta[];
  rows: unknown[][];
  othersRows: unknown[][];
  total: unknown[] | null;
  rowTotals: unknown[][] | null;
  colTotals: unknown[][] | null;
  compare: unknown[] | null;
  compareLabel: string | null;
  truncated: boolean;
  asOf: string | null;
  scope: string | null;
}

export interface FieldMeta {
  key: string; label: string; kind: 'dim' | 'date' | 'measure'; type: string;
  aggs: string[]; defaultAgg: string | null; format: string | null; hasCaption: boolean;
  drillTo: string | null; description: string | null; additive: boolean;
}

export interface DatasetMeta {
  key: string; label: string; grain: string; defaultDate: string | null; heavy: boolean; national: boolean;
  fields: FieldMeta[]; detail: string[]; defaults: SpecFilter[]; permission: string | null;
}

export interface AnalyticsMeta {
  datasets: DatasetMeta[];
  roles: { id: number; name: string }[];
  scope: string | null;
  today: string;
  canPublish: boolean;
}

export interface ValueOption { value: string | null; caption: string | null; }

export interface DashboardWidget {
  id: string; title: string; w: number; h: number; spec: ReportSpec; sourceItemId?: number | null;
}

export interface DashboardDef { v: 1; filters: SpecFilter[]; widgets: DashboardWidget[]; }

export interface SavedItem {
  id: number; kind: 'report' | 'dashboard'; name: string; description: string;
  datasetKey: string | null; visual: Visual | null; ownerName: string;
  isOwner: boolean; canEdit: boolean; isSystem: boolean; sharedRoleIds: number[];
  updatedAt: string | null; definition?: string | null;
}

export interface SaveItemRequest {
  kind: 'report' | 'dashboard'; name: string; description: string; definition: string;
  sharedRoleIds: number[]; isSystem: boolean;
}

export const DATE_PRESETS: { value: string; label: string }[] = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'last7', label: 'Last 7 days' },
  { value: 'last30', label: 'Last 30 days' },
  { value: 'mtd', label: 'Month to date' },
  { value: 'lastMonth', label: 'Last month' },
  { value: 'qtd', label: 'Quarter to date' },
  { value: 'ytd', label: 'Year to date' },
  { value: 'last12m', label: 'Last 12 months' },
];

export const VISUALS: { value: Visual; label: string; icon: string }[] = [
  { value: 'table', label: 'Table', icon: 'pi pi-table' },
  { value: 'pivot', label: 'Matrix', icon: 'pi pi-th-large' },
  { value: 'kpi', label: 'KPI card', icon: 'pi pi-hashtag' },
  { value: 'bar', label: 'Bar', icon: 'pi pi-chart-bar' },
  { value: 'line', label: 'Line', icon: 'pi pi-chart-line' },
  { value: 'pie', label: 'Pie', icon: 'pi pi-chart-pie' },
  { value: 'doughnut', label: 'Doughnut', icon: 'pi pi-circle' },
];

export function emptySpec(dataset: string): ReportSpec {
  return { v: 1, dataset, type: 'table', dimensions: [], measures: [], filters: [] };
}
