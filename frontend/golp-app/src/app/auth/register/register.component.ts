import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../auth.service';
import { CircleService } from '../../circles/circle.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule, RouterLink],
  templateUrl: './register.component.html'
})
export class RegisterComponent implements OnInit {
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
      name:     ['', [Validators.required, Validators.minLength(1)]],
      email:    ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]]
    });
  }

  ngOnInit(): void {
    this.inviteToken = this.route.snapshot.queryParamMap.get('inviteToken');
  }

  get loginQueryParams(): Record<string, string> {
    return this.inviteToken ? { inviteToken: this.inviteToken } : {};
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading = true;
    this.errorMessage = '';

    this.auth.register(this.form.value).subscribe({
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
        if (err.status === 409) {
          this.errorMessage = 'Email già registrata. Prova ad accedere.';
        } else {
          this.errorMessage = err.error?.error ?? 'Errore di registrazione. Riprova.';
        }
      }
    });
  }
}
