import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { AppUpdateBannerComponent } from './app-update-banner.component';
import { AppUpdateService } from './app-update.service';

describe('AppUpdateBannerComponent', () => {
  let fixture: ComponentFixture<AppUpdateBannerComponent>;
  let updateAvailable$: BehaviorSubject<boolean>;
  let activateSpy: jasmine.Spy;

  beforeEach(async () => {
    updateAvailable$ = new BehaviorSubject<boolean>(false);
    activateSpy = jasmine.createSpy('activate');

    await TestBed.configureTestingModule({
      imports: [AppUpdateBannerComponent],
      providers: [{
        provide: AppUpdateService,
        useValue: { updateAvailable: updateAvailable$.asObservable(), activate: activateSpy }
      }]
    }).compileComponents();

    fixture = TestBed.createComponent(AppUpdateBannerComponent);
  });

  it('non mostra il banner quando updateAvailable è false', () => {
    fixture.detectChanges();
    const banner = fixture.nativeElement.querySelector('.update-banner');
    expect(banner).toBeNull();
  });

  it('mostra il banner con un bottone quando updateAvailable è true', () => {
    updateAvailable$.next(true);
    fixture.detectChanges();
    const banner = fixture.nativeElement.querySelector('.update-banner');
    const button = fixture.nativeElement.querySelector('button');
    expect(banner).not.toBeNull();
    expect(button).not.toBeNull();
  });

  it('click sul bottone chiama activate()', () => {
    updateAvailable$.next(true);
    fixture.detectChanges();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    button.click();
    expect(activateSpy).toHaveBeenCalled();
  });
});
