import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../auth.service';
import { CircleService } from '../../circles/circle.service';
import { AppVersionComponent } from '../../shared/version/app-version.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule, RouterLink, AppVersionComponent],
  templateUrl: './login.component.html'
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly circleSvc = inject(CircleService);

  form: FormGroup;
  errorMessage = '';
  loading = false;
  inviteToken: string | null = null;

  constructor() {
    this.form = this.fb.group({
      email:    ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required]]
    });
  }

  ngOnInit(): void {
    this.inviteToken = this.route.snapshot.queryParamMap.get('inviteToken');
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading = true;
    this.errorMessage = '';

    this.auth.login(this.form.value).subscribe({
      next: () => {
        if (this.inviteToken) {
          this.circleSvc.joinByToken(this.inviteToken).subscribe({
            next: () => this.router.navigate(['/circles']),
            error: () => this.router.navigate(['/dashboard']),
          });
        } else {
          this.router.navigate(['/dashboard']);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        if (err.status === 401) {
          this.errorMessage = 'Credenziali non valide';
        } else {
          this.errorMessage = 'Errore di accesso. Riprova.';
        }
      }
    });
  }
}
