import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { CircleService } from '../circle.service';

@Component({
  selector: 'app-join-circle',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './join-circle.component.html',
})
export class JoinCircleComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authSvc = inject(AuthService);
  private readonly circleSvc = inject(CircleService);

  token = '';
  loading = false;
  error = '';
  alreadyMember = false;
  circleName = '';

  get inviteTokenParam(): Record<string, string> {
    return this.token ? { inviteToken: this.token } : {};
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';

    if (!this.token) {
      this.error = 'Link non valido o scaduto';
      return;
    }

    if (this.authSvc.isAuthenticated()) {
      this.loading = true;
      this.circleSvc.joinByToken(this.token).subscribe({
        next: (res) => {
          if (res.alreadyMember) {
            this.alreadyMember = true;
            this.loading = false;
            setTimeout(() => this.router.navigate(['/circles']), 2000);
          } else {
            this.router.navigate(['/circles']);
          }
        },
        error: () => {
          this.loading = false;
          this.error = 'Link non valido o scaduto';
        },
      });
    }
  }
}
