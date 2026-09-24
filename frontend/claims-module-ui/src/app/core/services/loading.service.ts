import { Injectable, computed, signal } from '@angular/core';

/**
 * Counts API requests in flight, so the shell can show one loading bar for every async
 * operation (brief §3.7.4). Views keep their own spinners or skeletons for first loads;
 * this covers actions such as transitions, approvals and uploads.
 */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly inFlight = signal(0);

  readonly isLoading = computed(() => this.inFlight() > 0);

  start(): void {
    this.inFlight.update((n) => n + 1);
  }

  stop(): void {
    this.inFlight.update((n) => Math.max(0, n - 1));
  }
}
