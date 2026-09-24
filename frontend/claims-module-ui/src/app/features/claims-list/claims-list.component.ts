import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, Subscription } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { ClaimsService } from '../../core/services/claims.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { ClaimListItemDto } from '../../shared/models/claim.models';
import { ClaimStatus, ClaimStatusLabels, optionsByLabel } from '../../shared/models/enums';
import { CauseOfLossCodeDto } from '../../shared/models/reference-data.models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { SlaBadgeComponent } from '../../shared/components/sla-badge/sla-badge.component';
import { claimStatusBadge } from '../../shared/models/status-badge.util';
import { FIELD_LIMITS } from '../../shared/models/field-limits';

@Component({
  selector: 'app-claims-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    StatusBadgeComponent,
    SlaBadgeComponent
  ],
  templateUrl: './claims-list.component.html',
  styleUrl: './claims-list.component.scss'
})
export class ClaimsListComponent implements OnInit {
  readonly limits = FIELD_LIMITS;
  readonly displayedColumns = ['claimNumber', 'policyNumber', 'clientName', 'lossDate', 'causeOfLoss', 'status', 'totalReserve', 'actions'];
  readonly statusOptions = optionsByLabel(ClaimStatusLabels);
  readonly statusLabels = ClaimStatusLabels;
  readonly causeOfLossCodes = signal<CauseOfLossCodeDto[]>([]);

  readonly claims = signal<ClaimListItemDto[]>([]);
  readonly loading = signal(true);
  readonly totalCount = signal(0);
  readonly pageNumber = signal(1);
  readonly pageSize = signal(20);

  selectedStatuses: ClaimStatus[] = [];
  fromDate: Date | null = null;
  toDate: Date | null = null;
  assignedHandler = '';
  causeOfLossCodeId = '';

  private readonly handlerFilterChanged = new Subject<string>();
  // Cancelled on each new load so a slow earlier response can't overwrite newer filter results.
  private loadSubscription?: Subscription;

  constructor(
    private readonly claimsService: ClaimsService,
    private readonly referenceData: ReferenceDataService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.referenceData.listCauseOfLossCodes().subscribe((codes) => this.causeOfLossCodes.set(codes));
    this.handlerFilterChanged.pipe(debounceTime(400)).subscribe(() => this.applyFilters());
    this.load();
  }

  onHandlerFilterChange(value: string): void {
    this.handlerFilterChanged.next(value);
  }

  load(): void {
    this.loading.set(true);
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = this.claimsService
      .list({
        statuses: this.selectedStatuses.length ? this.selectedStatuses : undefined,
        fromDate: this.fromDate?.toISOString(),
        toDate: this.toDate?.toISOString(),
        assignedHandler: this.assignedHandler || undefined,
        causeOfLossCodeId: this.causeOfLossCodeId || undefined,
        pageNumber: this.pageNumber(),
        pageSize: this.pageSize()
      })
      .subscribe({
        next: (result) => {
          this.claims.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      });
  }

  applyFilters(): void {
    this.pageNumber.set(1);
    this.load();
  }

  clearFilters(): void {
    this.selectedStatuses = [];
    this.fromDate = null;
    this.toDate = null;
    this.assignedHandler = '';
    this.causeOfLossCodeId = '';
    this.applyFilters();
  }

  onPage(event: PageEvent): void {
    this.pageNumber.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  openClaim(claim: ClaimListItemDto): void {
    this.router.navigate(['/claims', claim.id]);
  }

  newClaim(): void {
    this.router.navigate(['/claims/new']);
  }

  badgeFor(status: ClaimStatus) {
    return claimStatusBadge(status);
  }
}
