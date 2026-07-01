import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ShareConfirmComponent } from './share-confirm.component';
import { ConfirmationLink } from '../match.service';

const linkWithPhone: ConfirmationLink = {
  userId: 'u1',
  name: 'Marco',
  phone: '+39 340 1234567',
  tokenUrl: 'http://localhost:4200/m/abc-token',
};

const linkNoPhone: ConfirmationLink = {
  userId: 'u2',
  name: 'Sara',
  phone: null,
  tokenUrl: 'http://localhost:4200/m/def-token',
};

describe('ShareConfirmComponent', () => {
  let component: ShareConfirmComponent;
  let fixture: ComponentFixture<ShareConfirmComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ShareConfirmComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ShareConfirmComponent);
    component = fixture.componentInstance;
    component.sport = 'Padel';
    component.circleName = 'Tennis Club';
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('waUrl()', () => {
    it('strips non-digits from phone and builds wa.me URL', () => {
      const url = component.waUrl(linkWithPhone);
      expect(url).toContain('wa.me/393401234567');
    });

    it('encodes name in the message', () => {
      const url = component.waUrl(linkWithPhone);
      expect(url).toContain(encodeURIComponent('Marco'));
    });

    it('encodes sport in the message', () => {
      const url = component.waUrl(linkWithPhone);
      expect(url).toContain(encodeURIComponent('Padel'));
    });

    it('encodes circleName in the message', () => {
      const url = component.waUrl(linkWithPhone);
      expect(url).toContain(encodeURIComponent('Tennis Club'));
    });

    it('includes tokenUrl in the message', () => {
      const url = component.waUrl(linkWithPhone);
      expect(url).toContain(encodeURIComponent('http://localhost:4200/m/abc-token'));
    });

    it('strips leading +', () => {
      const link = { ...linkWithPhone, phone: '+393401234567' };
      const url = component.waUrl(link);
      expect(url).toContain('wa.me/393401234567');
    });
  });

  describe('template rendering', () => {
    it('shows WhatsApp link for participant with phone', () => {
      component.links = [linkWithPhone];
      fixture.detectChanges();
      const el = fixture.nativeElement as HTMLElement;
      const waLink = el.querySelector('[data-testid="btn-whatsapp"]');
      expect(waLink).toBeTruthy();
      expect((waLink as HTMLAnchorElement).href).toContain('wa.me/393401234567');
    });

    it('does not show WhatsApp link for participant without phone (hasShare=false)', () => {
      // Force hasShare = false for this test
      Object.defineProperty(component, 'hasShare', { value: false });
      component.links = [linkNoPhone];
      fixture.detectChanges();
      const el = fixture.nativeElement as HTMLElement;
      const waLink = el.querySelector('[data-testid="btn-whatsapp"]');
      expect(waLink).toBeNull();
    });

    it('shows fallback input when no phone and no navigator.share', () => {
      Object.defineProperty(component, 'hasShare', { value: false });
      component.links = [linkNoPhone];
      fixture.detectChanges();
      const el = fixture.nativeElement as HTMLElement;
      const fallback = el.querySelector('[data-testid="share-fallback"]');
      expect(fallback).toBeTruthy();
      const input = fallback?.querySelector('input[readonly]');
      expect(input).toBeTruthy();
      expect((input as HTMLInputElement).value).toBe(linkNoPhone.tokenUrl);
    });

    it('shows share button when no phone but navigator.share available', () => {
      Object.defineProperty(component, 'hasShare', { value: true });
      component.links = [linkNoPhone];
      fixture.detectChanges();
      const el = fixture.nativeElement as HTMLElement;
      const shareBtn = el.querySelector('[data-testid="btn-share"]');
      expect(shareBtn).toBeTruthy();
    });
  });

  describe('shareLink()', () => {
    it('calls navigator.share with correct args', async () => {
      const shareSpy = jasmine.createSpy('share').and.returnValue(Promise.resolve());
      Object.defineProperty(navigator, 'share', { value: shareSpy, configurable: true });

      await component.shareLink(linkNoPhone);

      expect(shareSpy).toHaveBeenCalledWith(jasmine.objectContaining({
        url: linkNoPhone.tokenUrl,
      }));
    });

    it('does not throw when navigator.share rejects (user cancel)', async () => {
      const shareSpy = jasmine.createSpy('share').and.returnValue(Promise.reject(new DOMException('AbortError')));
      Object.defineProperty(navigator, 'share', { value: shareSpy, configurable: true });

      await expectAsync(component.shareLink(linkNoPhone)).toBeResolved();
    });
  });
});
