import { Component, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';
import { SnackbarService } from '../../shared/services/snackbar.service';

/** Display names for the mock-login role keys returned by GET /api/auth/users. */
const ROLE_LABELS: Record<string, string> = {
  handler: 'Handler',
  supervisor: 'Supervisor',
  manager: 'Manager',
  'orgb-handler': 'Handler (Org B)'
};

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [MatCardModule, MatIconModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit {
  readonly roles = signal<string[]>([]);
  readonly loadingRoles = signal(true);
  readonly signingInAs = signal<string | null>(null);

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly snackbar: SnackbarService
  ) {}

  ngOnInit(): void {
    this.auth.listRoles().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loadingRoles.set(false);
      },
      error: () => {
        this.loadingRoles.set(false);
        this.snackbar.error('Could not reach the Claims Module API. Is it running?');
      }
    });
  }

  roleLabel(roleKey: string): string {
    return ROLE_LABELS[roleKey.toLowerCase()] ?? roleKey;
  }

  signInAs(role: string): void {
    this.signingInAs.set(role);
    this.auth.login(role).subscribe({
      next: () => {
        this.signingInAs.set(null);
        this.router.navigate(['/claims']);
      },
      error: () => this.signingInAs.set(null)
    });
  }
}
