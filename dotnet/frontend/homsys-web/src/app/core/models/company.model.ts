export interface CompanyDto {
  id: number;
  name: string;
  code: string;
  address: string;
  phone: string;
  email: string;
  contactPerson: string;
  isActive: boolean;
  /** Logo stored in Azure Blob Storage; shown via CompanyService.logoUrl() (cache-busted by logoVersion). */
  hasLogo?: boolean;
  logoSizeKb?: number | null;
  logoVersion?: number | null;
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
