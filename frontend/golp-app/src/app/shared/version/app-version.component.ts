import { Component } from '@angular/core';
import { APP_VERSION, APP_BUILD_HASH } from '../../version';

@Component({
  selector: 'app-version',
  standalone: true,
  template: `<footer class="app-version" [title]="buildHash">{{ version }}</footer>`
})
export class AppVersionComponent {
  version = APP_VERSION;
  buildHash = APP_BUILD_HASH;
}
