import { IdentityType } from './enums';

export interface IdentityResponse {
  clientId: string;
  type: IdentityType;
  displayName: string;
  grantedScopes: string[];
  enabled: boolean;
  createdAt: string;
}

export interface RegisterIdentityRequest {
  clientId: string;
  type: IdentityType;
  displayName: string;
  grantedScopes: string[];
}

export interface IssuedSecretResponse {
  identity: IdentityResponse;
  clientSecret: string;
}
