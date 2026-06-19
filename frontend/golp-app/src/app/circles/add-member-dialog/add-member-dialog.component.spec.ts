import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AddMemberDialogComponent } from './add-member-dialog.component';
import { CircleService, AddMemberResult } from '../circle.service';

const CIRCLE_ID = 'circle-1';
const CIRCLE_NAME = 'Test Circle';

describe('AddMemberDialogComponent', () => {
  let circleSvc: jasmine.SpyObj<CircleService>;

  beforeEach(async () => {
    circleSvc = jasmine.createSpyObj('CircleService', ['checkOrAddMember']);

    await TestBed.configureTestingModule({
      imports: [AddMemberDialogComponent],
      providers: [{ provide: CircleService, useValue: circleSvc }],
    }).compileComponents();
  });

  function createComponent() {
    const fixture = TestBed.createComponent(AddMemberDialogComponent);
    fixture.componentInstance.circleId = CIRCLE_ID;
    fixture.componentInstance.circleName = CIRCLE_NAME;
    fixture.detectChanges();
    return fixture;
  }

  it('submitEmail with existing email moves to confirmExisting step', () => {
    circleSvc.checkOrAddMember.and.returnValue(of({ exists: true, name: 'Mario' } as AddMemberResult));
    const fixture = createComponent();
    const comp = fixture.componentInstance;
    comp.email = 'mario@test.com';

    comp.submitEmail();

    expect(circleSvc.checkOrAddMember).toHaveBeenCalledWith(CIRCLE_ID, 'mario@test.com');
    expect(comp.step).toBe('confirmExisting');
    expect(comp.existingName).toBe('Mario');
  });

  it('submitEmail with unknown email moves to newPlayer step', () => {
    circleSvc.checkOrAddMember.and.returnValue(of({ exists: false } as AddMemberResult));
    const fixture = createComponent();
    const comp = fixture.componentInstance;
    comp.email = 'new@test.com';

    comp.submitEmail();

    expect(comp.step).toBe('newPlayer');
  });

  it('submitEmail with invalid email shows error from API', () => {
    circleSvc.checkOrAddMember.and.returnValue(
      throwError(() => ({ error: { error: 'Formato email non valido' } })),
    );
    const fixture = createComponent();
    const comp = fixture.componentInstance;
    comp.email = 'not-an-email';

    comp.submitEmail();

    expect(comp.error).toBe('Formato email non valido');
  });

  it('confirmExisting calls API with confirmed=true and shows success', () => {
    circleSvc.checkOrAddMember.and.returnValue(
      of({ exists: true, alreadyMember: false, name: 'Mario' } as AddMemberResult),
    );
    const fixture = createComponent();
    const comp = fixture.componentInstance;
    comp.email = 'mario@test.com';
    comp.existingName = 'Mario';

    comp.confirmExisting();

    expect(circleSvc.checkOrAddMember).toHaveBeenCalledWith(CIRCLE_ID, 'mario@test.com', undefined, true);
    expect(comp.step).toBe('success');
    expect(comp.successMessage).toContain('Mario');
  });

  it('confirmExisting when already member shows informative message', () => {
    circleSvc.checkOrAddMember.and.returnValue(
      of({ exists: true, alreadyMember: true, name: 'Mario' } as AddMemberResult),
    );
    const fixture = createComponent();
    const comp = fixture.componentInstance;
    comp.email = 'mario@test.com';
    comp.existingName = 'Mario';

    comp.confirmExisting();

    expect(comp.successMessage).toContain('già membro');
  });

  it('submitNewPlayer creates user with email and name, shows success', () => {
    circleSvc.checkOrAddMember.and.returnValue(of({ exists: false, created: true } as AddMemberResult));
    const fixture = createComponent();
    const comp = fixture.componentInstance;
    comp.email = 'new@test.com';
    comp.name = 'Nuovo Giocatore';

    comp.submitNewPlayer();

    expect(circleSvc.checkOrAddMember).toHaveBeenCalledWith(CIRCLE_ID, 'new@test.com', 'Nuovo Giocatore');
    expect(comp.step).toBe('success');
    expect(comp.successMessage).toContain('Nuovo Giocatore');
  });

  it('close emits closed event', () => {
    const fixture = createComponent();
    const comp = fixture.componentInstance;
    let emitted = false;
    comp.closed.subscribe(() => (emitted = true));

    comp.close();

    expect(emitted).toBeTrue();
  });
});
