import { configureStore, combineReducers } from '@reduxjs/toolkit';
import { persistStore, persistReducer } from 'redux-persist';
import storage from 'redux-persist/lib/storage';
import accountReducer from './account/account.slice';
import systemReducer from './system/system.slice';

const rootReducer = combineReducers({
  account: accountReducer,
  system: systemReducer,
});

const persistConfig = {
  key: 'washgo_root',
  storage,
  whitelist: ['account', 'system'],
};

const persistedReducer = persistReducer(persistConfig, rootReducer);

export const store = configureStore({
  reducer: persistedReducer,
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware({
      serializableCheck: false,
    }),
});

export const persistor = persistStore(store);

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
