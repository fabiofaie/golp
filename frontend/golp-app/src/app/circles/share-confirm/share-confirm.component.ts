import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmationLink } from '../match.service';

@Component({
  selector: 'app-share-confirm',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './share-confirm.component.html',
})
export class ShareConfirmComponent {
  @Input() links: ConfirmationLink[] = [];
  @Input() sport = '';
  @Input() circleName = '';

  readonly hasShare =
    typeof navigator !== 'undefined' && 'share' in navigator;

  copiedUserId: string | null = null;

  waUrl(link: ConfirmationLink): string {
    const phone = link.phone!.replace(/\D/g, '');
    const text = encodeURIComponent(
      `Ciao ${link.name}! Ho registrato una partita di ${this.sport} nel circolo ${this.circleName}. Conferma il risultato qui: ${link.tokenUrl}`
    );
    return `https://wa.me/${phone}?text=${text}`;
  }

  async shareLink(link: ConfirmationLink): Promise<void> {
    try {
      await navigator.share({
        title: 'Conferma partita',
        text: `Ciao ${link.name}! Ho registrato una partita di ${this.sport} nel circolo ${this.circleName}. Conferma il risultato qui:`,
        url: link.tokenUrl,
      });
    } catch {
      // user cancelled or share not supported — no-op
    }
  }

  async copyToClipboard(link: ConfirmationLink): Promise<void> {
    try {
      await navigator.clipboard.writeText(link.tokenUrl);
      this.copiedUserId = link.userId;
      setTimeout(() => { this.copiedUserId = null; }, 2000);
    } catch {
      // clipboard not available — no-op
    }
  }
}
