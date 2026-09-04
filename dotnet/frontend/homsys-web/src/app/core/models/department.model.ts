export interface DepartmentDto {
  id: number;
  name: string;
  code: string;
  description: string;
  isActive: boolean;
  companyId: number;
  companyName: string;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface CreateDepartmentDto {
  name: string;
  code: string;
  description: string;
  companyId: number;
}

export interface UpdateDepartmentDto {
  name: string;
  code: string;
  description: string;
  companyId: number;
  isActive: boolean;
}
