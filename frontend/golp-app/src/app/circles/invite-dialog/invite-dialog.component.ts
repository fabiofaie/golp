import { Component, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CircleService } from '../circle.service';

@Component({
  selector: 'app-invite-dialog',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './invite-dialog.component.html',
  styleUrl: './invite-dialog.component.scss',
})
export class InviteDialogComponent implements OnInit {
  @Input() circleId = '';
  @Input() circleName = '';
  @Output() closed = new EventEmitter<void>();

  private readonly circleService = inject(CircleService);

  inviteUrl = '';
  loading = true;
  error = '';
  copied = false;

  ngOnInit(): void {
    this.circleService.getInviteLink(this.circleId).subscribe({
      next: (res) => {
        this.inviteUrl = `${window.location.origin}/join?token=${res.inviteToken}`;
        this.loading = false;
      },
      error: () => {
        this.error = 'Impossibile generare il link. Riprova.';
        this.loading = false;
      },
    });
  }

  copyLink(): void {
    navigator.clipboard.writeText(this.inviteUrl).then(() => {
      this.copied = true;
      setTimeout(() => (this.copied = false), 2000);
    });
  }

  sendEmail(): void {
    const subject = encodeURIComponent(`Unisciti a ${this.circleName} su GOLP`);
    const body = encodeURIComponent(this.inviteUrl);
    window.open(`mailto:?subject=${subject}&body=${body}`, '_blank');
  }

  close(): void {
    this.closed.emit();
  }
}
