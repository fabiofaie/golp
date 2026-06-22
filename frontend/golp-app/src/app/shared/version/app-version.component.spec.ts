import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppVersionComponent } from './app-version.component';
import { APP_VERSION, APP_BUILD_HASH } from '../../version';

describe('AppVersionComponent', () => {
  let fixture: ComponentFixture<AppVersionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppVersionComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppVersionComponent);
    fixture.detectChanges();
  });

  it('renders APP_VERSION text in the footer', () => {
    const footer: HTMLElement = fixture.nativeElement.querySelector('footer');
    expect(footer.textContent?.trim()).toBe(APP_VERSION);
  });

  it('puts APP_BUILD_HASH in the title attribute, not in the visible text', () => {
    const footer: HTMLElement = fixture.nativeElement.querySelector('footer');
    expect(footer.getAttribute('title')).toBe(APP_BUILD_HASH);
    expect(footer.textContent).not.toContain(APP_BUILD_HASH);
  });
});
