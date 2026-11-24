import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { UserDto, CreateUserDto, UpdateUserDto, RoleDto } from '../models/user.model';
import { ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly base = `${environment.apiUrl}/users`;
  private readonly rolesBase = `${environment.apiUrl}/roles`;

  constructor(private http: HttpClient) {}

  getAll() { return this.http.get<ApiResponse<UserDto[]>>(this.base); }
  getById(id: number) { return this.http.get<ApiResponse<UserDto>>(`${this.base}/${id}`); }
  create(dto: CreateUserDto) {
    return this.http.post<ApiResponse<UserDto> & { generatedPassword: string }>(this.base, dto);
  }
  update(id: number, dto: UpdateUserDto) { return this.http.put<ApiResponse<UserDto>>(`${this.base}/${id}`, dto); }
  /** Servicing branches (Site ids): the branches a BA covers — limits the TPAR branch. */
  servicingBranches(id: number) { return this.http.get<ApiResponse<number[]>>(`${this.base}/${id}/servicing-branches`); }
  setServicingBranches(id: number, siteIds: number[]) {
    return this.http.put<ApiResponse<unknown>>(`${this.base}/${id}/servicing-branches`, siteIds);
  }
  delete(id: number) { return this.http.delete<ApiResponse<null>>(`${this.base}/${id}`); }
  resetPassword(id: number) {
    return this.http.post<{ success: boolean; generatedPassword: string }>(`${this.base}/${id}/reset-password`, {});
  }
  getRoles() { return this.http.get<ApiResponse<RoleDto[]>>(this.rolesBase); }
}
