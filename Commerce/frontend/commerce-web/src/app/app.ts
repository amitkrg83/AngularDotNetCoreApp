import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Auth, Cart, message } from './core';
@Component({selector:'app-root',standalone:true,imports:[RouterLink,RouterLinkActive,RouterOutlet],template:`
<a class="skip" href="#main">Skip to content</a>
<header><a class="brand" routerLink="/products"><span class="mark">C</span> commerce<span class="dot">.</span></a>
<nav aria-label="Main navigation">
@if(auth.user()) {
  <a routerLink="/products" routerLinkActive="selected">Discover</a>
  <a routerLink="/orders" routerLinkActive="selected">Orders</a>
  @if(auth.user()?.role==='Admin'){<a routerLink="/admin" routerLinkActive="selected">Admin</a>}
  <a routerLink="/cart" routerLinkActive="selected">Bag <span class="count">{{cart.count()}}</span></a>
  <button class="link" (click)="logout()" [disabled]="busy()">Sign out</button>
} @else {<a routerLink="/login">Sign in</a>}
</nav></header>
@if(error()){<p role="alert" class="error wrap">{{error()}}</p>}
<main id="main" class="wrap"><router-outlet/></main>
<footer class="wrap"><span>Commerce · Everyday, considered.</span><span>Learning demo · USD · No real charges</span></footer>
`})
export class App {
  readonly auth=inject(Auth);readonly cart=inject(Cart);private readonly router=inject(Router);
  readonly busy=signal(false);readonly error=signal('');
  async logout(){this.busy.set(true);try{await this.auth.logout();await this.router.navigateByUrl('/login');}catch(e){this.error.set(message(e));}finally{this.busy.set(false);}}
}
