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
  cancel?:  ToolbarHandler;
  add?:     ToolbarHandler;
  edit?:    ToolbarHandler;
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
