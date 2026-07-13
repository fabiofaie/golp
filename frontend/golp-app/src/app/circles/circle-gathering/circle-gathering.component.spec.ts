import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CircleGatheringComponent } from './circle-gathering.component';
import { CircleService, MemberSummary } from '../circle.service';
import { AttendanceService } from '../attendance.service';
import { MatchmakingService, MatchmakingPlanDto } from '../matchmaking.service';

const CIRCLE_ID = 'circle-1';

function makeMember(overrides: Partial<MemberSummary> = {}): MemberSummary {
  return { userId: 'user-1', name: 'Luca', rating: 1000, rank: 1, ...overrides };
}

function makePlan(overrides: Partial<MatchmakingPlanDto> = {}): MatchmakingPlanDto {
  return {
    rounds: [
      {
        index: 0,
        matches: [{ team1: ['user-1', 'user-2'], team2: ['user-3', 'user-4'] }],
        resting: [],
      },
    ],
    ...overrides,
  };
}

describe('CircleGatheringComponent', () => {
  let circleSvc: jasmine.SpyObj<CircleService>;
  let attendanceSvc: jasmine.SpyObj<AttendanceService>;
  let matchmakingSvc: jasmine.SpyObj<MatchmakingService>;

  const members = [
    makeMember({ userId: 'user-1', name: 'Luca' }),
    makeMember({ userId: 'user-2', name: 'Giulia' }),
    makeMember({ userId: 'user-3', name: 'Marco' }),
    makeMember({ userId: 'user-4', name: 'Sara' }),
  ];

  beforeEach(async () => {
    circleSvc = jasmine.createSpyObj('CircleService', ['getMembers']);
    attendanceSvc = jasmine.createSpyObj('AttendanceService', ['setAttendance']);
    matchmakingSvc = jasmine.createSpyObj('MatchmakingService', ['getSuggestion']);

    circleSvc.getMembers.and.returnValue(of(members));

    await TestBed.configureTestingModule({
      imports: [CircleGatheringComponent],
      providers: [
        provideRouter([]),
        { provide: CircleService, useValue: circleSvc },
        { provide: AttendanceService, useValue: attendanceSvc },
        { provide: MatchmakingService, useValue: matchmakingSvc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => CIRCLE_ID } } },
        },
      ],
    }).compileComponents();
  });

  it('should create and load members', () => {
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.members.length).toBe(4);
    expect(fixture.componentInstance.loading).toBeFalse();
  });

  it('canGeneratePlan is false with fewer than 4 present', () => {
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();
    fixture.componentInstance.members[0].present = true;
    expect(fixture.componentInstance.canGeneratePlan).toBeFalse();
  });

  it('canGeneratePlan is true with 4 or more present', () => {
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();
    fixture.componentInstance.members.forEach(m => (m.present = true));
    expect(fixture.componentInstance.canGeneratePlan).toBeTrue();
  });

  it('toggleMemberPresence calls AttendanceService and flips present on success', () => {
    attendanceSvc.setAttendance.and.returnValue(of({ userId: 'user-1', present: true }));
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();

    const member = fixture.componentInstance.members[0];
    expect(member.present).toBeFalse();
    fixture.componentInstance.toggleMemberPresence(member);

    expect(attendanceSvc.setAttendance).toHaveBeenCalledWith(CIRCLE_ID, true, 'user-1');
    expect(member.present).toBeTrue();
  });

  it('toggleMemberPresence shows an error and does not flip state on failure', () => {
    attendanceSvc.setAttendance.and.returnValue(throwError(() => new Error('fail')));
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();

    const member = fixture.componentInstance.members[0];
    fixture.componentInstance.toggleMemberPresence(member);

    expect(member.present).toBeFalse();
    expect(fixture.componentInstance.errorMessage).toBeTruthy();
  });

  it('generatePlan does nothing when fewer than 4 present', () => {
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();
    fixture.componentInstance.generatePlan();
    expect(matchmakingSvc.getSuggestion).not.toHaveBeenCalled();
  });

  it('generatePlan calls MatchmakingService and stores the plan when 4+ present', () => {
    matchmakingSvc.getSuggestion.and.returnValue(of(makePlan()));
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();
    fixture.componentInstance.members.forEach(m => (m.present = true));

    fixture.componentInstance.generatePlan();

    expect(matchmakingSvc.getSuggestion).toHaveBeenCalledWith(CIRCLE_ID, 1, 'Total', 4);
    expect(fixture.componentInstance.plan?.rounds.length).toBe(1);
  });

  it('toggleTargetMode resets targetValue to the mode default', () => {
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();

    fixture.componentInstance.toggleTargetMode('PerPlayer');
    expect(fixture.componentInstance.targetValue).toBe(2);

    fixture.componentInstance.toggleTargetMode('Total');
    expect(fixture.componentInstance.targetValue).toBe(4);
  });

  it('adjustCourts clamps between 1 and 8', () => {
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();

    fixture.componentInstance.adjustCourts(-5);
    expect(fixture.componentInstance.courts).toBe(1);

    for (let i = 0; i < 10; i++) fixture.componentInstance.adjustCourts(1);
    expect(fixture.componentInstance.courts).toBe(8);
  });

  it('onPlayerClick swaps two players within the same match', () => {
    matchmakingSvc.getSuggestion.and.returnValue(of(makePlan()));
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();
    fixture.componentInstance.members.forEach(m => (m.present = true));
    fixture.componentInstance.generatePlan();

    fixture.componentInstance.onPlayerClick(0, 1, 'user-1');
    fixture.componentInstance.onPlayerClick(0, 2, 'user-3');

    const match = fixture.componentInstance.plan!.rounds[0].matches[0];
    expect(match.team1).toContain('user-3');
    expect(match.team2).toContain('user-1');
  });

  it('registerMatch navigates to record-match with the four player ids as query params', () => {
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    const fixture = TestBed.createComponent(CircleGatheringComponent);
    fixture.detectChanges();

    fixture.componentInstance.registerMatch({ team1: ['a', 'b'], team2: ['c', 'd'] });

    expect(router.navigate).toHaveBeenCalledWith(
      ['/circles', CIRCLE_ID, 'match', 'new'],
      { queryParams: { team1p1: 'a', team1p2: 'b', team2p1: 'c', team2p2: 'd' } },
    );
  });
});
