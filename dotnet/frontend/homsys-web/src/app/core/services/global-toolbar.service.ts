import { Injectable, Signal, signal } from '@angular/core';

export interface ToolbarHandler {
  onClick: () => void;
  disabled?: Signal<boolean>;
  loading?:  Signal<boolean>;
}

export interface ExportMenuItem {
  label: string;
  icon?: string;
  command: () => void;
}

export interface ExportConfig {
  pdf?:   () => void;
  excel?: () => void;
  csv?:   () => void;
  items?: ExportMenuItem[];
  disabled?: Signal<boolean>;
}

export interface ToolbarConfig {
  title?:   string;
  save?:    ToolbarHandler;
  /** Workflow submit (send icon next to Save); label is the tooltip, e.g. TASAF "Submit" / "Resubmit". */
  submit?:  ToolbarHandler & { label: string };
  /** Workflow decisions for the current approver (tooltip = label). */
  approve?: ToolbarHandler & { label: string };
  return?:  ToolbarHandler;
  reject?:  ToolbarHandler;
  /** Admin: undo an approval (back to the proponent). */
  revert?:  ToolbarHandler;
  /** Withdraw the document (kept, status Cancelled) — not the same as `cancel` (discard edits). */
  cancelDoc?: ToolbarHandler & { label: string };
  cancel?:  ToolbarHandler;
  /** Empty the form's fields (the document stays open; nothing is saved). */
  clearForm?: ToolbarHandler;
  add?:     ToolbarHandler;
  edit?:    ToolbarHandler;
  /** Document history (e.g. TASAF approval history) — the page opens its own dialog. */
  history?: ToolbarHandler;
  delete?:  ToolbarHandler;
  refresh?: ToolbarHandler;
  list?:    ToolbarHandler;
  find?:         ToolbarHandler;
  search?:       (term: string) => void;
  initialSearch?: string;
  print?:  ToolbarHandler;
  export?: ExportConfig;
  import?: ToolbarHandler;
  importByName?: ToolbarHandler;
  /** Form Designer — pages only set this for Admins. */
  design?: ToolbarHandler;
}

@Injectable({ providedIn: 'root' })
export class GlobalToolbarService {
  private _config = signal<ToolbarConfig>({});

  readonly config = this._config.asReadonly();

  set(config: ToolbarConfig) {
    this._config.set(config);
  }

  clear() {
    this._config.set({});
  }
}
