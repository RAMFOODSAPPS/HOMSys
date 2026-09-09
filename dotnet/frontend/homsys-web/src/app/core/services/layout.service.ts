import { Injectable, signal } from '@angular/core';

const PINNED_KEY = 'homsys.sidebarPinned';

@Injectable({ providedIn: 'root' })
export class LayoutService {
  // A pinned/locked sidebar only makes sense in landscape, where it can sit
  // beside the content. In portrait there's no room for that — it would just
  // stay stuck open as a permanent overlay with no way to dismiss it — so
  // pinning is disabled entirely there, regardless of what was saved from a
  // previous landscape session.
  private portraitQuery = window.matchMedia('(orientation: portrait)');
  isPortrait = signal(this.portraitQuery.matches);

  sidebarPinned  = signal(localStorage.getItem(PINNED_KEY) === 'true');
  sidebarVisible = signal(this.sidebarPinned() && !this.isPortrait());

  constructor() {
    this.portraitQuery.addEventListener('change', e => {
      this.isPortrait.set(e.matches);
      if (e.matches) this.sidebarVisible.set(false);
    });
  }

  private isLocked(): boolean {
    return this.sidebarPinned() && !this.isPortrait();
  }

  toggleSidebar() { if (!this.isLocked()) this.sidebarVisible.update(v => !v); }

  togglePin() {
    if (this.isPortrait()) return;
    this.sidebarPinned.update(v => !v);
    this.sidebarVisible.set(this.sidebarPinned());
    localStorage.setItem(PINNED_KEY, String(this.sidebarPinned()));
  }

  closeIfUnpinned() {
    if (!this.isLocked()) {
      this.sidebarVisible.set(false);
    }
  }
}
