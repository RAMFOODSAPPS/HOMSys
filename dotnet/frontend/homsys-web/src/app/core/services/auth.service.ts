import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { LoginRequest, AuthResponse, UserInfo, ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _user = signal<UserInfo | null>(null);
  private accessToken = '';

  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._user() !== null);
  readonly isAdmin = computed(() => this._user()?.roles.includes('Admin') ?? false);

  hasRole(...roles: string[]): boolean {
    const userRoles = this._user()?.roles ?? [];
    return roles.some(r => userRoles.includes(r));
  }

  hasPermission(key: string): boolean {
    return this._user()?.permissions.includes(key) ?? false;
  }

  constructor(private http: HttpClient, private router: Router) {}

  // Called once from an app initializer, before routes activate. The access
  // token only ever lives in memory, so a hard refresh always starts with
  // none — restoring _user from localStorage alone (the old behavior) let
  // authGuard pass immediately with no token, guaranteeing a 401 on the
  // page's first API call. Awaiting a real refresh() here means the guard
  // only sees isAuthenticated() once a token is actually in hand again.
  initSession(): Promise<void> {
    if (!localStorage.getItem('user')) return Promise.resolve();
    return firstValueFrom(this.refresh()).then(
      () => {},
      () => { this.clearSession(); }
    );
  }

  login(request: LoginRequest) {
    return this.http
      .post<ApiResponse<AuthResponse>>(`${environment.apiUrl}/auth/login`, request, { withCredentials: true })
      .pipe(tap(res => { if (res.success && res.data) this.setSession(res.data); }));
  }

  refresh() {
    return this.http
      .post<ApiResponse<AuthResponse>>(`${environment.apiUrl}/auth/refresh`, {}, { withCredentials: true })
      .pipe(tap(res => { if (res.success && res.data) this.setSession(res.data); }));
  }

  logout() {
    this.http.post(`${environment.apiUrl}/auth/logout`, {}, { withCredentials: true })
      .subscribe({ error: () => {} });
    this.clearSession();
    this.router.navigate(['/login']);
  }

  getAccessToken(): string { return this.accessToken; }

  private setSession(auth: AuthResponse) {
    this.accessToken = auth.accessToken;
    localStorage.setItem('user', JSON.stringify(auth.user));
    this._user.set(auth.user);
  }

  private clearSession() {
    this.accessToken = '';
    localStorage.removeItem('user');
    this._user.set(null);
  }
}
