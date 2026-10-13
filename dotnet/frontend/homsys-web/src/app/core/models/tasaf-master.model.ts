/** One TASAF master list and which optional fields it uses (null = field not used). */
export interface TasafMasterMeta {
  key: string;
  label: string;
  parentKey?: string | null;
  parentLabel?: string | null;
  userLabel?: string | null;
  amountLabel?: string | null;
}

/** Approval rules: approval types × roles → not required / always (overAmount null) / over amount. */
export interface TasafApprovalRules {
  types: TasafLookupDto[];
  roles: TasafLookupDto[];
  rules: TasafApprovalRuleCell[];
}

export interface TasafApprovalRuleCell {
  approvalTypeId: number;
  roleId: number;
  required: boolean;
  overAmount: number | null;
}

/** Approval matrix: sales groups × approval roles → user (null = vacant). */
export interface TasafApprovalMatrix {
  salesGroups: TasafLookupDto[];
  roles: TasafLookupDto[];
  cells: TasafApproverCell[];
}

export interface TasafApproverCell {
  salesGroupId: number;
  roleId: number;
  userId: number | null;
  userName?: string | null;
}

export interface TasafLookupDto {
  id: number;
  code: string;
  name: string;
  description: string;
  sortOrder: number;
  isActive: boolean;
  parentId?: number | null;
  parentName?: string | null;
  userId?: number | null;
  userName?: string | null;
  amount?: number | null;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface SaveTasafLookupDto {
  code: string;
  name: string;
  description: string;
  sortOrder: number;
  isActive: boolean;
  parentId?: number | null;
  userId?: number | null;
  amount?: number | null;
}

export interface TasafUserOption {
  id: number;
  username: string;
  fullName: string;
}
