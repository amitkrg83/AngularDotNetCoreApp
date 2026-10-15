import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, Auth, Cart, message } from '../core';
import { Checkout } from '../models';
@Component({standalone:true,imports:[CurrencyPipe,FormsModule,RouterLink],template:`
<span class="eyebrow">ALMOST YOURS</span><h1>Your shopping bag</h1>
@if(cart.items().length){<div class="checkout-layout"><section class="panel">@for(line of cart.items();track line.product.id){<div class="cart-line"><img [src]="line.product.imageUrl" [alt]="line.product.name"><div><a [routerLink]="['/products',line.product.id]"><h3>{{line.product.name}}</h3></a><p>{{line.product.price|currency:'USD'}}</p><label>Quantity<input type="number" min="1" [max]="line.product.stockQuantity" [ngModel]="line.quantity" (ngModelChange)="cart.quantity(line.product.id,$event)"></label><button class="link" (click)="cart.remove(line.product.id)">Remove</button></div><strong>{{line.product.price*line.quantity|currency:'USD'}}</strong></div>}</section><aside class="panel summary"><h2>Order summary</h2><div class="between"><span>Subtotal</span><strong>{{cart.total()|currency:'USD'}}</strong></div><div class="between"><span>Shipping</span><span>Free</span></div><p class="muted">Demo pricing. No taxes calculated.</p><hr><div class="between"><strong>Total</strong><strong>{{cart.total()|currency:'USD'}}</strong></div><a routerLink="/checkout" class="primary button full">Continue to checkout →</a><a routerLink="/products">Keep exploring</a></aside></div>}@else{<section class="panel empty"><h2>Your bag is waiting.</h2><p>Find something you love in the collection.</p><a routerLink="/products" class="primary button">Browse products →</a></section>}
`})
export class CartPage {readonly cart=inject(Cart);}
@Component({standalone:true,imports:[ReactiveFormsModule,CurrencyPipe,RouterLink],template:`
<a routerLink="/cart">← Back to bag</a><h1>Checkout</h1>
@if(cart.items().length || attempt){<div class="checkout-layout"><section class="panel"><h2>Shipping details</h2><form [formGroup]="form" (ngSubmit)="submit()"><label>Full name<input formControlName="name" autocomplete="shipping name" maxlength="200"></label><label>Street address<input formControlName="line1" autocomplete="shipping address-line1" maxlength="200"></label><div class="form-row"><label>City<input formControlName="city" autocomplete="shipping address-level2" maxlength="200"></label><label>ZIP code<input formControlName="postalCode" autocomplete="shipping postal-code" maxlength="200"></label></div><label>Country<select formControlName="country"><option value="US">United States</option></select></label>
@if(error()){<p class="error" role="alert">{{error()}}</p>}
@if(attempt){<p class="notice">Checkout is reserved. Retry uses the same cart and checkout key. Visit Orders to see its status.</p>}
<button class="primary full" [disabled]="busy()">{{busy()?'Preparing checkout…':attempt?'Retry payment checkout →':'Continue to payment →'}}</button></form></section><aside class="panel summary"><h2>Review your order</h2>@for(line of cart.items();track line.product.id){<div class="between"><span>{{line.product.name}} × {{line.quantity}}</span><span>{{line.product.price*line.quantity|currency:'USD'}}</span></div>}<hr><div class="between"><strong>Total</strong><strong>{{cart.total()|currency:'USD'}}</strong></div><p>Prices and stock are checked again by the server.</p><p class="muted">USD · Free demo shipping · No taxes calculated.</p></aside></div>}@else{<p>Your bag is empty. <a routerLink="/products">Browse products</a>.</p>}
`})
export class CheckoutPage {
  readonly cart=inject(Cart);private readonly auth=inject(Auth);private readonly api=inject(Api);private readonly router=inject(Router);private readonly fb=inject(FormBuilder);
  readonly busy=signal(false);readonly error=signal('');attempt:{key:string;input:Checkout}|null=null;
  readonly form=this.fb.nonNullable.group({name:[this.auth.user()?.name??'',Validators.required],line1:['',Validators.required],city:['',Validators.required],postalCode:['',Validators.required],country:['US',Validators.required]});
  async submit(){
    if(this.busy())return;this.error.set('');
    if(!this.attempt && this.form.invalid){this.form.markAllAsTouched();this.error.set('Complete every shipping field.');return;}
    this.attempt??={key:crypto.randomUUID(),input:{items:this.cart.items().map(l=>({productId:l.product.id,quantity:l.quantity})),shipping:this.form.getRawValue()}};
    this.form.disable();this.busy.set(true);
    try{const result=await this.api.checkout(this.attempt.input,this.attempt.key);this.cart.clear();const url=new URL(result.url,location.origin);if(url.origin===location.origin){await this.router.navigateByUrl(url.pathname+url.search);}else if(url.protocol==='https:'&&url.hostname==='checkout.stripe.com'){location.assign(url.href);}else{throw new Error('Unexpected checkout URL');}}
    catch(e){this.error.set(message(e));if(e instanceof HttpErrorResponse && (e.status===400 || e.status===409)){this.attempt=null;this.form.enable();}}finally{this.busy.set(false);}
  }
}
