import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmationLink, MatchService } from '../match.service';

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
      gap: 12px;
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

    .share-actions {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-shrink: 0;
    }

    .btn-share,
    .btn-whatsapp,
    .btn-whatsapp-ghost {
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
      border: none;
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
      color: #fff;
    }

    .btn-whatsapp:hover { opacity: 0.88; }

    .btn-whatsapp-ghost {
      background: none;
      border: 1px solid var(--color-border);
      color: var(--color-text-placeholder);
    }

    .btn-whatsapp-ghost:hover {
      border-color: #25D366;
      color: #25D366;
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

    .btn-copy:hover { border-color: var(--color-accent); }
  `]
})
export class ShareConfirmComponent {
  @Input() links: ConfirmationLink[] = [];
  @Input() sport = '';
  @Input() circleName = '';

  private readonly matchSvc = inject(MatchService);

  readonly hasShare = typeof navigator !== 'undefined' && 'share' in navigator;
  readonly hasContactPicker =
    typeof navigator !== 'undefined' &&
    'contacts' in navigator &&
    'ContactsManager' in window;

  copiedUserId: string | null = null;

  waUrl(phone: string, name: string, tokenUrl: string): string {
    const cleaned = phone.replace(/\D/g, '');
    const text = encodeURIComponent(
      `Ciao ${name}! Ho registrato una partita di ${this.sport} nel circolo ${this.circleName}. Conferma il risultato qui: ${tokenUrl}`
    );
    return `https://wa.me/${cleaned}?text=${text}`;
  }

  async onWaGhostClick(link: ConfirmationLink): Promise<void> {
    if (!this.hasContactPicker) return;
    try {
      const contacts: Array<{ name?: string[]; tel?: string[] }> =
        await (navigator as any).contacts.select(['name', 'tel'], { multiple: false });
      if (!contacts.length || !contacts[0].tel?.length) return;

      const phone = contacts[0].tel[0].trim();

      if (!link.isActivated) {
        // ospite: salva nel DB
        this.matchSvc.patchGuestPhone(link.userId, phone).subscribe({
          next: (res) => {
            const idx = this.links.findIndex(l => l.userId === link.userId);
            if (idx !== -1) {
              this.links = this.links.map((l, i) =>
                i === idx ? { ...l, phone: res.phone } : l
              );
            }
            window.open(this.waUrl(phone, link.name, link.tokenUrl), '_blank', 'noopener');
          },
        });
      } else {
        // utente registrato: apri WA senza salvare
        window.open(this.waUrl(phone, link.name, link.tokenUrl), '_blank', 'noopener');
      }
    } catch {
      // utente ha annullato — no-op
    }
  }

  async shareLink(link: ConfirmationLink): Promise<void> {
    try {
      await navigator.share({
        title: 'Conferma partita',
        text: `Ciao ${link.name}! Ho registrato una partita di ${this.sport} nel circolo ${this.circleName}. Conferma il risultato qui:`,
        url: link.tokenUrl,
      });
    } catch {
      // utente ha annullato — no-op
    }
  }

  async copyToClipboard(link: ConfirmationLink): Promise<void> {
    try {
      await navigator.clipboard.writeText(link.tokenUrl);
      this.copiedUserId = link.userId;
      setTimeout(() => { this.copiedUserId = null; }, 2000);
    } catch {
      // clipboard non disponibile — no-op
    }
  }
}
