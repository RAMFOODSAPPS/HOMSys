import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';

export interface AppNotification {
  id: number;
  title: string;
  message: string;
  link: string;
  entityType: string;
  entityId: number | null;
  createdAt: string;
  isRead: boolean;
}

interface NotificationFeed { unreadCount: number; items: AppNotification[]; }

const POLL_MS = 60_000;

/** Topbar bell feed. Polls while a user is signed in; refresh() after any action that may notify. */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/notifications`;
  private timer?: ReturnType<typeof setInterval>;

  readonly items  = signal<AppNotification[]>([]);
  readonly unread = signal(0);

  start() {
    if (this.timer) return;
    this.refresh();
    this.timer = setInterval(() => { if (!document.hidden) this.refresh(); }, POLL_MS);
  }

  stop() {
    clearInterval(this.timer);
    this.timer = undefined;
  }

  refresh() {
    this.http.get<ApiResponse<NotificationFeed>>(this.base).subscribe({
      next: res => {
        this.items.set(res.data?.items ?? []);
        this.unread.set(res.data?.unreadCount ?? 0);
      },
      error: () => { /* offline / logged out — try again next tick */ }
    });
  }

  markRead(n: AppNotification) {
    if (n.isRead) return;
    this.items.update(list => list.map(x => x.id === n.id ? { ...x, isRead: true } : x));
    this.unread.update(c => Math.max(0, c - 1));
    this.http.post(`${this.base}/${n.id}/read`, {}).subscribe();
  }

  markAllRead() {
    this.items.update(list => list.map(x => ({ ...x, isRead: true })));
    this.unread.set(0);
    this.http.post(`${this.base}/read-all`, {}).subscribe();
  }
}
