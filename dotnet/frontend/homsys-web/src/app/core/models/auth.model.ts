export interface LoginRequest {
  username: string;
  password: string;
}

export interface UserInfo {
  id: number;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  mustChangePassword: boolean;
  roles: string[];
  permissions: string[];
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  mustChangePassword: boolean;
  user: UserInfo;
}

export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
}
