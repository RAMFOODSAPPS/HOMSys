import { Injectable, signal } from '@angular/core';

const PINNED_KEY = 'homsys.sidebarPinned';

@Injectable({ providedIn: 'root' })
export class LayoutService {
  sidebarPinned  = signal(localStorage.getItem(PINNED_KEY) === 'true');
  sidebarVisible = signal(this.sidebarPinned());

  toggleSidebar() { if (!this.sidebarPinned()) this.sidebarVisible.update(v => !v); }

  togglePin() {
    this.sidebarPinned.update(v => !v);
    this.sidebarVisible.set(this.sidebarPinned());
    localStorage.setItem(PINNED_KEY, String(this.sidebarPinned()));
  }

  closeIfUnpinned() {
    if (!this.sidebarPinned()) {
      this.sidebarVisible.set(false);
    }
  }
}
