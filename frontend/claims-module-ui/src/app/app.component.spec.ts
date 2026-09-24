import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MatToolbarHarness } from '@angular/material/toolbar/testing';
import { AppComponent } from './app.component';
import { AuthService } from './core/services/auth.service';
import { authStub } from './testing/fixtures';

describe('AppComponent', () => {
  function setup(role: 'Handler' | null) {
    const auth = authStub(role);
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }]
    });
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    return { fixture, auth, loader: TestbedHarnessEnvironment.loader(fixture) };
  }

  it('hides the toolbar when signed out', async () => {
    const { loader } = setup(null);
    expect(await loader.getAllHarnesses(MatToolbarHarness)).toEqual([]);
  });

  it('shows the signed-in user and role in the toolbar', async () => {
    const { loader } = setup('Handler');
    const toolbar = await loader.getHarness(MatToolbarHarness);
    const text = (await toolbar.getRowsAsText()).join(' ');
    expect(text).toContain('Handler User');
    expect(text).toContain('Handler');
  });

  it('signs out and navigates to /login', async () => {
    const { loader, auth } = setup('Handler');
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    await (await loader.getHarness(MatButtonHarness.with({ selector: '[aria-label="Sign out"]' }))).click();

    expect(auth.logout).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});
