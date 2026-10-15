import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Api, Cart, message } from '../core';
import { Product } from '../models';
@Component({standalone:true,imports:[CurrencyPipe,FormsModule,RouterLink],template:`
<section class="hero"><div><span class="eyebrow">THE EVERYDAY COLLECTION</span><h1>Small upgrades.<br>Better everyday.</h1><p>Discover well-made essentials for work, play, and everything in between.</p><a class="primary button" href="#collection">Explore the collection ↓</a></div><div class="hero-art" aria-hidden="true"><div class="orb"></div><span>GOOD DESIGN.<br>GREAT COMPANY.</span></div></section>
<section id="collection"><div class="section-heading"><div><span class="eyebrow">CURATED FOR YOU</span><h2>The collection <small>{{filtered().length}} items</small></h2></div><a routerLink="/cart">View your bag →</a></div>
<div class="filters"><label class="search">Search products<input type="search" [ngModel]="query()" (ngModelChange)="query.set($event)" placeholder="Find your next favorite…"></label><label>Category<select [ngModel]="category()" (ngModelChange)="category.set($event)"><option value="">All categories</option>@for(c of categories();track c){<option>{{c}}</option>}</select></label><label>Sort by<select [ngModel]="sort()" (ngModelChange)="sort.set($event)"><option value="name">Name</option><option value="low">Price: low to high</option><option value="high">Price: high to low</option></select></label></div>
@if(error()){<p class="error" role="alert">{{error()}}</p><button (click)="load()">Retry</button>}
@if(loading()){<p role="status">Loading collection…</p>}
<p class="notice" aria-live="polite">{{notice()}}</p>
<div class="product-grid">@for(p of filtered();track p.id){<article class="product-card"><a [routerLink]="['/products',p.id]" class="product-image"><img [src]="p.imageUrl || placeholder" [alt]="p.name" loading="lazy" (error)="imageError($event)"></a><div class="product-info"><span class="eyebrow">{{p.category}}</span><h3><a [routerLink]="['/products',p.id]">{{p.name}}</a></h3><p>{{p.description}}</p><div class="between"><strong>{{p.price|currency:'USD'}}</strong><button class="secondary" [disabled]="p.stockQuantity===0" (click)="add(p)">{{p.stockQuantity===0?'Sold out':'Add to bag +'}}</button></div><small class="muted">{{p.stockQuantity}} available</small></div></article>}@empty{ @if(!loading()&&!error()){<p>No matching products. Try another search.</p>} }</div></section>
`})
export class CatalogPage implements OnInit {
  private readonly api=inject(Api);readonly cart=inject(Cart);readonly products=signal<Product[]>([]);readonly loading=signal(true);readonly error=signal('');readonly notice=signal('');
  readonly query=signal('');readonly category=signal('');readonly sort=signal('name');
  readonly placeholder='data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="400" height="300"%3E%3Crect width="400" height="300" fill="%23e8ece4"/%3E%3C/svg%3E';
  readonly categories=computed(()=>[...new Set(this.products().map(p=>p.category))].sort());
  readonly filtered=computed(()=>this.products().filter(p=>(!this.category()||p.category===this.category())&&(p.name+' '+p.description).toLowerCase().includes(this.query().toLowerCase())).sort((a,b)=>this.sort()==='low'?a.price-b.price:this.sort()==='high'?b.price-a.price:a.name.localeCompare(b.name)));
  ngOnInit(){void this.load();}async load(){this.loading.set(true);this.error.set('');try{this.products.set(await this.api.products());}catch(e){this.error.set(message(e));}finally{this.loading.set(false);}}
  add(p:Product){this.cart.add(p);this.notice.set(p.name+' added to your bag.');}
  imageError(event:Event){(event.target as HTMLImageElement).src=this.placeholder;}
}
@Component({standalone:true,imports:[CurrencyPipe,RouterLink],template:`
<a routerLink="/products">← Back to collection</a>
@if(error()){<p class="error" role="alert">{{error()}}</p>}
@if(product();as p){<div class="detail"><img [src]="p.imageUrl" [alt]="p.name"><section><span class="eyebrow">{{p.category}}</span><h1>{{p.name}}</h1><p>{{p.description}}</p><h2>{{p.price|currency:'USD'}}</h2><p>{{p.stockQuantity}} available · USD · Free demo shipping</p><button class="primary" [disabled]="!p.stockQuantity" (click)="cart.add(p);added.set(true)">Add to bag +</button>@if(added()){<p role="status">Added to bag. <a routerLink="/cart">View bag →</a></p>}</section></div>}@else if(!error()){<p role="status">Loading product…</p>}
`})
export class DetailPage implements OnInit {
  readonly cart=inject(Cart);private readonly api=inject(Api);private readonly route=inject(ActivatedRoute);
  readonly product=signal<Product|null>(null);readonly error=signal('');readonly added=signal(false);
  async ngOnInit(){try{this.product.set(await this.api.product(this.route.snapshot.paramMap.get('id')!));}catch(e){this.error.set(message(e));}}
}
