import { CurrencyPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith } from 'rxjs';
import { Product } from '../../core/models/product';
import { ProductService } from '../../core/services/product.service';

interface ProductListState {
  products: readonly Product[];
  loading: boolean;
  error: string;
}

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [CurrencyPipe],
  template: `
    <section aria-labelledby="products-heading">
      <h1 id="products-heading">Find your next favorite</h1>
      <p>Workspace essentials, thoughtfully selected.</p>

      @if (state().loading) {
        <p role="status">Loading products…</p>
      } @else if (state().error) {
        <p role="alert">{{ state().error }}</p>
      } @else {
        <div class="products">
          @for (product of state().products; track product.id) {
            <article>
              <span class="category">{{ product.category }}</span>
              <h2>{{ product.name }}</h2>
              <p>{{ product.description }}</p>
              <strong>{{ product.price | currency:'USD' }}</strong>
              <p>{{ product.stockQuantity }} in stock</p>
            </article>
          } @empty {
            <p>No products available.</p>
          }
        </div>
      }
    </section>
  `,
  styles: [`
    h1 { font-size: clamp(2rem, 5vw, 3rem); margin-bottom: .5rem; }
    .products {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 260px), 1fr));
      gap: 1.5rem;
      margin-top: 2rem;
    }
    article {
      padding: 1.5rem;
      background: white;
      border: 1px solid #dce3ed;
      border-radius: 16px;
    }
    .category { color: #3458a6; font-size: .85rem; font-weight: 600; }
    h2 { font-size: 1.25rem; }
    p { color: #536176; line-height: 1.6; }
    strong { font-size: 1.5rem; }
  `]
})
export class ProductList {
  private readonly productService = inject(ProductService);

  readonly state = toSignal(
    this.productService.getProducts().pipe(
      map((products): ProductListState => ({
        products,
        loading: false,
        error: ''
      })),
      startWith({
        products: [],
        loading: true,
        error: ''
      } as ProductListState),
      catchError(() => of<ProductListState>({
        products: [],
        loading: false,
        error: 'Unable to load products. Check that the API is running.'
      }))
    ),
    { requireSync: true }
  );
}
