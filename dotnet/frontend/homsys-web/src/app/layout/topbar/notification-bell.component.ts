import { Component, inject, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { Popover, PopoverModule } from 'primeng/popover';
import { AppNotification, NotificationService } from '../../core/services/notification.service';
import { utc } from '../../core/utils/utc-date';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [PopoverModule, DatePipe],
  template: `
    <button class="bell" (click)="toggle($event)" [attr.aria-label]="'Notifications, ' + svc.unread() + ' unread'">
      <i class="pi pi-bell"></i>
      @if (svc.unread() > 0) { <span class="badge">{{ svc.unread() > 99 ? '99+' : svc.unread() }}</span> }
    </button>

    <p-popover #pop styleClass="notif-pop">
      <div class="head">
        <strong>Notifications</strong>
        @if (svc.unread() > 0) { <button class="link" (click)="svc.markAllRead()">Mark all read</button> }
      </div>
      <div class="list">
        @for (n of svc.items(); track n.id) {
          <button class="item" [class.unread]="!n.isRead" (click)="open(n)">
            <span class="title">{{ n.title }}</span>
            <span class="msg">{{ n.message }}</span>
            <span class="when">{{ utc(n.createdAt) | date:'dd MMM, h:mm a' }}</span>
          </button>
        } @empty {
          <div class="empty">No notifications yet.</div>
        }
      </div>
    </p-popover>
  `,
  styles: [`
    .bell { position: relative; background: none; border: 0; color: #fff; cursor: pointer;
      width: 34px; height: 34px; border-radius: 50%; margin-right: 0.5rem; }
    .bell:hover { background: rgba(255, 255, 255, 0.12); }
    .bell .pi { font-size: 1.05rem; }
    .badge { position: absolute; top: 2px; right: 0; min-width: 17px; height: 17px; padding: 0 4px;
      border-radius: 9px; background: #fff; color: #800000; font-size: 0.65rem; font-weight: 700; line-height: 17px; }
    .head { display: flex; justify-content: space-between; align-items: center; padding-bottom: 0.4rem;
      border-bottom: 1px solid var(--p-surface-border); }
    .link { background: none; border: 0; color: #800000; cursor: pointer; font-size: 0.78rem; }
    .list { width: 340px; max-height: 420px; overflow-y: auto; }
    .item { display: flex; flex-direction: column; gap: 0.1rem; width: 100%; text-align: left; background: none;
      border: 0; border-bottom: 1px solid var(--p-surface-border); padding: 0.55rem 0.4rem; cursor: pointer; color: inherit; }
    .item:hover { background: var(--p-surface-hover); }
    .item.unread { background: rgba(128, 0, 0, 0.05); }
    .item.unread .title::before { content: '● '; color: #800000; }
    .title { font-weight: 600; font-size: 0.83rem; }
    .msg { font-size: 0.78rem; }
    .when { font-size: 0.7rem; color: var(--p-text-muted-color); }
    .empty { padding: 1.5rem 0; text-align: center; color: var(--p-text-muted-color); font-size: 0.85rem; }
  `]
})
export class NotificationBellComponent implements OnInit, OnDestroy {
  protected svc = inject(NotificationService);
  protected readonly utc = utc;
  private router = inject(Router);
  @ViewChild('pop') pop!: Popover;

  ngOnInit()    { this.svc.start(); }
  ngOnDestroy() { this.svc.stop(); }

  toggle(e: Event) {
    this.svc.refresh();
    this.pop.toggle(e);
  }

  open(n: AppNotification) {
    this.svc.markRead(n);
    this.pop.hide();
    if (!n.link) return;
    // The target page reads { <entityType>Id } from history.state, e.g. { tasafId: 12 }.
    const state = n.entityId ? { [`${n.entityType}Id`]: n.entityId } : undefined;
    this.router.navigate([n.link], state ? { state } : {});
  }
}
