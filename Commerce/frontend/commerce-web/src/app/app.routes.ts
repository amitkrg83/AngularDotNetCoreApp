import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'products'
  },
  {
    path: 'products',
    loadComponent: () =>
      import('./features/products/product-list')
        .then(module => module.ProductList)
  },
  {
    path: '**',
    redirectTo: 'products'
  }
];
