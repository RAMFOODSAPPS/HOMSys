export interface UserDto {
  id: number;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  companyId?: number;
  companyName?: string;
  departmentId?: number;
  departmentName?: string;
  siteId?: number;
  siteName?: string;
  branchCode?: string;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
  roles: string[];
  roleIds: number[];
}

export interface CreateUserDto {
  username: string;
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  companyId?: number;
  departmentId?: number;
  siteId?: number;
  branchCode?: string;
  roleIds: number[];
}

export interface UpdateUserDto {
  email: string;
  firstName: string;
  lastName: string;
  companyId?: number;
  departmentId?: number;
  siteId?: number;
  branchCode?: string;
  isActive: boolean;
  roleIds: number[];
  newPassword?: string;
}

export interface RoleDto {
  id: number;
  name: string;
  description?: string;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface CreateRoleDto {
  name: string;
  description?: string;
}

export interface UpdateRoleDto {
  name: string;
  description?: string;
}
