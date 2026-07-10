import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { AppShellComponent } from './app-shell.component';

@Component({ standalone: true, template: '<p>stub</p>' })
class StubComponent {}

const ROUTES = [
  { path: '', component: AppShellComponent, children: [
    { path: 'dashboard', component: StubComponent },
    { path: 'my-matches', component: StubComponent },
    { path: 'circles', component: StubComponent },
    { path: 'circles/new', component: StubComponent },
    { path: 'profilo', component: StubComponent },
    { path: 'circles/:id/matches', component: StubComponent },
  ]},
];

describe('AppShellComponent', () => {
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideRouter(ROUTES), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    harness = await RouterTestingHarness.create();
  });

  it('mostra la bottom-nav su /dashboard', async () => {
    const shell = await harness.navigateByUrl('/dashboard', AppShellComponent);
    expect(shell.showBottomNav()).toBeTrue();
  });

  it('mostra la bottom-nav su /my-matches, /circles, /profilo', async () => {
    for (const url of ['/my-matches', '/circles', '/profilo']) {
      const shell = await harness.navigateByUrl(url, AppShellComponent);
      expect(shell.showBottomNav()).withContext(url).toBeTrue();
    }
  });

  it('nasconde la bottom-nav su una rotta secondaria (match esatto, no prefisso)', async () => {
    const shell = await harness.navigateByUrl('/circles/new', AppShellComponent);
    expect(shell.showBottomNav()).toBeFalse();
  });

  it('nasconde la bottom-nav su rotta con parametro (storico circolo)', async () => {
    const shell = await harness.navigateByUrl('/circles/abc123/matches', AppShellComponent);
    expect(shell.showBottomNav()).toBeFalse();
  });
});
