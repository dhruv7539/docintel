import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './auth.component.html',
  styleUrls: ['./auth.component.scss'],
})
export class AuthComponent {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);

  @Output() authenticated = new EventEmitter<void>();

  mode = signal<'login' | 'register'>('register');
  workspaceName = '';
  email = '';
  password = '';
  loading = signal(false);
  error = signal<string | null>(null);

  toggle(): void {
    this.mode.set(this.mode() === 'login' ? 'register' : 'login');
    this.error.set(null);
  }

  submit(): void {
    this.loading.set(true);
    this.error.set(null);

    const request$ =
      this.mode() === 'register'
        ? this.api.register(this.workspaceName, this.email, this.password)
        : this.api.login(this.email, this.password);

    request$.subscribe({
      next: (res) => {
        this.auth.setSession(res);
        this.loading.set(false);
        this.authenticated.emit();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.error ?? 'Authentication failed. Check your details and try again.');
      },
    });
  }
}
