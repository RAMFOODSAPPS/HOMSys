import { ApplicationConfig, provideZoneChangeDetection, provideAppInitializer, inject } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withRouterConfig } from '@angular/router';
import { providePrimeNG } from 'primeng/config';
import { HOMSysTheme } from './core/theme/homsys-theme';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { AuthService } from './core/services/auth.service';
import { followScrollOverlayOptions } from './core/utils/overlay-follow-scroll';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideAnimationsAsync(),
    provideRouter(routes, withRouterConfig({ onSameUrlNavigation: 'reload' })),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideAppInitializer(() => inject(AuthService).initSession()),
    providePrimeNG({
      // Every dropdown / multiselect / autocomplete panel opens on <body> (clipped, never scrolls — see
      // styles.scss). Inside the scrolling page area, focusing the panel's search box scrolled that area,
      // and PrimeNG closes a panel on scroll — so dropdowns lower on a long form closed as they opened.
      // Panels also follow their field when the page scrolls instead of closing (overlay-follow-scroll.ts).
      overlayOptions: { appendTo: 'body', ...followScrollOverlayOptions },
      theme: {
        preset: HOMSysTheme,
        options: {
          darkModeSelector: '.dark-mode'
        }
      }
    })
  ]
};
