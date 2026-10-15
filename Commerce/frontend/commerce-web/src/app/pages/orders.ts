import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, message } from '../core';
import { Order } from '../models';
@Component({standalone:true,imports:[CurrencyPipe,DatePipe,RouterLink],template:`
<div class="section-heading"><div><span class="eyebrow">YOUR COLLECTION, ON ITS WAY</span><h1>Your orders</h1></div><button class="secondary" (click)="load()">Refresh status</button></div>
@if(canceled){<p class="notice">You returned from checkout. The order stays pending until payment succeeds or the Stripe session expires. You can resume checkout below.</p>}
@if(error()){<p class="error" role="alert">{{error()}}</p>}
@if(loading()){<p role="status">Loading orders…</p>}
<p class="muted">Pending payment status refreshes automatically. Only a verified payment event confirms a Stripe order.</p>
@for(order of orders();track order.id){<article class="panel order"><div class="between"><div><small>{{order.createdAt|date:'medium'}}</small><h2>Order {{order.id.slice(0,8)}}</h2></div><strong>{{order.total|currency:'USD'}}</strong></div><div class="actions"><span class="badge" [class.success]="order.paymentStatus==='Paid'">{{order.paymentStatus}}</span><span class="badge">{{order.fulfillmentStatus}}</span></div>@for(line of order.lines;track line.productId){<div class="between"><span>{{line.name}} × {{line.quantity}}</span><span>{{line.unitPrice*line.quantity|currency:'USD'}}</span></div>}<p class="muted">Ship to {{order.shipping.name}}, {{order.shipping.line1}}, {{order.shipping.city}}, {{order.shipping.postalCode}}, {{order.shipping.country}}</p>@if(order.paymentStatus==='Pending'&&order.checkoutUrl){<a class="secondary button" [href]="order.checkoutUrl">Resume checkout →</a>}</article>}@empty{ @if(!loading()&&!error()){<section class="panel empty"><h2>No orders yet.</h2><a routerLink="/products">Explore the collection →</a></section>} }
`})
export class OrdersPage implements OnInit,OnDestroy {
  private readonly api=inject(Api);private readonly route=inject(ActivatedRoute);readonly orders=signal<Order[]>([]);readonly error=signal('');readonly loading=signal(true);readonly canceled=this.route.snapshot.queryParamMap.has('canceled');private timer:ReturnType<typeof setInterval>|undefined;private fetching=false;
  ngOnInit(){void this.load();this.timer=setInterval(()=>{if(this.orders().some(o=>o.paymentStatus==='Pending'))void this.load(false);},5000);}
  async load(show=true){if(this.fetching)return;this.fetching=true;if(show)this.loading.set(true);this.error.set('');try{this.orders.set(await this.api.orders());}catch(e){this.error.set(message(e));}finally{this.fetching=false;this.loading.set(false);}}
  ngOnDestroy(){if(this.timer)clearInterval(this.timer);}
}
@Component({standalone:true,imports:[CurrencyPipe,RouterLink],template:`
<section class="panel payment"><span class="eyebrow">SIMULATED PAYMENT · NO REAL CHARGES</span><h1>Try the checkout flow.</h1><p>This local simulator does not contact a payment provider or collect card details. Switch the backend to Stripe test mode to try a real sandbox integration.</p>
@if(error()){<p class="error" role="alert">{{error()}}</p>}
@if(order();as o){<h2>{{o.total|currency:'USD'}}</h2><p>Order {{o.id.slice(0,8)}} · {{o.paymentStatus}}</p><div class="actions"><button class="primary" [disabled]="busy()||o.paymentStatus!=='Pending'" (click)="finish('Paid')">Simulate successful payment</button><button class="secondary" [disabled]="busy()||o.paymentStatus!=='Pending'" (click)="finish('Failed')">Simulate decline</button><button class="link" [disabled]="busy()||o.paymentStatus!=='Pending'" (click)="finish('Canceled')">Cancel order</button></div>}
<a routerLink="/orders">View orders →</a></section>
`})
export class PaymentPage implements OnInit {
  private readonly api=inject(Api);private readonly route=inject(ActivatedRoute);private readonly router=inject(Router);readonly order=signal<Order|null>(null);readonly error=signal('');readonly busy=signal(false);
  async ngOnInit(){try{const mode=await this.api.get<{mode:string}>('orders/payment-mode');if(mode.mode!=='Simulated'){this.error.set('Simulator is disabled while Stripe mode is active.');return;}const order=(await this.api.orders()).find(o=>o.id===this.route.snapshot.paramMap.get('id'));if(!order){this.error.set('Order not found.');return;}this.order.set(order);}catch(e){this.error.set(message(e));}}
  async finish(outcome:string){if(this.busy()||!this.order())return;this.busy.set(true);try{await this.api.post('orders/'+this.order()!.id+'/simulate',{outcome});await this.router.navigateByUrl('/orders');}catch(e){this.error.set(message(e));}finally{this.busy.set(false);}}
}
