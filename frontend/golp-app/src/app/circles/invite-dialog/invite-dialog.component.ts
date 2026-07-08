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
  canShare = typeof navigator !== 'undefined' && !!navigator.share;

  ngOnInit(): void {
    this.circleService.getInviteLink(this.circleId).subscribe({
      next: (res) => {
        this.inviteUrl = `${window.location.origin}/join?token=${res.inviteToken}`;
        this.loading = false;
        if (this.canShare) {
          this.shareLink();
        }
      },
      error: () => {
        this.error = 'Impossibile generare il link. Riprova.';
        this.loading = false;
      },
    });
  }

  shareLink(): void {
    navigator
      .share({
        title: `Unisciti a ${this.circleName} su GOLP`,
        url: this.inviteUrl,
      })
      .then(() => this.close())
      .catch(() => {
        // utente ha annullato la condivisione: nessuna azione necessaria
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
