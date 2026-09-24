import { Component, DestroyRef, computed, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, switchMap } from 'rxjs';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { AnalyticsService } from '../../core/services/analytics.service';
import { DATE_PRESETS, DatasetMeta, FilterOp, SpecFilter } from '../../core/models/analytics.model';

const OP_LABELS: Record<FilterOp, string> = {
  preset: 'period', between: 'between', in: 'is any of', notIn: 'is not', eq: 'is', neq: 'is not',
  contains: 'contains', startsWith: 'starts with', gt: '>', gte: '≥', lt: '<', lte: '≤',
  isNull: 'is blank', notNull: 'is not blank', any: 'any',
};

/** One filter row: field label · operator · value editor · remove. */
@Component({
  selector: 'app-filter-editor',
  standalone: true,
  imports: [CommonModule, FormsModule, SelectModule, MultiSelectModule, ButtonModule, TooltipModule],
  template: `
    @if (field(); as f) {
      <div class="an-filter">
        <span class="an-filter-label" [pTooltip]="f.description ?? ''">{{ f.label }}</span>
        @if (ops().length > 1) {
          <p-select [options]="ops()" optionLabel="label" optionValue="value" [ngModel]="filter().op"
                    (ngModelChange)="setOp($event)" size="small" appendTo="body" styleClass="an-op" />
        }
        @switch (filter().op) {
          @case ('preset') {
            <p-select [options]="presets" optionLabel="label" optionValue="value" [ngModel]="filter().values?.[0]"
                      (ngModelChange)="setValues([$event])" size="small" appendTo="body" styleClass="an-val" />
          }
          @case ('in') { <ng-container *ngTemplateOutlet="multi" /> }
          @case ('notIn') { <ng-container *ngTemplateOutlet="multi" /> }
          @case ('eq') {
            @if (f.type === 'bool') {
              <p-select [options]="yesNo" optionLabel="label" optionValue="value" [ngModel]="filter().values?.[0]"
                        (ngModelChange)="setValues([$event])" size="small" appendTo="body" styleClass="an-val" />
            } @else {
              <input class="p-inputtext p-inputtext-sm an-val" [type]="inputType()" [ngModel]="filter().values?.[0]" (ngModelChange)="setValues([$event])" />
            }
          }
          @case ('between') {
            <input class="p-inputtext p-inputtext-sm an-val-half" [type]="inputType()" [ngModel]="filter().values?.[0]" (ngModelChange)="setAt(0, $event)" />
            <span class="an-muted">–</span>
            <input class="p-inputtext p-inputtext-sm an-val-half" [type]="inputType()" [ngModel]="filter().values?.[1]" (ngModelChange)="setAt(1, $event)" />
          }
          @case ('isNull') {}
          @case ('notNull') {}
          @case ('any') {}
          @default {
            <input class="p-inputtext p-inputtext-sm an-val" [type]="inputType()" [ngModel]="filter().values?.[0]" (ngModelChange)="setValues([$event])" />
          }
        }
        @if (removable()) {
          <p-button icon="pi pi-times" size="small" [text]="true" [rounded]="true" severity="secondary" (onClick)="remove.emit()" ariaLabel="Remove filter" />
        }
      </div>

      <ng-template #multi>
        <p-multiSelect [options]="options()" optionLabel="label" optionValue="value" [ngModel]="filter().values ?? []"
                       (ngModelChange)="setValues($event)" [filter]="true" (onFilter)="search$.next($event.filter ?? '')"
                       (onPanelShow)="search$.next('')" [placeholder]="placeholder()" [maxSelectedLabels]="2"
                       selectedItemsLabel="{0} selected" size="small" appendTo="body" styleClass="an-val"
                       [disabled]="locked()" [showClear]="!locked()" [loading]="loadingValues()" />
      </ng-template>
    }
  `,
})
export class FilterEditorComponent {
  private api = inject(AnalyticsService);

  readonly dataset = input.required<DatasetMeta>();
  readonly filter = model.required<SpecFilter>();
  readonly removable = input(true);
  readonly locked = input(false);
  readonly placeholder = input('All');
  readonly remove = output<void>();

  readonly presets = DATE_PRESETS;
  readonly yesNo = [{ label: 'Yes', value: 'true' }, { label: 'No', value: 'false' }];
  readonly search$ = new Subject<string>();
  readonly loadingValues = signal(false);
  private readonly loaded = signal<{ label: string; value: string | null }[]>([]);

  readonly field = computed(() => this.dataset().fields.find(f => f.key === this.filter().field));

  readonly ops = computed(() => {
    const f = this.field();
    let ops: FilterOp[];
    if (!f) ops = [];
    else if (f.kind === 'date') ops = ['preset', 'between', 'gte', 'lte', 'isNull', 'notNull'];
    else if (f.kind === 'measure') ops = ['gt', 'gte', 'lt', 'lte', 'between', 'isNull', 'notNull'];
    else if (f.type === 'bool') ops = ['eq'];
    else if (f.type === 'int') ops = ['in', 'notIn', 'gte', 'lte', 'between', 'isNull', 'notNull'];
    else ops = ['in', 'notIn', 'contains', 'startsWith', 'isNull', 'notNull'];
    return ops.map(o => ({ value: o, label: OP_LABELS[o] }));
  });

  readonly inputType = computed(() => this.field()?.kind === 'date' ? 'date' : this.field()?.type === 'string' ? 'text' : 'number');

  /** Loaded values plus any already-selected ones (so a saved selection always renders). */
  readonly options = computed(() => {
    const opts = this.loaded();
    const extra = (this.filter().values ?? []).filter(v => !opts.some(o => o.value === v)).map(v => ({ label: v ?? '(blank)', value: v }));
    return [...extra, ...opts];
  });

  constructor() {
    this.search$.pipe(
      debounceTime(300),
      switchMap(q => { this.loadingValues.set(true); return this.api.values(this.dataset().key, this.filter().field, q, 200); }),
      takeUntilDestroyed(inject(DestroyRef)),
    ).subscribe({
      next: vals => {
        this.loadingValues.set(false);
        this.loaded.set(vals.map(v => ({
          value: v.value,
          label: v.value === null ? '(blank)' : v.caption && v.caption !== v.value ? `${v.caption} (${v.value})` : v.value,
        })));
      },
      error: () => this.loadingValues.set(false),
    });
  }

  setOp(op: FilterOp) {
    const keep = (['in', 'notIn'].includes(op) && ['in', 'notIn'].includes(this.filter().op)) ? this.filter().values : [];
    this.filter.set({ ...this.filter(), op, values: op === 'eq' && this.field()?.type === 'bool' ? ['true'] : keep });
  }

  setValues(values: (string | null)[]) {
    this.filter.set({ ...this.filter(), values: values.map(v => v === '' || v === undefined ? null : String(v)) });
  }

  setAt(i: number, v: string) {
    const vals = [...(this.filter().values ?? [null, null])];
    vals[i] = v === '' ? null : String(v);
    this.filter.set({ ...this.filter(), values: vals });
  }
}

/** A fresh filter for a field with a sensible default operator. */
export function defaultFilter(ds: DatasetMeta, fieldKey: string): SpecFilter {
  const f = ds.fields.find(x => x.key === fieldKey);
  if (f?.kind === 'date') return { field: fieldKey, op: 'preset', values: ['mtd'] };
  if (f?.kind === 'measure') return { field: fieldKey, op: 'gt', values: ['0'] };
  if (f?.type === 'bool') return { field: fieldKey, op: 'eq', values: ['true'] };
  return { field: fieldKey, op: 'in', values: [] };
}
