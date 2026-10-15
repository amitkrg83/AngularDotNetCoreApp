import { Routes } from '@angular/router';
import { adminGuard, customerGuard } from './core';
export const routes:Routes=[
  {path:'login',loadComponent:()=>import('./pages/login').then(m=>m.LoginPage)},
  {path:'products',canActivate:[customerGuard],loadComponent:()=>import('./pages/catalog').then(m=>m.CatalogPage)},
  {path:'products/:id',canActivate:[customerGuard],loadComponent:()=>import('./pages/catalog').then(m=>m.DetailPage)},
  {path:'cart',canActivate:[customerGuard],loadComponent:()=>import('./pages/checkout').then(m=>m.CartPage)},
  {path:'checkout',canActivate:[customerGuard],loadComponent:()=>import('./pages/checkout').then(m=>m.CheckoutPage)},
  {path:'payment/:id',canActivate:[customerGuard],loadComponent:()=>import('./pages/orders').then(m=>m.PaymentPage)},
  {path:'orders',canActivate:[customerGuard],loadComponent:()=>import('./pages/orders').then(m=>m.OrdersPage)},
  {path:'admin',canActivate:[adminGuard],loadComponent:()=>import('./pages/admin').then(m=>m.AdminPage)},
  {path:'',pathMatch:'full',redirectTo:'products'},
  {path:'**',redirectTo:'products'}
];
