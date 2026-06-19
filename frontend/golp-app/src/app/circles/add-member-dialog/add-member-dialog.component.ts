import { Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CircleService } from '../circle.service';

type Step = 'email' | 'confirmExisting' | 'newPlayer' | 'success';

@Component({
  selector: 'app-add-member-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './add-member-dialog.component.html',
  styleUrl: './add-member-dialog.component.scss',
})
export class AddMemberDialogComponent {
  @Input() circleId = '';
  @Input() circleName = '';
  @Output() closed = new EventEmitter<void>();

  private readonly circleService = inject(CircleService);

  step: Step = 'email';
  email = '';
  name = '';
  existingName = '';
  loading = false;
  error = '';
  successMessage = '';

  submitEmail(): void {
    if (!this.email.trim()) return;
    this.loading = true;
    this.error = '';

    this.circleService.checkOrAddMember(this.circleId, this.email.trim()).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.exists) {
          this.existingName = res.name ?? '';
          this.step = 'confirmExisting';
        } else {
          this.step = 'newPlayer';
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.error ?? 'Formato email non valido.';
      },
    });
  }

  confirmExisting(): void {
    this.loading = true;
    this.error = '';

    this.circleService.checkOrAddMember(this.circleId, this.email.trim(), undefined, true).subscribe({
      next: (res) => {
        this.loading = false;
        this.successMessage = res.alreadyMember
          ? `${this.existingName} è già membro del circolo.`
          : `${this.existingName} è stato aggiunto al circolo.`;
        this.step = 'success';
      },
      error: () => {
        this.loading = false;
        this.error = 'Impossibile aggiungere il giocatore. Riprova.';
      },
    });
  }

  submitNewPlayer(): void {
    if (!this.name.trim()) return;
    this.loading = true;
    this.error = '';

    this.circleService.checkOrAddMember(this.circleId, this.email.trim(), this.name.trim()).subscribe({
      next: () => {
        this.loading = false;
        this.successMessage = `${this.name.trim()} è stato invitato al circolo. Riceverà una email con le istruzioni per attivare l'account.`;
        this.step = 'success';
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.error ?? 'Impossibile creare il giocatore. Riprova.';
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
