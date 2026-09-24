import { Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

export type SnackbarSeverity = 'success' | 'error' | 'warning' | 'info';

@Injectable({ providedIn: 'root' })
export class SnackbarService {
  constructor(private readonly snackBar: MatSnackBar) {}

  show(message: string, severity: SnackbarSeverity = 'info'): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: severity === 'error' ? 8000 : 4000,
      panelClass: [`snackbar-${severity}`],
      horizontalPosition: 'right',
      verticalPosition: 'top'
    });
  }

  success(message: string): void {
    this.show(message, 'success');
  }

  error(message: string): void {
    this.show(message, 'error');
  }

  warning(message: string): void {
    this.show(message, 'warning');
  }
}
