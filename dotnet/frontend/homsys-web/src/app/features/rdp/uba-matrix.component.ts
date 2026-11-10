import { Component, computed, input, output, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { UbaSplit, UbaSplitRow } from '../../core/services/rdp.service';

export type UbaSort = 'high' | 'low' | 'name';

interface Node { row: UbaSplitRow; path: string; children: Node[]; }
interface Line { row: UbaSplitRow; path: string; hasKids: boolean; open: boolean; }

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
/** "2026-09" → "Sep 26". */
export const monthLabel = (p: string) => `${MONTHS[+p.slice(5, 7) - 1]} ${p.slice(2, 4)}`;

/**
 * Month × row matrix for the UBA dashboard (distributor leaderboard, channel table, split explorer):
 * heat-shaded month cells, Range, share and like-for-like change vs the prior period.
 * With several group-by levels, rows expand to their next level.
 */
@Component({
  selector: 'app-uba-matrix',
  standalone: true,
  imports: [DecimalPipe],
  template: `
    @if (split(); as s) {
      <div class="wrap">
        <table>
          <thead><tr>
            <th class="lbl">{{ title() }}</th>
            @if (owner()) { <th>Area</th><th>RDM</th> }
            @for (p of s.periods; track p) { <th class="n">{{ month(p) }}</th> }
            <th class="n">Range</th><th class="n">Share</th><th class="n">vs prior</th>
          </tr></thead>
          <tbody>
            @for (l of lines(); track l.path) {
              <tr [class]="'lv' + l.row.level" [class.pick]="selectable() && l.row.level === 1"
                  [class.sel]="l.row.level === 1 && selected().includes(l.row.keys[0])"
                  (click)="selectable() && l.row.level === 1 && pick.emit(l.row)">
                <td class="lbl" [style.padding-left.rem]="0.4 + (l.row.level - 1) * 1.1">
                  @if (l.hasKids) {
                    <button class="tw" (click)="toggle(l.path)" [attr.aria-label]="l.open ? 'Collapse' : 'Expand'">
                      <i class="pi" [class.pi-chevron-down]="l.open" [class.pi-chevron-right]="!l.open"></i></button>
                  }{{ l.row.label }}</td>
                @if (owner()) { <td>{{ l.row.area }}</td><td>{{ l.row.rdm }}</td> }
                @for (v of l.row.months; track $index) {
                  <td class="n" [style.background]="heat(v, l.row.level)">{{ v === null ? '–' : (v | number:fmt()) }}</td>
                }
                <td class="n b">{{ l.row.range | number:fmt() }}</td>
                <td class="n">{{ l.row.share | number:'1.1-1' }}%</td>
                <td class="n" [class.up]="(l.row.changePct ?? 0) > 0" [class.dn]="(l.row.changePct ?? 0) < 0">{{ change(l.row.changePct) }}</td>
              </tr>
            } @empty {
              <tr><td [attr.colspan]="s.periods.length + (owner() ? 6 : 4)" class="empty">No data for this selection.</td></tr>
            }
          </tbody>
          <tfoot><tr>
            <td class="lbl">Total</td>
            @if (owner()) { <td></td><td></td> }
            @for (v of s.total.months; track $index) { <td class="n">{{ v === null ? '–' : (v | number:fmt()) }}</td> }
            <td class="n">{{ s.total.range | number:fmt() }}</td><td class="n">100%</td>
            <td class="n" [class.up]="(s.total.changePct ?? 0) > 0" [class.dn]="(s.total.changePct ?? 0) < 0">{{ change(s.total.changePct) }}</td>
          </tr></tfoot>
        </table>
      </div>
      @if (s.note) { <div class="note">{{ s.note }}</div> }
    }
  `,
  styles: [`
    .wrap { overflow: auto; max-height: var(--uba-max-h, none); border: 1px solid var(--p-surface-border); border-radius: 6px; }
    table { border-collapse: collapse; width: 100%; font-size: .8rem; }
    th, td { padding: .32rem .5rem; border-bottom: 1px solid var(--p-surface-border); white-space: nowrap; }
    thead th { position: sticky; top: 0; background: var(--p-surface-50, #f8f9fa); z-index: 1; font-weight: 600; color: var(--p-text-muted-color); }
    tfoot td { position: sticky; bottom: 0; background: var(--p-surface-50, #f8f9fa); font-weight: 700; }
    .lbl { text-align: left; position: sticky; left: 0; background: var(--p-surface-0, #fff); z-index: 1; max-width: 26rem; overflow: hidden; text-overflow: ellipsis; }
    thead .lbl, tfoot .lbl { z-index: 2; background: var(--p-surface-50, #f8f9fa); }
    .n { text-align: right; font-variant-numeric: tabular-nums; }
    .b, .lv1 .lbl { font-weight: 600; }
    .lv2 .lbl, .lv3 .lbl { font-weight: 400; }
    .tw { border: 0; background: none; cursor: pointer; padding: 0 .3rem 0 0; color: var(--p-text-muted-color); }
    .tw .pi { font-size: .7rem; }
    .up { color: #0f7b4f; } .dn { color: #b42318; }
    .empty { text-align: center; color: var(--p-text-muted-color); padding: 1rem; }
    .note { font-size: .75rem; color: var(--p-text-muted-color); margin-top: .35rem; }
    tr.pick { cursor: pointer; }
    tr.pick:hover td { box-shadow: inset 0 0 0 999px rgba(128, 0, 0, .05); }
    tr.sel td { box-shadow: inset 0 0 0 999px rgba(128, 0, 0, .1); }
    tr.sel .lbl { box-shadow: inset 3px 0 0 #800000, inset 0 0 0 999px rgba(128, 0, 0, .1); color: #800000; }
  `]
})
export class UbaMatrixComponent {
  split = input<UbaSplit | null>(null);
  title = input('');
  /** Show Area + RDM columns (rows are distributors). */
  owner = input(false);
  sort = input<UbaSort>('high');
  /** Hide rows whose range is below this. */
  minRange = input(0);
  search = input('');
  /** Top-level rows are clickable (they filter the dashboard); `selected` = their keys that are picked. */
  selectable = input(false);
  selected = input<string[]>([]);
  pick = output<UbaSplitRow>();

  private expanded = signal(new Set<string>());

  protected fmt = computed(() => this.split()?.metric === 'sales' ? '1.0-0' : this.split()?.metric === 'cases' ? '1.0-1' : '1.0-0');
  protected month = monthLabel;

  private tree = computed<Node[]>(() => {
    const roots: Node[] = [];
    const stack: Node[] = [];
    for (const row of this.split()?.rows ?? []) {
      const node: Node = { row, path: row.keys.join('\u001f'), children: [] };
      stack.length = row.level - 1;
      (row.level === 1 ? roots : stack[row.level - 2]?.children ?? roots).push(node);
      stack[row.level - 1] = node;
    }
    return roots;
  });

  /** Max month value per level, for the heat shading. */
  private maxByLevel = computed(() => {
    const max: number[] = [];
    for (const r of this.split()?.rows ?? [])
      for (const v of r.months) if (v !== null && v > (max[r.level] ?? 0)) max[r.level] = v;
    return max;
  });

  protected lines = computed<Line[]>(() => {
    const term = this.search().trim().toLowerCase();
    const min = this.minRange() || 0;
    const open = this.expanded();
    const sort = this.sort();
    const cmp = (a: Node, b: Node) =>
      sort === 'name' ? a.row.label.localeCompare(b.row.label)
      : sort === 'low' ? a.row.range - b.row.range : b.row.range - a.row.range;
    const matches = (n: Node): boolean => !term || n.row.label.toLowerCase().includes(term) || n.children.some(matches);
    const out: Line[] = [];
    const walk = (nodes: Node[]) => {
      for (const n of [...nodes].sort(cmp)) {
        if (n.row.range < min || !matches(n)) continue;
        // A search opens the way to its matches.
        const isOpen = open.has(n.path) || (!!term && n.children.some(matches) && !n.row.label.toLowerCase().includes(term));
        out.push({ row: n.row, path: n.path, hasKids: n.children.length > 0, open: isOpen });
        if (isOpen) walk(n.children);
      }
    };
    walk(this.tree());
    return out;
  });

  toggle(path: string) {
    const next = new Set(this.expanded());
    next.has(path) ? next.delete(path) : next.add(path);
    this.expanded.set(next);
  }

  expandAll() {
    this.expanded.set(new Set((this.split()?.rows ?? []).map(r => r.keys.join('\u001f'))));
  }

  collapseAll() { this.expanded.set(new Set()); }

  /** Visible rows as tab-separated text (pastes into Excel). */
  asText(): string {
    const s = this.split();
    if (!s) return '';
    const head = [this.title(), ...(this.owner() ? ['Area', 'RDM'] : []), ...s.periods.map(monthLabel), 'Range', 'Share %', 'vs prior %'];
    const row = (r: UbaSplitRow, label: string) => [label, ...(this.owner() ? [r.area ?? '', r.rdm ?? ''] : []),
      ...r.months.map(v => v ?? ''), r.range, r.share, r.changePct ?? ''].join('\t');
    return [head.join('\t'), ...this.lines().map(l => row(l.row, '  '.repeat(l.row.level - 1) + l.row.label)), row(s.total, 'Total')].join('\n');
  }

  protected heat(v: number | null, level: number) {
    const max = this.maxByLevel()[level] ?? 0;
    if (v === null || v <= 0 || !max) return null;
    return `rgba(128, 0, 0, ${(0.04 + 0.3 * v / max).toFixed(3)})`;
  }

  protected change(pct: number | null) {
    return pct === null ? '–' : `${pct > 0 ? '▲' : pct < 0 ? '▼' : ''} ${Math.abs(pct).toFixed(1)}%`;
  }
}

