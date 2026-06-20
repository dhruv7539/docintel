import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthComponent } from './auth/auth.component';
import { WorkspaceComponent } from './workspace/workspace.component';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, AuthComponent, WorkspaceComponent],
  template: `
    <app-workspace *ngIf="auth.isAuthenticated(); else login" />
    <ng-template #login>
      <app-auth />
    </ng-template>
  `,
  styleUrls: ['./app.component.scss'],
})
export class AppComponent {
  readonly auth = inject(AuthService);
}
