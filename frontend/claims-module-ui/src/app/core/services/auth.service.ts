import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';

export interface MockLoginResponse {
  token: string;
  userId: string;
  userName: string;
  role: string;
}

export interface CurrentUser {
  token: string;
  userId: string;
  userName: string;
  role: string;
}

const STORAGE_KEY = 'claims-module.currentUser';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly userSignal = signal<CurrentUser | null>(this.restore());

  readonly currentUser = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.userSignal() !== null);
  readonly role = computed(() => this.userSignal()?.role ?? null);
  readonly canApproveReserves = computed(() => {
    const role = this.role();
    return role === 'Supervisor' || role === 'Manager';
  });

  constructor(private readonly http: HttpClient) {}

  listRoles(): Observable<string[]> {
    return this.http.get<string[]>(`${API_BASE_URL}/auth/users`);
  }

  login(roleKey: string): Observable<MockLoginResponse> {
    return this.http.post<MockLoginResponse>(`${API_BASE_URL}/auth/mock-login`, { role: roleKey }).pipe(
      tap((response) => {
        const user: CurrentUser = {
          token: response.token,
          userId: response.userId,
          userName: response.userName,
          role: response.role
        };
        this.userSignal.set(user);
        localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
      })
    );
  }

  logout(): void {
    this.userSignal.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private restore(): CurrentUser | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as CurrentUser) : null;
    } catch {
      return null;
    }
  }
}
