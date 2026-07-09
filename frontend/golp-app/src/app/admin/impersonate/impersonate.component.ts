import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-impersonate',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule],
  templateUrl: './impersonate.component.html'
})
export class ImpersonateComponent {
  form: FormGroup;
  loading = false;
  error: string | null = null;

  constructor(private fb: FormBuilder, private auth: AuthService, private router: Router) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading = true;
    this.error = null;
    this.auth.startImpersonation(this.form.value.email).subscribe({
      next: () => { this.loading = false; void this.router.navigate(['/dashboard']); },
      error: (err) => {
        this.loading = false;
        this.error = err.status === 404 ? 'Nessun utente trovato con questa email.' : 'Impersonazione fallita.';
      }
    });
  }
}
