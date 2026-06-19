import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { InviteDialogComponent } from './invite-dialog.component';
import { CircleService, InviteLinkResponse } from '../circle.service';

const CIRCLE_ID = 'circle-1';
const CIRCLE_NAME = 'Test Circle';
const TOKEN = 'abc123def456';

describe('InviteDialogComponent', () => {
  let circleSvc: jasmine.SpyObj<CircleService>;

  beforeEach(async () => {
    circleSvc = jasmine.createSpyObj('CircleService', ['getInviteLink']);
    circleSvc.getInviteLink.and.returnValue(of({ inviteToken: TOKEN } as InviteLinkResponse));

    await TestBed.configureTestingModule({
      imports: [InviteDialogComponent],
      providers: [{ provide: CircleService, useValue: circleSvc }],
    }).compileComponents();
  });

  function createComponent(canShare: boolean) {
    const fixture = TestBed.createComponent(InviteDialogComponent);
    fixture.componentInstance.circleId = CIRCLE_ID;
    fixture.componentInstance.circleName = CIRCLE_NAME;
    fixture.componentInstance.canShare = canShare;
    fixture.detectChanges();
    return fixture;
  }

  it('calls getInviteLink with correct circleId on init', () => {
    createComponent(false);
    expect(circleSvc.getInviteLink).toHaveBeenCalledWith(CIRCLE_ID);
  });

  it('builds inviteUrl containing the token', () => {
    const fixture = createComponent(false);
    const comp = fixture.componentInstance;
    expect(comp.inviteUrl).toContain(`/join?token=${TOKEN}`);
  });

  it('when canShare, auto-triggers navigator.share and closes the dialog', () => {
    const shareSpy = jasmine.createSpy('share').and.returnValue(Promise.resolve());
    (navigator as any).share = shareSpy;
    let emitted = false;

    const fixture = createComponent(true);
    fixture.componentInstance.closed.subscribe(() => (emitted = true));
    fixture.componentInstance.ngOnInit();

    expect(shareSpy).toHaveBeenCalledWith({
      title: `Unisciti a ${CIRCLE_NAME} su GOLP`,
      url: fixture.componentInstance.inviteUrl,
    });
    expect(emitted).toBeTrue();
  });

  it('copyLink calls navigator.clipboard.writeText with full invite URL', async () => {
    const fixture = createComponent(false);
    const comp = fixture.componentInstance;
    const clipboardSpy = spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());

    comp.copyLink();

    expect(clipboardSpy).toHaveBeenCalledWith(comp.inviteUrl);
  });

  it('sendEmail opens mailto containing the invite URL', () => {
    const fixture = createComponent(false);
    const comp = fixture.componentInstance;
    const openSpy = spyOn(window, 'open');

    comp.sendEmail();

    expect(openSpy).toHaveBeenCalledWith(
      jasmine.stringContaining(encodeURIComponent(comp.inviteUrl)),
      '_blank',
    );
  });

  it('close emits closed event', () => {
    const fixture = createComponent(false);
    const comp = fixture.componentInstance;
    let emitted = false;
    comp.closed.subscribe(() => (emitted = true));

    comp.close();

    expect(emitted).toBeTrue();
  });
});
