import { Component, ElementRef, OnInit, signal } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { AbstractControl, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse, HttpEventType } from '@angular/common/http';

import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDialog } from '@angular/material/dialog';

import { ClaimsService } from '../../core/services/claims.service';
import { ReservesService } from '../../core/services/reserves.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { AuthService } from '../../core/services/auth.service';
import { SnackbarService } from '../../shared/services/snackbar.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PhoneInputDirective } from '../../shared/directives/phone-input.directive';
import { CurrencyInputDirective } from '../../shared/directives/currency-input.directive';
import { contactEmailMessage, contactEmailValidator } from '../../shared/validators/email.validator';
import { SlaBadgeComponent } from '../../shared/components/sla-badge/sla-badge.component';
import { ConfirmDialogComponent, ConfirmDialogResult } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { claimStatusBadge, approvalStatusBadge, postingStatusBadge } from '../../shared/models/status-badge.util';
import { ClaimAuditLogDto, ClaimDetailDto, ClaimDocumentDto } from '../../shared/models/claim.models';
import { ReserveComponentDto, ReserveHistoryDto } from '../../shared/models/reserve.models';
import { reserveAmountSignMessage, reserveAmountSignValidator } from '../../shared/validators/reserve-amount.validator';
import {
  ApprovalStatus,
  ApprovalTier,
  ClaimStatus,
  ClaimStatusLabels,
  ClaimTypeLabels,
  DocumentType,
  DocumentTypeLabels,
  PartyRole,
  PartyRoleLabels,
  PartyType,
  PartyTypeLabels,
  ReserveComponentType,
  ReserveComponentTypeLabels,
  canApproveTier,
  estimateApprovalTier,
  optionsByLabel,
  sortByLabel
} from '../../shared/models/enums';
import { ClaimStatusDto } from '../../shared/models/reference-data.models';
import { ApiErrorResponse, PagedList } from '../../shared/models/common.models';
import { isAggregateCapRejection } from '../../core/interceptors/error-handling-context';
import { resolveDocumentUrl } from '../../core/config/api-config';
import { FIELD_LIMITS } from '../../shared/models/field-limits';

@Component({
  selector: 'app-claim-detail',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatProgressBarModule,
    MatPaginatorModule,
    StatusBadgeComponent,
    SlaBadgeComponent,
    PhoneInputDirective,
    CurrencyInputDirective
  ],
  providers: [CurrencyPipe],
  templateUrl: './claim-detail.component.html',
  styleUrl: './claim-detail.component.scss'
})
export class ClaimDetailComponent implements OnInit {
  readonly limits = FIELD_LIMITS;
  readonly claim = signal<ClaimDetailDto | null>(null);
  selectedTabIndex = 0;
  readonly loading = signal(true);
  readonly allowedNextStatuses = signal<ClaimStatus[]>([]);
  readonly claimStatusLabels = ClaimStatusLabels;
  readonly claimTypeLabels = ClaimTypeLabels;

  readonly partyTypeLabels = PartyTypeLabels;
  readonly partyRoleLabels = PartyRoleLabels;
  readonly reserveComponentTypeLabels = ReserveComponentTypeLabels;
  readonly reserveComponentTypes = optionsByLabel(ReserveComponentTypeLabels);
  readonly partyTypes = optionsByLabel(PartyTypeLabels);
  readonly partyRoles = optionsByLabel(PartyRoleLabels);
  readonly ApprovalStatus = ApprovalStatus;
  readonly ApprovalTier = ApprovalTier;
  readonly recoveryReserve = ReserveComponentType.RecoveryReserve;
  readonly documentTypeLabels = DocumentTypeLabels;
  readonly documentTypes = optionsByLabel(DocumentTypeLabels);

  // Parties tab
  readonly addingParty = signal(false);
  partyForm: FormGroup;

  // Reserves tab
  readonly showReservePanel = signal(false);
  readonly reservePanelMode = signal<'open' | 'adjust'>('open');
  /** The component being adjusted (adjust mode only) — its currentAmount is the "before" value. */
  readonly adjustingComponent = signal<ReserveComponentDto | null>(null);
  reserveForm: FormGroup;

  // Documents tab
  readonly documents = signal<ClaimDocumentDto[]>([]);
  readonly uploadProgress = signal<number | null>(null);
  readonly documentTypeControl = new FormControl<DocumentType>(DocumentType.Other, { nonNullable: true });

  // Audit log tab
  readonly auditEntries = signal<ClaimAuditLogDto[]>([]);
  readonly auditTotalCount = signal(0);
  readonly auditPageNumber = signal(1);
  readonly auditPageSize = signal(20);
  readonly auditLoading = signal(false);

  private claimId = '';

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly claimsService: ClaimsService,
    private readonly reservesService: ReservesService,
    private readonly referenceData: ReferenceDataService,
    readonly auth: AuthService,
    private readonly snackbar: SnackbarService,
    private readonly dialog: MatDialog,
    private readonly fb: FormBuilder,
    private readonly currencyPipe: CurrencyPipe,
    private readonly host: ElementRef<HTMLElement>
  ) {
    this.partyForm = this.fb.group({
      partyType: [PartyType.Individual, Validators.required],
      partyRole: [PartyRole.Claimant, Validators.required],
      name: ['', Validators.required],
      contactEmail: ['', contactEmailValidator()],
      contactPhone: ['']
    });

    this.reserveForm = this.fb.group({
      reserveComponentId: [''],
      componentType: [ReserveComponentType.IndemnityReserve],
      amount: [null as number | null, [Validators.required, reserveAmountSignValidator(), (c: AbstractControl) => this.differsFromCurrent(c)]],
      reason: ['', Validators.maxLength(1000)],
      currency: ['USD', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]]
    });

    // The sign rule depends on the component type, so re-check the amount when the type changes.
    this.reserveForm.get('componentType')!.valueChanges.subscribe(() => this.reserveForm.get('amount')!.updateValueAndValidity());
  }

  ngOnInit(): void {
    this.claimId = this.route.snapshot.paramMap.get('id')!;
    this.loadClaim();
    this.loadDocuments();
    this.loadAuditLog();
  }

  private loadClaim(): void {
    this.loading.set(true);
    this.claimsService.getById(this.claimId).subscribe({
      next: (claim) => {
        this.claim.set(claim);
        this.loading.set(false);
        this.loadAllowedNextStatuses(claim.status);
      },
      error: () => this.loading.set(false)
    });
  }

  private loadAllowedNextStatuses(currentStatus: ClaimStatus): void {
    this.referenceData.listClaimStatuses().subscribe((statuses: ClaimStatusDto[]) => {
      const entry = statuses.find((s) => s.status === currentStatus);
      this.allowedNextStatuses.set(sortByLabel(entry?.allowedNextStatuses ?? [], ClaimStatusLabels));
    });
  }

  statusBadge(status: ClaimStatus) {
    return claimStatusBadge(status);
  }

  approvalBadge(status: ApprovalStatus) {
    return approvalStatusBadge(status);
  }

  postingBadge(status: ReserveHistoryDto['postingStatus']) {
    return postingStatusBadge(status);
  }

  transitionTo(newStatus: ClaimStatus): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Change claim status',
        message: `Transition this claim from ${this.claimStatusLabels[this.claim()!.status]} to ${this.claimStatusLabels[newStatus]}?`,
        confirmLabel: 'Transition'
      }
    });

    dialogRef.afterClosed().subscribe((result: ConfirmDialogResult) => {
      if (!result?.confirmed) return;

      this.claimsService.transitionStatus(this.claimId, newStatus).subscribe((claim) => {
        this.claim.set(claim);
        this.loadAllowedNextStatuses(claim.status);
        this.loadAuditLog();
        this.snackbar.success(`Claim status updated to ${this.claimStatusLabels[claim.status]}.`);
      });
    });
  }

  // ---- Parties ----

  toggleAddParty(): void {
    this.addingParty.set(!this.addingParty());
  }

  partyEmailError(): string {
    return contactEmailMessage(this.partyForm.get('contactEmail')?.errors) ?? '';
  }

  submitParty(): void {
    if (this.partyForm.invalid) {
      this.partyForm.markAllAsTouched();
      return;
    }

    this.claimsService.addParty(this.claimId, this.partyForm.value).subscribe(() => {
      this.snackbar.success('Party added.');
      this.partyForm.reset({ partyType: PartyType.Individual, partyRole: PartyRole.Claimant, name: '', contactEmail: '', contactPhone: '' });
      this.addingParty.set(false);
      this.loadClaim();
      this.loadAuditLog();
    });
  }

  // ---- Reserves ----

  openNewReservePanel(): void {
    this.reservePanelMode.set('open');
    this.adjustingComponent.set(null);
    this.setReasonRequired(false);
    this.reserveForm.reset({ reserveComponentId: '', componentType: ReserveComponentType.IndemnityReserve, amount: null, reason: '', currency: 'USD' });
    this.showReservePanel.set(true);
  }

  /** Adjust takes the component's NEW TOTAL (prefilled with the current amount) plus a mandatory reason. */
  openAdjustReservePanel(component: ReserveComponentDto): void {
    this.reservePanelMode.set('adjust');
    this.adjustingComponent.set(component);
    this.setReasonRequired(true);
    this.reserveForm.reset({
      reserveComponentId: component.id,
      componentType: component.componentType,
      amount: component.currentAmount,
      reason: '',
      currency: component.currency
    });
    this.showReservePanel.set(true);
    // The panel renders above the reserve cards, so the Adjust button can be far below it.
    setTimeout(() => this.host.nativeElement.querySelector('.reserve-panel')?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
  }

  /**
   * The API accepts one outstanding change per component: while one is pending approval another
   * adjustment is rejected (422), so Adjust is disabled until that change is approved or rejected.
   */
  hasPendingChange(component: ReserveComponentDto): boolean {
    return component.history.some((h) => h.approvalStatus === ApprovalStatus.PendingApproval);
  }

  currencyError(): string {
    const errors = this.reserveForm.get('currency')?.errors;
    if (!errors) return '';
    if (errors['required']) return 'Enter a currency.';
    if (errors['server']) return errors['server'] as string;
    return 'Use a 3-letter currency code, e.g. USD.';
  }

  closeReservePanel(): void {
    this.showReservePanel.set(false);
    this.adjustingComponent.set(null);
  }

  private setReasonRequired(required: boolean): void {
    const reason = this.reserveForm.get('reason')!;
    reason.setValidators(required ? [Validators.required, Validators.maxLength(1000), notBlank] : [Validators.maxLength(1000)]);
    reason.updateValueAndValidity();
  }

  /** Adjusting to the amount the component already has is a no-op the API rejects (422). */
  private differsFromCurrent(control: AbstractControl): ValidationErrors | null {
    const current = this.adjustingComponent()?.currentAmount;
    if (current === undefined || control.value === null || control.value === '') return null;
    return Number(control.value) === current ? { sameAsCurrent: true } : null;
  }

  /** Authority tier is evaluated on the amount being submitted — for an adjust, the new total. */
  get reserveTierPreview(): string {
    const amount = this.reserveForm.get('amount')?.value;
    if (!amount) return '';
    return estimateApprovalTier(Math.abs(amount));
  }

  get amountLabel(): string {
    return this.reservePanelMode() === 'adjust' ? 'New reserve amount' : 'Amount';
  }

  amountError(): string {
    const errors = this.reserveForm.get('amount')?.errors;
    if (!errors) return '';
    if (errors['required']) return 'Enter an amount.';
    const sign = reserveAmountSignMessage(errors);
    if (sign) return sign;
    if (errors['sameAsCurrent']) return 'The new amount must differ from the current amount.';
    if (errors['server']) return errors['server'] as string;
    return 'This amount is not valid.';
  }

  reasonError(): string {
    const errors = this.reserveForm.get('reason')?.errors;
    if (!errors) return '';
    if (errors['required'] || errors['blank']) return 'A reason for the change is required.';
    if (errors['maxlength']) return 'The reason must be 1000 characters or fewer.';
    if (errors['server']) return errors['server'] as string;
    return 'This reason is not valid.';
  }

  submitReserve(managerOverride = false): void {
    if (this.reserveForm.invalid) {
      this.reserveForm.markAllAsTouched();
      return;
    }
    const { reserveComponentId, componentType, amount, currency } = this.reserveForm.value;
    const reason = ((this.reserveForm.value.reason as string | null) ?? '').trim();
    const override = managerOverride ? { managerOverrideConfirmed: true } : {};
    // As with approvals, only a Manager is offered the $10M-cap override (BR-R-07), so only then
    // does this component handle the cap error instead of the global snackbar.
    const offersOverride = !managerOverride && this.auth.role()?.toLowerCase() === 'manager';
    const options = { callerHandlesAggregateCap: offersOverride };

    const request$ =
      this.reservePanelMode() === 'open'
        ? this.reservesService.open(this.claimId, { componentType, amount, currency, ...(reason ? { reason } : {}), ...override }, options)
        : this.reservesService.adjust(this.claimId, reserveComponentId, { amount, reason, currency, ...override }, options);

    request$.subscribe({
      next: (result) => {
        this.snackbar.success(managerOverride ? 'Reserve submitted with manager override.' : 'Reserve submitted.');
        for (const warning of result.warnings) {
          this.snackbar.warning(warning.message);
        }
        this.closeReservePanel();
        this.loadClaim();
        this.loadAuditLog();
      },
      error: (error: unknown) => {
        if (offersOverride && isAggregateCapRejection(error)) {
          this.confirmReserveOverride();
          return;
        }
        this.applyReserveServerErrors(error);
      }
    });
  }

  private confirmReserveOverride(): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Manager override required',
        message: "This takes the claim's aggregate reserve over $10M — submit with manager override?",
        confirmLabel: 'Submit with override'
      }
    });

    dialogRef.afterClosed().subscribe((result: ConfirmDialogResult) => {
      if (result?.confirmed) this.submitReserve(true);
    });
  }

  /** Maps a 400 ProblemDetails `errors` dictionary keyed by "Amount" / "Reason" onto the panel's inline errors. */
  private applyReserveServerErrors(error: unknown): void {
    if (!(error instanceof HttpErrorResponse) || error.status !== 400) return;
    const errors = (error.error as ApiErrorResponse | null)?.errors ?? {};
    for (const [key, messages] of Object.entries(errors)) {
      const control = this.reserveForm.get(key.charAt(0).toLowerCase() + key.slice(1));
      if (!control || !['amount', 'reason'].includes(key.toLowerCase())) continue;
      control.setErrors({ ...(control.errors ?? {}), server: messages.join(' ') });
      control.markAsTouched();
    }
  }

  /** "+$10,000.00" / "-$5,000.00" — a reserve change with its sign. */
  signedChange(history: ReserveHistoryDto): string {
    const formatted = this.currencyPipe.transform(Math.abs(history.amount), history.currency) ?? String(Math.abs(history.amount));
    return `${history.amount < 0 ? '-' : '+'}${formatted}`;
  }

  private money(amount: number, currency: string): string {
    return this.currencyPipe.transform(amount, currency) ?? `${amount} ${currency}`;
  }

  /**
   * Approve/Reject is only offered on pending changes the current role has authority over:
   * Supervisors on Supervisor-tier changes, Managers on every tier (mirrors ReserveAuthorityEvaluator).
   */
  canDecide(history: ReserveHistoryDto): boolean {
    if (history.approvalStatus !== ApprovalStatus.PendingApproval) return false;
    // The authority tier is evaluated on the new total, not the delta.
    const tier = history.requiredTier ?? ApprovalTier[estimateApprovalTier(Math.abs(history.newAmount ?? history.amount))];
    return canApproveTier(this.auth.role(), tier);
  }

  /** Four-eyes rule (the API answers 403): nobody approves a change they requested themselves. Rejecting it is allowed. */
  isOwnRequest(history: ReserveHistoryDto): boolean {
    const me = this.auth.currentUser()?.userName;
    return !!me && history.requestedBy.localeCompare(me, undefined, { sensitivity: 'accent' }) === 0;
  }

  /** Newest change first. */
  historyNewestFirst(component: ReserveComponentDto): ReserveHistoryDto[] {
    return [...component.history].sort((a, b) => b.changeSequence - a.changeSequence);
  }

  private describeChange(history: ReserveHistoryDto): string {
    return `${this.money(history.previousAmount, history.currency)} → ${this.money(history.newAmount, history.currency)} (${this.signedChange(history)})`;
  }

  approveReserve(component: ReserveComponentDto, history: ReserveHistoryDto): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Approve reserve change',
        message: `Approve the reserve change ${this.describeChange(history)} requested by ${history.requestedBy}?`,
        confirmLabel: 'Approve'
      }
    });

    dialogRef.afterClosed().subscribe((result: ConfirmDialogResult) => {
      if (!result?.confirmed) return;
      this.sendApproval(component, history, false);
    });
  }

  private sendApproval(component: ReserveComponentDto, history: ReserveHistoryDto, managerOverride: boolean): void {
    const request = managerOverride ? { reserveHistoryId: history.id, managerOverrideConfirmed: true } : { reserveHistoryId: history.id };
    // Only a Manager is offered the override, so only then does this component own the cap error.
    const offersOverride = !managerOverride && this.auth.role()?.toLowerCase() === 'manager';
    this.reservesService.approve(this.claimId, component.id, request, { callerHandlesAggregateCap: offersOverride }).subscribe({
      next: () => {
        this.snackbar.success(managerOverride ? 'Reserve approved with manager override.' : 'Reserve approved.');
        this.loadClaim();
        this.loadAuditLog();
      },
      error: (error: unknown) => {
        if (offersOverride && isAggregateCapRejection(error)) {
          this.confirmManagerOverride(component, history);
        }
      }
    });
  }

  private confirmManagerOverride(component: ReserveComponentDto, history: ReserveHistoryDto): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Manager override required',
        message: 'Approving takes the claim over $10M — approve with manager override?',
        confirmLabel: 'Approve with override'
      }
    });

    dialogRef.afterClosed().subscribe((result: ConfirmDialogResult) => {
      if (!result?.confirmed) return;
      this.sendApproval(component, history, true);
    });
  }

  rejectReserve(component: ReserveComponentDto, history: ReserveHistoryDto): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Reject reserve change',
        message: `Reject the reserve change ${this.describeChange(history)} requested by ${history.requestedBy}?`,
        confirmLabel: 'Reject',
        requireReason: true
      }
    });

    dialogRef.afterClosed().subscribe((result: ConfirmDialogResult) => {
      if (!result?.confirmed) return;

      this.reservesService.reject(this.claimId, component.id, { reserveHistoryId: history.id, reason: result.reason! }).subscribe(() => {
        this.snackbar.success('Reserve rejected.');
        this.loadClaim();
        this.loadAuditLog();
      });
    });
  }

  // ---- Documents ----

  private loadDocuments(): void {
    this.claimsService.getDocuments(this.claimId).subscribe((docs) => this.documents.set(docs));
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.uploadProgress.set(0);
    this.claimsService.uploadDocument(this.claimId, file, this.documentTypeControl.value).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          this.uploadProgress.set(Math.round((100 * event.loaded) / event.total));
        } else if (event.type === HttpEventType.Response) {
          this.uploadProgress.set(null);
          this.snackbar.success('Document uploaded.');
          this.loadDocuments();
          this.loadAuditLog();
          input.value = '';
        }
      },
      error: () => {
        this.uploadProgress.set(null);
        input.value = '';
      }
    });
  }

  /** Falls back to "Other" for documents an older API returned without a type. */
  documentTypeLabel(type: DocumentType | null | undefined): string {
    return (type !== null && type !== undefined && this.documentTypeLabels[type]) || this.documentTypeLabels[DocumentType.Other];
  }

  documentUrl(downloadUrl: string): string {
    return resolveDocumentUrl(downloadUrl);
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  // ---- Audit Log ----

  loadAuditLog(): void {
    this.auditLoading.set(true);
    this.claimsService.getAuditLog(this.claimId, this.auditPageNumber(), this.auditPageSize()).subscribe({
      next: (result: PagedList<ClaimAuditLogDto>) => {
        this.auditEntries.set(result.items);
        this.auditTotalCount.set(result.totalCount);
        this.auditLoading.set(false);
      },
      error: () => this.auditLoading.set(false)
    });
  }

  onAuditPage(event: PageEvent): void {
    this.auditPageNumber.set(event.pageIndex + 1);
    this.auditPageSize.set(event.pageSize);
    this.loadAuditLog();
  }

  goBack(): void {
    this.router.navigate(['/claims']);
  }
}

/** Rejects whitespace-only text (Validators.required accepts "   "). */
function notBlank(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  return typeof value === 'string' && value.length > 0 && value.trim().length === 0 ? { blank: true } : null;
}
