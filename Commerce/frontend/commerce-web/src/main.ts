import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { App } from './app/app';
import { routes } from './app/routes';
import { errorInterceptor } from './app/core';
bootstrapApplication(App, { providers: [provideHttpClient(withInterceptors([errorInterceptor])), provideRouter(routes)] }).catch(console.error);
