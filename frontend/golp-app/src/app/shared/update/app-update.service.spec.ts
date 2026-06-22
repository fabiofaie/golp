import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { SwUpdate, VersionEvent } from '@angular/service-worker';
import { AppUpdateService } from './app-update.service';

describe('AppUpdateService', () => {
  let service: AppUpdateService;
  let versionUpdates$: Subject<VersionEvent>;
  let swUpdateMock: {
    isEnabled: boolean;
    versionUpdates: Subject<VersionEvent>;
    checkForUpdate: jasmine.Spy;
    activateUpdate: jasmine.Spy;
  };

  beforeEach(() => {
    versionUpdates$ = new Subject<VersionEvent>();
    swUpdateMock = {
      isEnabled: true,
      versionUpdates: versionUpdates$,
      checkForUpdate: jasmine.createSpy('checkForUpdate').and.resolveTo(false),
      activateUpdate: jasmine.createSpy('activateUpdate').and.resolveTo(true)
    };

    TestBed.configureTestingModule({
      providers: [{ provide: SwUpdate, useValue: swUpdateMock }]
    });
    service = TestBed.inject(AppUpdateService);
  });

  it('imposta updateAvailable a true quando arriva VERSION_READY', done => {
    service.updateAvailable.subscribe(value => {
      if (value) {
        expect(value).toBe(true);
        done();
      }
    });
    versionUpdates$.next({ type: 'VERSION_READY' } as VersionEvent);
  });

  it('triggerCheck non chiama checkForUpdate se isEnabled è false', () => {
    swUpdateMock.isEnabled = false;
    service.triggerCheck();
    expect(swUpdateMock.checkForUpdate).not.toHaveBeenCalled();
  });

  it('triggerCheck non chiama checkForUpdate due volte entro la finestra di debounce', () => {
    service.triggerCheck();
    service.triggerCheck();
    expect(swUpdateMock.checkForUpdate).toHaveBeenCalledTimes(1);
  });

  it('triggerCheck non propaga eccezioni se checkForUpdate rifiuta (offline)', async () => {
    swUpdateMock.checkForUpdate.and.rejectWith(new Error('offline'));
    expect(() => service.triggerCheck()).not.toThrow();
    await new Promise(resolve => setTimeout(resolve, 0));
  });

  it('activate chiama activateUpdate e poi la funzione di reload iniettata', async () => {
    const reloadSpy = jasmine.createSpy('reload');
    service.setReloadFn(reloadSpy);
    service.activate();
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(swUpdateMock.activateUpdate).toHaveBeenCalled();
    expect(reloadSpy).toHaveBeenCalled();
  });

  it('activate chiama comunque il reload se activateUpdate rifiuta', async () => {
    swUpdateMock.activateUpdate.and.rejectWith(new Error('fail'));
    const reloadSpy = jasmine.createSpy('reload');
    service.setReloadFn(reloadSpy);
    service.activate();
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(reloadSpy).toHaveBeenCalled();
  });
});
