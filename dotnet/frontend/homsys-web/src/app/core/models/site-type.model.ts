export interface SiteTypeDto {
  id: number;
  name: string;
  code: string;
  description: string;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface CreateSiteTypeDto {
  name: string;
  code: string;
  description: string;
}

export interface UpdateSiteTypeDto {
  name: string;
  code: string;
  description: string;
  isActive: boolean;
}
