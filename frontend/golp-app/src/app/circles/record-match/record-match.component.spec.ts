import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { RecordMatchComponent } from './record-match.component';
import { CircleService, CircleSummary, SportConfig } from '../circle.service';
import { MatchService, MatchCreated } from '../match.service';

const CIRCLE_ID = 'circle-1';

function makeCircle(sport: string): CircleSummary {
  return {
    id: CIRCLE_ID,
    name: 'Test Circle',
    sport,
    sets: true,
    pointUnit: 'game',
    ownerId: 'owner',
    memberCount: 2,
    myRating: 1000,
    myRank: 1,
  };
}

function makeSport(key: string, allowsSingles: boolean): SportConfig {
  return { sport: key, displayName: key, pointUnit: 'game', sets: true, teamSize: 2, allowsSingles };
}

describe('RecordMatchComponent — US-048 singles', () => {
  let circleSvc: jasmine.SpyObj<CircleService>;
  let matchSvc: jasmine.SpyObj<MatchService>;

  beforeEach(async () => {
    circleSvc = jasmine.createSpyObj('CircleService', ['getMyCircles', 'getSports', 'getMembers']);
    matchSvc  = jasmine.createSpyObj('MatchService', ['createMatch']);

    circleSvc.getMembers.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [RecordMatchComponent],
      providers: [
        { provide: CircleService, useValue: circleSvc },
        { provide: MatchService,  useValue: matchSvc },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => CIRCLE_ID } } } },
      ],
    }).compileComponents();
  });

  // ─── AC1: no toggle se allowsSingles=false ────────────────────────────────

  it('allowsSingles=false for sport that does not support singles', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle('basket2v2')]));
    circleSvc.getSports.and.returnValue(of([makeSport('basket2v2', false)]));

    const fixture = TestBed.createComponent(RecordMatchComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.allowsSingles).toBeFalse();
  });

  it('toggle element is absent when allowsSingles=false', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle('basket2v2')]));
    circleSvc.getSports.and.returnValue(of([makeSport('basket2v2', false)]));

    const fixture = TestBed.createComponent(RecordMatchComponent);
    fixture.detectChanges();

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('.slot-toggle-btn') as NodeListOf<HTMLElement>)
      .filter(b => b.textContent?.trim() === 'Singolo' || b.textContent?.trim() === 'Doppio');
    expect(buttons.length).toBe(0);
  });

  // ─── AC2: toggle visibile se allowsSingles=true ───────────────────────────

  it('allowsSingles=true for sport that supports singles', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle('padel')]));
    circleSvc.getSports.and.returnValue(of([makeSport('padel', true)]));

    const fixture = TestBed.createComponent(RecordMatchComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.allowsSingles).toBeTrue();
  });

  it('toggle element is present when allowsSingles=true', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle('padel')]));
    circleSvc.getSports.and.returnValue(of([makeSport('padel', true)]));

    const fixture = TestBed.createComponent(RecordMatchComponent);
    fixture.detectChanges();

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('.slot-toggle-btn') as NodeListOf<HTMLElement>)
      .filter(b => b.textContent?.trim() === 'Singolo' || b.textContent?.trim() === 'Doppio');
    expect(buttons.length).toBe(2);
  });

  // ─── AC3: 1 slot per team in singolo ─────────────────────────────────────

  it('team1Slots=[0] and team2Slots=[2] after toggleFormat(true)', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle('padel')]));
    circleSvc.getSports.and.returnValue(of([makeSport('padel', true)]));

    const fixture = TestBed.createComponent(RecordMatchComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.toggleFormat(true);
    expect(comp.team1Slots).toEqual([0]);
    expect(comp.team2Slots).toEqual([2]);
  });

  it('team1Slots=[0,1] and team2Slots=[2,3] after toggleFormat(false)', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle('padel')]));
    circleSvc.getSports.and.returnValue(of([makeSport('padel', true)]));

    const fixture = TestBed.createComponent(RecordMatchComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.toggleFormat(true);
    comp.toggleFormat(false);
    expect(comp.team1Slots).toEqual([0, 1]);
    expect(comp.team2Slots).toEqual([2, 3]);
  });

  // ─── AC4: isSingles=true nel body POST ───────────────────────────────────

  it('createMatch is called with isSingles=true and team size 1 when in singles mode', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle('padel')]));
    circleSvc.getSports.and.returnValue(of([makeSport('padel', true)]));
    matchSvc.createMatch.and.returnValue(of({} as MatchCreated));

    const fixture = TestBed.createComponent(RecordMatchComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    comp.toggleFormat(true);
    // fill slot 0 and slot 2
    comp.updateSlotField(0, 'userId', 'player-a');
    comp.updateSlotField(2, 'userId', 'player-b');
    // add minimal set score
    comp.updateSet(0, 'team1', 6);
    comp.updateSet(0, 'team2', 4);

    comp.submit();

    expect(matchSvc.createMatch).toHaveBeenCalledOnceWith(
      CIRCLE_ID,
      jasmine.objectContaining({
        isSingles: true,
        team1: [jasmine.objectContaining({ userId: 'player-a' })],
        team2: [jasmine.objectContaining({ userId: 'player-b' })],
      })
    );
  });

  // ─── AC7: regressione doppio ──────────────────────────────────────────────

  it('createMatch is called with isSingles=false and team size 2 in doubles mode', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle('padel')]));
    circleSvc.getSports.and.returnValue(of([makeSport('padel', true)]));
    matchSvc.createMatch.and.returnValue(of({} as MatchCreated));

    const fixture = TestBed.createComponent(RecordMatchComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // default is doubles
    comp.updateSlotField(0, 'userId', 'p1');
    comp.updateSlotField(1, 'userId', 'p2');
    comp.updateSlotField(2, 'userId', 'p3');
    comp.updateSlotField(3, 'userId', 'p4');
    comp.updateSet(0, 'team1', 6);
    comp.updateSet(0, 'team2', 4);

    comp.submit();

    expect(matchSvc.createMatch).toHaveBeenCalledOnceWith(
      CIRCLE_ID,
      jasmine.objectContaining({
        isSingles: false,
        team1: jasmine.arrayContaining([jasmine.objectContaining({ userId: 'p1' })]),
        team2: jasmine.arrayContaining([jasmine.objectContaining({ userId: 'p3' })]),
      })
    );
    const body = matchSvc.createMatch.calls.mostRecent().args[1];
    expect(body.team1.length).toBe(2);
    expect(body.team2.length).toBe(2);
  });

  it('sport without singles: createMatch sends isSingles=false (regressione)', () => {
    circleSvc.getMyCircles.and.returnValue(of([makeCircle('basket2v2')]));
    circleSvc.getSports.and.returnValue(of([makeSport('basket2v2', false)]));
    matchSvc.createMatch.and.returnValue(of({} as MatchCreated));

    const fixture = TestBed.createComponent(RecordMatchComponent);
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // isSingles defaults to false, no toggle available
    comp.updateSlotField(0, 'userId', 'p1');
    comp.updateSlotField(1, 'userId', 'p2');
    comp.updateSlotField(2, 'userId', 'p3');
    comp.updateSlotField(3, 'userId', 'p4');
    comp.updateSet(0, 'team1', 10);
    comp.updateSet(0, 'team2', 8);

    comp.submit();

    expect(matchSvc.createMatch).toHaveBeenCalledOnceWith(
      CIRCLE_ID,
      jasmine.objectContaining({ isSingles: false })
    );
  });
});
