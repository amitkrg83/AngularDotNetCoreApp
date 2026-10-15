import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Auth, message } from '../core';
@Component({standalone:true,imports:[ReactiveFormsModule],template:`
<div class="auth-layout"><section class="intro"><span class="eyebrow">YOUR EVERYDAY, UPGRADED</span><h1>Good things.<br>Great beginnings.</h1><p>A little less searching. A little more finding the things you love.</p><div class="intro-art" aria-hidden="true"><span>Everyday<br>essentials.</span></div></section>
<section class="panel auth-card"><span class="eyebrow">WELCOME TO COMMERCE</span><h2>{{registering()?'Create your account':'Make yourself at home'}}</h2><p class="muted">{{registering()?'Your next favorite is waiting.':'Sign in to discover the collection.'}}</p>
<form [formGroup]="form" (ngSubmit)="submit()">
@if(registering()){<label>Name<input formControlName="name" autocomplete="name" maxlength="80"></label>}
<label>Email<input type="email" formControlName="email" autocomplete="username" required maxlength="200"></label>
<label>Password<input type="password" formControlName="password" [autocomplete]="registering()?'new-password':'current-password'" required maxlength="128"></label>
@if(registering()){<small>Use 10–128 characters.</small>}
@if(error()){<p class="error" role="alert">{{error()}}</p>}
<button class="primary full" [disabled]="busy()">{{busy()?'Please wait…':registering()?'Create account':'Sign in →'}}</button>
</form><button class="link" (click)="toggle()">{{registering()?'Already have an account? Sign in':'New here? Create an account'}}</button>
<div class="demo"><strong>Try the demo</strong><p>No setup needed. Products and accounts reset when the API restarts.</p><div class="actions"><button class="secondary" (click)="demo(false)">Customer account</button><button class="secondary" (click)="demo(true)">Admin account</button></div><small>CustomerDemo123! / AdminDemo123!</small></div>
</section></div>
`})
export class LoginPage {
  private readonly auth=inject(Auth);private readonly router=inject(Router);private readonly fb=inject(FormBuilder);
  readonly registering=signal(false);readonly busy=signal(false);readonly error=signal('');
  readonly form=this.fb.nonNullable.group({name:[''],email:['',[Validators.required,Validators.email]],password:['',Validators.required]});
  toggle(){this.registering.update(v=>!v);this.error.set('');}
  demo(admin:boolean){this.registering.set(false);this.form.patchValue({email:admin?'admin@commerce.test':'customer@commerce.test',password:admin?'AdminDemo123!':'CustomerDemo123!'});}
  async submit(){
    this.error.set('');if(this.form.invalid){this.form.markAllAsTouched();this.error.set('Enter a valid email and password.');return;}
    const {name,email,password}=this.form.getRawValue();
    if(this.registering() && (name.trim().length<2 || password.length<10)){this.error.set('Enter a name and a password of at least 10 characters.');return;}
    this.busy.set(true);try{if(this.registering())await this.auth.register(name,email,password);else await this.auth.login(email,password);await this.router.navigateByUrl('/products');}catch(e){this.error.set(message(e));}finally{this.busy.set(false);}
  }
}
