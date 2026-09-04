import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface PermissionDto {
  id: number;
  key: string;
  name: string;
  description?: string;
}

@Injectable({ providedIn: 'root' })
export class PermissionService {
  private base = `${environment.apiUrl}/permissions`;

  constructor(private http: HttpClient) {}

  getAll() {
    return this.http.get<{ success: boolean; data: PermissionDto[] }>(this.base);
  }

  getRolePermissions(roleId: number) {
    return this.http.get<{ success: boolean; data: number[] }>(`${this.base}/role/${roleId}`);
  }

  setRolePermissions(roleId: number, permissionIds: number[]) {
    return this.http.put<{ success: boolean; message: string }>(
      `${this.base}/role/${roleId}`,
      { permissionIds }
    );
  }
}
