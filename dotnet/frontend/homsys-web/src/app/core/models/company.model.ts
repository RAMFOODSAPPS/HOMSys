export interface CompanyDto {
  id: number;
  name: string;
  code: string;
  address: string;
  phone: string;
  email: string;
  contactPerson: string;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface CreateCompanyDto {
  name: string;
  code: string;
  address: string;
  phone: string;
  email: string;
  contactPerson: string;
}

export interface UpdateCompanyDto {
  name: string;
  code: string;
  address: string;
  phone: string;
  email: string;
  contactPerson: string;
  isActive: boolean;
}
