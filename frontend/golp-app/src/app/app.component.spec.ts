import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AppComponent } from './app.component';
import { AppUpdateService } from './shared/update/app-update.service';

@Component({ selector: 'app-dummy', standalone: true, template: '' })
class DummyComponent {}

describe('AppComponent', () => {
  let updateServiceMock: { triggerCheck: jasmine.Spy };

  beforeEach(async () => {
    updateServiceMock = { triggerCheck: jasmine.createSpy('triggerCheck') };

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([{ path: '**', component: DummyComponent }]),
        { provide: AppUpdateService, useValue: updateServiceMock }
      ]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it(`should have the 'golp-app' title`, () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.title).toEqual('golp-app');
  });

  it('should render title', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Hello, golp-app');
  });

  it('chiama triggerCheck quando il documento torna visibile', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
    document.dispatchEvent(new Event('visibilitychange'));

    expect(updateServiceMock.triggerCheck).toHaveBeenCalled();
  });

  it('chiama triggerCheck alla fine di una navigazione', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/login');

    expect(updateServiceMock.triggerCheck).toHaveBeenCalled();
  });
});
