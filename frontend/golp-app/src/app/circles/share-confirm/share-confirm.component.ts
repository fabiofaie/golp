import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmationLink } from '../match.service';

@Component({
  selector: 'app-share-confirm',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './share-confirm.component.html',
  styles: [`
    .share-confirm-list {
      display: flex;
      flex-direction: column;
      border: 1px solid var(--color-border);
      border-radius: 10px;
      overflow: hidden;
      background: var(--color-surface);
    }

    .share-confirm-item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      padding: 14px 16px;
      border-bottom: 1px solid var(--color-border);
    }

    .share-confirm-item:last-child {
      border-bottom: none;
    }

    .share-name {
      font-size: 14px;
      font-weight: 600;
      flex: 1;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .btn-share,
    .btn-whatsapp {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      flex-shrink: 0;
      padding: 8px 14px;
      border-radius: 8px;
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
      text-decoration: none;
      transition: opacity 0.15s;
    }

    .btn-share {
      background: none;
      border: 1px solid var(--color-border);
      color: var(--color-text);
    }

    .btn-share:hover {
      border-color: var(--color-accent);
      color: var(--color-accent);
    }

    .btn-whatsapp {
      background: #25D366;
      border: none;
      color: #fff;
    }

    .btn-whatsapp:hover {
      opacity: 0.88;
    }

    .share-fallback {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-shrink: 0;
    }

    .link-input {
      padding: 7px 10px;
      border-radius: 6px;
      border: 1px solid var(--color-border);
      background: var(--color-bg);
      color: var(--color-text);
      font-size: 12px;
      width: 140px;
    }

    .btn-copy {
      padding: 7px 12px;
      border-radius: 6px;
      border: 1px solid var(--color-border);
      background: var(--color-surface);
      color: var(--color-text);
      font-size: 12px;
      font-weight: 600;
      cursor: pointer;
      white-space: nowrap;
    }

    .btn-copy:hover {
      border-color: var(--color-accent);
    }
  `]
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
