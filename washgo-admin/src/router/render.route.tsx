import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { useSelector } from 'react-redux';
import { RootState } from '../redux/store.redux';
import { AppLayout } from './AppLayout';
import { LoginScreen } from '../screens/login/login.screen';
import { DashboardScreen } from '../screens/dashboard/dashboard.screen';
import { WashOrderScreen } from '../screens/wash-order/wash-order.screen';
import { LockerScreen } from '../screens/locker/locker.screen';
import { WashServiceScreen } from '../screens/wash-service/wash-service.screen';
import { ServiceAreaScreen } from '../screens/service-area/service-area.screen';
import { MerchantScreen } from '../screens/merchant/merchant.screen';

const ProtectedRoute: React.FC<{ children: React.ReactElement }> = ({ children }) => {
  const { isAuthenticated } = useSelector((state: RootState) => state.account);
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  return children;
};

export const AppRouter: React.FC = () => {
  const { isAuthenticated } = useSelector((state: RootState) => state.account);

  return (
    <Routes>
      <Route
        path="/login"
        element={isAuthenticated ? <Navigate to="/" replace /> : <LoginScreen />}
      />

      <Route
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route path="/" element={<DashboardScreen />} />
        <Route path="/wash-order" element={<WashOrderScreen />} />
        <Route path="/locker" element={<LockerScreen />} />
        <Route path="/wash-service" element={<WashServiceScreen />} />
        <Route path="/service-area" element={<ServiceAreaScreen />} />
        <Route path="/merchant" element={<MerchantScreen />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};
