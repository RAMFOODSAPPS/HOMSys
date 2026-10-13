/**
 * Form Designer — admin-arranged form layouts on a 12-column snap grid.
 * A page declares its fields (FormFieldDef, with the built-in default placement);
 * the saved layout (FormLayoutItem[]) only overrides position/size/caption/hidden/tab order.
 */
export const GRID_COLS = 12;

export interface FormFieldDef {
  key: string;
  /** Default caption. */
  label: string;
  /** Designer-only explanation, e.g. when a field shows. */
  hint?: string;
  /** field = label + input; block = a whole section (table); note = help text. */
  kind: 'field' | 'block' | 'note';
  /** Needed to save/submit — can't be hidden. */
  required?: boolean;
  minW?: number;
  /** Rows the item may span (multi-line inputs). Defaults to 1..1. */
  minH?: number;
  maxH?: number;
  /** Default placement (1-based col/row). */
  col: number; row: number; w: number; h: number;
}

export interface FormLayoutItem {
  key: string;
  col: number; row: number; w: number; h: number;
  /** Keyboard Tab order (1 = first). Items render in this order, so Tab follows it. */
  tab: number;
  /** Custom caption; empty = the default. */
  label?: string;
  hidden?: boolean;
}

export interface FormLayout { version: 1; items: FormLayoutItem[]; }

const clamp = (v: number, lo: number, hi: number) => Math.min(hi, Math.max(lo, v));

export function minW(d: FormFieldDef) { return d.minW ?? 2; }
export function minH(d: FormFieldDef) { return d.minH ?? 1; }
export function maxH(d: FormFieldDef) { return Math.max(minH(d), d.maxH ?? d.h); }

/** Keep an item inside the grid and its def's size limits. */
export function clampItem(it: FormLayoutItem, d: FormFieldDef): FormLayoutItem {
  const w = clamp(Math.round(it.w), minW(d), GRID_COLS);
  return {
    ...it,
    w,
    h:   clamp(Math.round(it.h), minH(d), maxH(d)),
    col: clamp(Math.round(it.col), 1, GRID_COLS - w + 1),
    row: Math.max(1, Math.round(it.row)),
    hidden: d.required ? false : !!it.hidden,
  };
}

export function defaultItems(defs: FormFieldDef[]): FormLayoutItem[] {
  return byPositionTabs(defs.map(d => ({ key: d.key, col: d.col, row: d.row, w: d.w, h: d.h, tab: 0 })));
}

/**
 * Saved layout + current field defs → the items to render, sorted by tab order.
 * Unknown keys are dropped; fields added since the layout was saved go to the bottom.
 */
export function mergeLayout(defs: FormFieldDef[], saved?: FormLayout | null): FormLayoutItem[] {
  if (!saved?.items?.length) return defaultItems(defs);
  const byKey = new Map(saved.items.map(i => [i.key, i]));
  const out: FormLayoutItem[] = [];
  const added: FormFieldDef[] = [];
  for (const d of defs) {
    const s = byKey.get(d.key);
    if (s) out.push(clampItem({ ...s, label: (s.label ?? '').trim() || undefined }, d));
    else added.push(d);
  }
  let bottom = bottomRow(out);
  let tab = out.reduce((m, i) => Math.max(m, i.tab || 0), 0);
  for (const d of added) {
    out.push({ key: d.key, col: 1, row: bottom, w: d.w, h: d.h, tab: ++tab });
    bottom += d.h;
  }
  return resolveCollisions(sortByTab(out), null);
}

/** First free row below every visible item. */
export function bottomRow(items: FormLayoutItem[]) {
  return items.filter(i => !i.hidden).reduce((m, i) => Math.max(m, i.row + i.h), 1);
}

export function overlaps(a: FormLayoutItem, b: FormLayoutItem) {
  return a.col < b.col + b.w && b.col < a.col + a.w && a.row < b.row + b.h && b.row < a.row + a.h;
}

/**
 * Push overlapping items down (the item `pinnedKey` keeps its place). Returns new objects.
 */
export function resolveCollisions(items: FormLayoutItem[], pinnedKey: string | null): FormLayoutItem[] {
  const copy = items.map(i => ({ ...i }));
  const visible = copy.filter(i => !i.hidden);
  const pinned = visible.find(i => i.key === pinnedKey);
  const placed: FormLayoutItem[] = pinned ? [pinned] : [];
  const rest = visible.filter(i => i !== pinned).sort((a, b) => a.row - b.row || a.col - b.col);
  for (const it of rest) {
    let hit = placed.filter(p => overlaps(p, it));
    while (hit.length) {
      it.row = Math.max(...hit.map(p => p.row + p.h));
      hit = placed.filter(p => overlaps(p, it));
    }
    placed.push(it);
  }
  return copy;
}

export function sortByTab(items: FormLayoutItem[]) {
  return [...items].sort((a, b) => (a.tab || 9999) - (b.tab || 9999));
}

/** Tab order = reading order (row, then column). */
export function byPositionTabs(items: FormLayoutItem[]): FormLayoutItem[] {
  const order = [...items].sort((a, b) => a.row - b.row || a.col - b.col);
  return order.map((i, n) => ({ ...i, tab: n + 1 }));
}

/** Renumber tab 1..n keeping the current relative order. */
export function renumberTabs(items: FormLayoutItem[]): FormLayoutItem[] {
  return sortByTab(items).map((i, n) => ({ ...i, tab: n + 1 }));
}

/** CSS variables consumed by `.fl-cell` (see styles.scss). */
export function cellStyle(i: FormLayoutItem): Record<string, string> {
  return { '--c': `${i.col}`, '--r': `${i.row}`, '--w': `${i.w}`, '--h': `${i.h}` };
}
