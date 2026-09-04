import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  standalone: true,
  template: `
    <footer class="footer">
      <span>Ram Food Products, Inc. &copy; {{ year }} &mdash; Head Office Monitoring System</span>
      <span class="version">HOMSys v1.0</span>
    </footer>
  `,
  styles: [`
    .footer {
      height: 36px;
      background: #800000;
      color: rgba(255,255,255,0.65);
      font-size: 0.72rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0 1.25rem;
      flex-shrink: 0;
    }
    .version { color: rgba(255,255,255,0.4); }
  `]
})
export class FooterComponent {
  year = new Date().getFullYear();
}
