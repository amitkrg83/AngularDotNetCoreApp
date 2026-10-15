import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, firstValueFrom, throwError } from 'rxjs';
import { CartItem, Checkout, Order, Product, ProductInput, User } from './models';

export const errorInterceptor: HttpInterceptorFn = (request,next) => {
  const router=inject(Router);
  return next(request).pipe(catchError((error:HttpErrorResponse)=>{
    if(error.status===401 && !request.url.startsWith('/api/auth/')) void router.navigateByUrl('/login');
    return throwError(()=>error);
  }));
};
export function message(error:unknown):string {
  if(error instanceof HttpErrorResponse) {
    if(error.status===429) return 'Too many attempts. Please wait a minute.';
    if(error.status===0) return 'Cannot reach the API. Check that the backend is running.';
    return error.error?.title ?? 'Request failed. Please check the input and try again.';
  }
  return 'Something went wrong. Please try again.';
}
@Injectable({providedIn:'root'})
export class Api {
  private readonly http=inject(HttpClient);
  get<T>(path:string){return firstValueFrom(this.http.get<T>('/api/'+path));}
  post<T>(path:string,body:unknown,headers:Record<string,string>={}){return firstValueFrom(this.http.post<T>('/api/'+path,body,{headers}));}
  put<T>(path:string,body:unknown){return firstValueFrom(this.http.put<T>('/api/'+path,body));}
  delete(path:string){return firstValueFrom(this.http.delete('/api/'+path));}
  csrf(){return this.get<void>('auth/csrf');}
  products(){return this.get<Product[]>('products');}
  product(id:string){return this.get<Product>('products/'+id);}
  orders(admin=false){return this.get<Order[]>(admin?'admin/orders':'orders');}
  checkout(input:Checkout,key:string){return this.post<{orderId:string;url:string;mode:string}>('orders/checkout',input,{'Idempotency-Key':key});}
  saveProduct(id:string|null,input:ProductInput){return id?this.put<Product>('admin/products/'+id,input):this.post<Product>('admin/products',input);}
}
@Injectable({providedIn:'root'})
export class Cart {
  readonly items=signal<CartItem[]>([]);
  readonly count=computed(()=>this.items().reduce((n,l)=>n+l.quantity,0));
  readonly total=computed(()=>this.items().reduce((n,l)=>n+l.quantity*l.product.price,0));
  add(product:Product,quantity=1){
    if(product.stockQuantity<1) return;
    this.items.update(items=>{
      const line=items.find(l=>l.product.id===product.id);
      return line?items.map(l=>l===line?{product,quantity:Math.min(l.quantity+quantity,product.stockQuantity,99)}:l):[...items,{product,quantity:Math.min(quantity,product.stockQuantity,99)}];
    });
  }
  quantity(id:string,value:number){
    if(!Number.isFinite(value))return;
    if(value<=0){this.remove(id);return;}
    this.items.update(items=>items.map(l=>l.product.id===id?{...l,quantity:Math.max(1,Math.min(Math.floor(value),l.product.stockQuantity,99))}:l));
  }
  remove(id:string){this.items.update(items=>items.filter(l=>l.product.id!==id));}
  clear(){this.items.set([]);}
}
@Injectable({providedIn:'root'})
export class Auth {
  private readonly api=inject(Api);
  private readonly cart=inject(Cart);
  readonly user=signal<User|null>(null);
  private restored:Promise<void>|null=null;
  restore():Promise<void>{return this.restored??=this.load();}
  private async load(){
    await this.api.csrf();
    try{this.user.set(await this.api.get<User>('auth/me'));}
    catch(error){if(!(error instanceof HttpErrorResponse) || error.status!==401)throw error;}
    finally{await this.api.csrf();}
  }
  async login(email:string,password:string){await this.api.csrf();this.user.set(await this.api.post<User>('auth/login',{email,password}));this.cart.clear();await this.api.csrf();}
  async register(name:string,email:string,password:string){await this.api.csrf();this.user.set(await this.api.post<User>('auth/register',{name,email,password}));this.cart.clear();await this.api.csrf();}
  async logout(){await this.api.csrf();await this.api.post<void>('auth/logout',{});this.user.set(null);this.cart.clear();await this.api.csrf();}
}
export const customerGuard:CanActivateFn=async()=>{
  const auth=inject(Auth),router=inject(Router);
  try{await auth.restore();return auth.user()?true:router.createUrlTree(['/login']);}
  catch{return router.createUrlTree(['/login']);}
};
export const adminGuard:CanActivateFn=async()=>{
  const auth=inject(Auth),router=inject(Router);
  try{await auth.restore();return auth.user()?.role==='Admin'?true:router.createUrlTree(['/products']);}
  catch{return router.createUrlTree(['/login']);}
};
