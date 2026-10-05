import React, { ReactNode } from 'react';
import { useSelector } from 'react-redux';
import { RootState } from '../../redux/store.redux';

interface PermissionGateProps {
  permission?: string | string[];
  role?: string | string[];
  children: ReactNode;
  fallback?: ReactNode;
}

export const PermissionGate: React.FC<PermissionGateProps> = ({
  permission,
  role,
  children,
  fallback = null,
}) => {
  const { permissions, user } = useSelector((state: RootState) => state.account);

  if (role) {
    const roles = Array.isArray(role) ? role : [role];
    const userRole = user?.role;
    if (!userRole || !roles.includes(userRole)) {
      return <>{fallback}</>;
    }
  }

  if (permission) {
    const perms = Array.isArray(permission) ? permission : [permission];
    const hasPerm = perms.some((p) => permissions.includes(p));
    if (!hasPerm) {
      return <>{fallback}</>;
    }
  }

  return <>{children}</>;
};
