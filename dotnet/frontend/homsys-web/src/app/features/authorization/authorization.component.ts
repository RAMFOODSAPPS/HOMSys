import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { MessageService, ConfirmationService } from 'primeng/api';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { PermissionService, PermissionDto } from '../../core/services/permission.service';
import { RoleService } from '../../core/services/role.service';
import { UserService } from '../../core/services/user.service';
import { UserDto } from '../../core/models/user.model';
import { AUTH_MODULES, AuthModule, AuthPermDef } from './authorization-catalog';

interface RoleRow {
  id: number;
  name: string;
  description?: string;
  /** Saved permission ids. */
  saved: Set<number>;
  /** Edited permission ids (= saved until changed). */
  ids: Set<number>;
  saving: boolean;
}

/** A permission as shown: its catalog entry (page chips / icon) + the DB row. */
interface PermView extends AuthPermDef { perm: PermissionDto; }
interface ModuleView { def: AuthModule; pages: PermView[]; actions: PermView[]; }

@Component({
  selector: 'app-authorization',
  standalone: true,
  imports: [FormsModule, CheckboxModule, ButtonModule, InputTextModule],
  // Styles: the .az-* classes in styles.scss (kept out of the component for the 8 kB style budget).
  template: `
    <div class="az">
      <div class="az-tabs">
        <button type="button" [class.on]="tab() === 'role'" (click)="tab.set('role')"><i class="pi pi-shield"></i> By Role</button>
        <button type="button" [class.on]="tab() === 'user'" (click)="openUserTab()"><i class="pi pi-user"></i> By User</button>
      </div>

      @if (loading()) {
        <div class="az-empty">Loading…</div>
      } @else {
        <div class="az-grid">
          <!-- Left: roles or users -->
          <aside class="az-col az-list">
            @if (tab() === 'role') {
              <div class="az-col-title"><i class="pi pi-shield"></i> Roles</div>
              @for (r of roles(); track r.id) {
                <button type="button" class="az-item" [class.on]="r.id === roleId()" (click)="roleId.set(r.id)">
                  <i class="pi" [class]="roleIcon(r.name)"></i>
                  <span class="az-item-text">
                    <strong>{{ r.name }}</strong>
                    @if (isDirty(r)) { <span class="az-badge">Unsaved</span> }
                  </span>
                </button>
              }
            } @else {
              <div class="az-col-title"><i class="pi pi-users"></i> Users</div>
              <input pInputText class="az-search" placeholder="Search name or username" [ngModel]="userTerm()" (ngModelChange)="userTerm.set($event)" />
              @for (u of filteredUsers(); track u.id) {
                <button type="button" class="az-item" [class.on]="u.id === userId()" (click)="userId.set(u.id)">
                  <i class="pi pi-user"></i>
                  <span class="az-item-text">
                    <strong>{{ u.firstName }} {{ u.lastName }}</strong>
                    <small>{{ u.username }}@if (u.roles.length) { · {{ u.roles.join(', ') }} }</small>
                  </span>
                </button>
              } @empty {
                <div class="az-empty sm">{{ usersError() || 'No users found.' }}</div>
              }
            }
          </aside>

          <!-- Middle: modules -->
          <nav class="az-col az-pages">
            <div class="az-col-title"><i class="pi pi-file"></i> Pages</div>
            <button type="button" class="az-page" [class.on]="moduleKey() === null" (click)="moduleKey.set(null)">
              <i class="pi pi-th-large"></i> All Permissions
            </button>
            @for (mv of modules(); track mv.def.key) {
              <button type="button" class="az-page" [class.on]="moduleKey() === mv.def.key" (click)="moduleKey.set(mv.def.key)">
                <i class="pi pi-angle-right"></i><i class="pi" [class]="mv.def.icon"></i> {{ mv.def.label }}
              </button>
            }
          </nav>

          <!-- Main: header + permissions -->
          <section class="az-main">
            @if (tab() === 'role') {
              @if (role(); as r) {
                <div class="az-head">
                  <div>
                    <h2><i class="pi" [class]="roleIcon(r.name)"></i> {{ r.name }}</h2>
                    <p>{{ r.description || 'No description.' }} · {{ r.ids.size }} of {{ permissions().length }} permissions</p>
                  </div>
                  <div class="az-head-actions">
                    <p-button label="Discard" severity="secondary" [text]="true" [disabled]="!isDirty(r)" (onClick)="discard(r)" />
                    <p-button label="Save" icon="pi pi-check" [loading]="r.saving" [disabled]="!isDirty(r)" (onClick)="save(r)" />
                  </div>
                </div>
              }
            } @else {
              @if (user(); as u) {
                <div class="az-head">
                  <div>
                    <h2><i class="pi pi-user"></i> {{ u.firstName }} {{ u.lastName }}</h2>
                    <p>{{ u.username }} · {{ u.isActive ? 'Active' : 'Inactive' }} · {{ userPermIds().size }} permissions from their roles</p>
                    <div class="az-chips">
                      @for (rn of u.roles; track rn) { <span class="az-chip role"><i class="pi pi-shield"></i>{{ rn }}</span> }
                      @empty { <span class="az-muted">No roles — this user can't open any page.</span> }
                    </div>
                  </div>
                  <span class="az-muted">Read-only — change access per role on the By Role tab.</span>
                </div>
              } @else {
                <div class="az-head"><p class="az-muted">Pick a user to see what they can access.</p></div>
              }
            }

            <div class="az-table">
              <div class="az-row az-th">
                <span>Permission</span>
                <span class="c">{{ tab() === 'role' ? 'Access' : 'Has access' }}</span>
                @if (tab() === 'user') { <span>Given by</span> }
              </div>
              @for (mv of shownModules(); track mv.def.key) {
                @if (mv.pages.length) {
                  <div class="az-group">
                    <span><i class="pi" [class]="mv.def.icon"></i> {{ mv.def.label }}</span>
                    @if (tab() === 'role' && role(); as r) {
                      <button type="button" class="az-link" (click)="setAll(r, mv.pages, !allOn(r, mv.pages))">
                        {{ allOn(r, mv.pages) ? 'Clear all' : 'Select all' }}
                      </button>
                    }
                  </div>
                  @for (p of mv.pages; track p.perm.id) {
                    <div class="az-row" [class.changed]="tab() === 'role' && changed(p.perm.id)">
                      <div class="az-perm">
                        <i class="pi" [class]="p.icon"></i>
                        <div>
                          <div class="az-perm-name">{{ p.perm.name }}</div>
                          @if (p.perm.description) { <div class="az-perm-desc">{{ p.perm.description }}</div> }
                          @if (p.pages?.length) {
                            <div class="az-chips">
                              @for (pg of p.pages; track pg) { <span class="az-chip"><i class="pi pi-file"></i>{{ pg }}</span> }
                            </div>
                          }
                        </div>
                      </div>
                      <span class="c">
                        @if (tab() === 'role' && role(); as r) {
                          <p-checkbox [binary]="true" [ngModel]="r.ids.has(p.perm.id)" (ngModelChange)="toggle(r, p.perm.id, $event)" />
                        } @else {
                          <i class="pi" [class]="userPermIds().has(p.perm.id) ? 'pi-check-circle az-yes' : 'pi-minus az-no'"></i>
                        }
                      </span>
                      @if (tab() === 'user') { <span class="az-muted">{{ grantedBy(p.perm.id) }}</span> }
                    </div>
                  }
                }
              } @empty {
                <div class="az-empty sm">No permissions.</div>
              }
            </div>
          </section>

          <!-- Right: feature switches inside pages -->
          <aside class="az-col az-actions">
            <div class="az-col-title"><i class="pi pi-bolt"></i> Actions</div>
            @for (mv of shownModules(); track mv.def.key) {
              @for (a of mv.actions; track a.perm.id) {
                <div class="az-action" [class.changed]="tab() === 'role' && changed(a.perm.id)">
                  <div class="az-action-top">
                    <i class="pi" [class]="a.icon"></i>
                    <div>
                      <div class="az-perm-name">{{ a.perm.name }}</div>
                      @if (a.perm.description) { <div class="az-perm-desc">{{ a.perm.description }}</div> }
                    </div>
                  </div>
                  <div class="az-action-bottom">
                    <span class="az-tag">{{ mv.def.label }}</span>
                    @if (tab() === 'role' && role(); as r) {
                      <p-checkbox [binary]="true" [ngModel]="r.ids.has(a.perm.id)" (ngModelChange)="toggle(r, a.perm.id, $event)" />
                    } @else {
                      <span class="az-muted">
                        @if (userPermIds().has(a.perm.id)) { <i class="pi pi-check-circle az-yes"></i> {{ grantedBy(a.perm.id) }} }
                        @else { <i class="pi pi-minus az-no"></i> }
                      </span>
                    }
                  </div>
                </div>
              }
            }
            @if (!shownActionCount()) { <div class="az-empty sm">No actions in this module.</div> }
          </aside>
        </div>
      }
    </div>
  `
})
export class AuthorizationComponent implements OnInit, OnDestroy {
  private permSvc = inject(PermissionService);
  private roleSvc = inject(RoleService);
  private userSvc = inject(UserService);
  private toolbar = inject(GlobalToolbarService);
  private msgSvc  = inject(MessageService);
  private confirm = inject(ConfirmationService);

  protected loading     = signal(true);
  protected tab         = signal<'role' | 'user'>('role');
  protected permissions = signal<PermissionDto[]>([]);
  protected roles       = signal<RoleRow[]>([]);
  protected roleId      = signal<number | null>(null);
  /** Module filter (middle column); null = all. */
  protected moduleKey   = signal<string | null>(null);

  protected users       = signal<UserDto[] | null>(null);
  protected usersError  = signal<string | null>(null);
  protected userTerm    = signal('');
  protected userId      = signal<number | null>(null);

  protected role = computed(() => this.roles().find(r => r.id === this.roleId()) ?? null);
  protected user = computed(() => this.users()?.find(u => u.id === this.userId()) ?? null);

  /** Catalog modules filled with the DB permissions; permissions not in the catalog land in "Other". */
  protected modules = computed<ModuleView[]>(() => {
    const byKey = new Map(this.permissions().map(p => [p.key, p]));
    const used = new Set<string>();
    const views = AUTH_MODULES.map(def => {
      const pick = (defs: AuthPermDef[]) => defs
        .filter(d => byKey.has(d.key))
        .map(d => { used.add(d.key); return { ...d, perm: byKey.get(d.key)! }; });
      return { def, pages: pick(def.pages), actions: pick(def.actions ?? []) };
    });
    const other = this.permissions().filter(p => !used.has(p.key))
      .map(p => ({ key: p.key, icon: 'pi-key', perm: p }));
    if (other.length) views.push({ def: { key: 'other', label: 'Other', icon: 'pi-ellipsis-h', pages: [] }, pages: other, actions: [] });
    return views.filter(v => v.pages.length || v.actions.length);
  });

  protected shownModules = computed(() => {
    const key = this.moduleKey();
    return key ? this.modules().filter(m => m.def.key === key) : this.modules();
  });
  protected shownActionCount = computed(() => this.shownModules().reduce((n, m) => n + m.actions.length, 0));

  protected filteredUsers = computed(() => {
    const term = this.userTerm().trim().toLowerCase();
    const list = this.users() ?? [];
    return term
      ? list.filter(u => `${u.firstName} ${u.lastName} ${u.username}`.toLowerCase().includes(term))
      : list;
  });

  /** Permission ids the picked user gets from their roles (saved state). */
  protected userPermIds = computed(() => {
    const ids = new Set<number>();
    const u = this.user();
    if (!u) return ids;
    for (const r of this.roles()) if (u.roleIds.includes(r.id)) r.saved.forEach(id => ids.add(id));
    return ids;
  });

  ngOnInit() {
    this.toolbar.set({ title: 'Authorization Management' });
    this.load();
  }

  ngOnDestroy() { this.toolbar.clear(); }

  private load() {
    this.loading.set(true);
    forkJoin({ roles: this.roleSvc.getAll(), perms: this.permSvc.getAll() }).subscribe({
      next: ({ roles, perms }) => {
        const roleList = roles.data ?? [];
        this.permissions.set(perms.data ?? []);
        forkJoin(roleList.map(r => this.permSvc.getRolePermissions(r.id))).subscribe({
          next: results => {
            this.roles.set(roleList.map((r, i) => {
              const ids = results[i]?.data ?? [];
              return { id: r.id, name: r.name, description: r.description,
                       saved: new Set(ids), ids: new Set(ids), saving: false };
            }));
            this.roleId.set(roleList[0]?.id ?? null);
            this.loading.set(false);
          },
          error: () => this.failLoad()
        });
      },
      error: () => this.failLoad()
    });
  }

  private failLoad() {
    this.loading.set(false);
    this.msgSvc.add({ severity: 'error', summary: 'Error', detail: 'Failed to load roles and permissions.' });
  }

  /** Users load on first open of the By User tab (needs the Users permission's API). */
  protected openUserTab() {
    this.tab.set('user');
    if (this.users() !== null) return;
    this.userSvc.getAll().subscribe({
      next: res => {
        const list = (res.data ?? []).slice().sort((a, b) =>
          `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`));
        this.users.set(list);
        if (list.length && this.userId() === null) this.userId.set(list[0].id);
      },
      error: () => { this.users.set([]); this.usersError.set('Could not load users (needs the User Management permission).'); }
    });
  }

  protected roleIcon(name: string) {
    const n = name.toLowerCase();
    if (n === 'admin') return 'pi-star';
    if (n.includes('encoder') || n.includes('custodian')) return 'pi-pencil';
    if (n.includes('verifier')) return 'pi-check-square';
    if (n.includes('manager') || ['coo', 'nsd', 'nsm', 'gkam', 'tmm'].includes(n)) return 'pi-briefcase';
    return 'pi-users';
  }

  protected isDirty(r: RoleRow) {
    return r.ids.size !== r.saved.size || [...r.ids].some(id => !r.saved.has(id));
  }

  protected changed(permId: number) {
    const r = this.role();
    return !!r && r.ids.has(permId) !== r.saved.has(permId);
  }

  protected allOn(r: RoleRow, perms: PermView[]) { return perms.every(p => r.ids.has(p.perm.id)); }

  protected setAll(r: RoleRow, perms: PermView[], on: boolean) {
    for (const p of perms) on ? r.ids.add(p.perm.id) : r.ids.delete(p.perm.id);
    this.touch();
  }

  protected toggle(r: RoleRow, permId: number, checked: boolean) {
    checked ? r.ids.add(permId) : r.ids.delete(permId);
    this.touch();
  }

  protected discard(r: RoleRow) {
    r.ids = new Set(r.saved);
    this.touch();
  }

  protected save(r: RoleRow) {
    const removingOwnAccess = r.saved.has(this.permId('authorization')) && !r.ids.has(this.permId('authorization'));
    const go = () => {
      r.saving = true;
      this.touch();
      this.permSvc.setRolePermissions(r.id, [...r.ids]).subscribe({
        next: () => {
          r.saving = false;
          r.saved = new Set(r.ids);
          this.touch();
          this.msgSvc.add({ severity: 'success', summary: 'Saved',
            detail: `Permissions updated for ${r.name}. Users get them at their next login.` });
        },
        error: () => {
          r.saving = false;
          this.touch();
          this.msgSvc.add({ severity: 'error', summary: 'Error', detail: 'Failed to save permissions.' });
        }
      });
    };
    if (!removingOwnAccess) { go(); return; }
    this.confirm.confirm({
      header: 'Remove Authorization access?',
      message: `Users with only the ${r.name} role will no longer be able to open this page.`,
      acceptLabel: 'Save anyway', rejectLabel: 'Cancel',
      accept: go
    });
  }

  /** "Admin, TMM" — the user's roles that give a permission. */
  protected grantedBy(permId: number) {
    const u = this.user();
    if (!u) return '';
    return this.roles().filter(r => u.roleIds.includes(r.id) && r.saved.has(permId)).map(r => r.name).join(', ');
  }

  private permId(key: string) { return this.permissions().find(p => p.key === key)?.id ?? -1; }

  /** Role rows are mutated in place; re-emit so the computed views refresh. */
  private touch() { this.roles.update(rows => [...rows]); }
}
