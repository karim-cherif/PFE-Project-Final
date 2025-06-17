import { auth, user, type UserProfile } from "./api";
import router from "@/router";
import { jwtDecode } from "jwt-decode";

interface DecodedToken {
  exp: number;
  sub: string;
  email: string;
  role: string;
}

export class AuthService {
  private static TOKEN_KEY = "token";
  private static USER_KEY = "user";
  private static REFRESH_THRESHOLD = 5 * 60 * 1000; // 5 minutes in milliseconds

  static getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  static setToken(token: string): void {
    localStorage.setItem(this.TOKEN_KEY, token);
    this.scheduleTokenRefresh(token);
  }

  static removeToken(): void {
    localStorage.removeItem(this.TOKEN_KEY);
  }

  static async setUser(userData: any): Promise<void> {
    try {
      console.log("🔍 [AuthService.setUser] Input userData:", userData);

      // Get the full user profile
      const response = await user.getProfile();
      console.log("🔍 [AuthService.setUser] Profile response:", response);

      if (response.success && response.data) {
        console.log(
          "🔍 [AuthService.setUser] Storing profile data:",
          response.data
        );
        localStorage.setItem(this.USER_KEY, JSON.stringify(response.data));
      } else {
        console.error("Failed to get user profile:", response.message);
        console.log(
          "🔍 [AuthService.setUser] Storing fallback userData:",
          userData
        );
        localStorage.setItem(this.USER_KEY, JSON.stringify(userData));
      }
    } catch (error) {
      console.error("Error fetching user profile:", error);
      // If we can't get the profile, store what we have
      console.log(
        "🔍 [AuthService.setUser] Storing fallback userData due to error:",
        userData
      );
      localStorage.setItem(this.USER_KEY, JSON.stringify(userData));
    }
  }

  static getUser(): UserProfile | null {
    const user = localStorage.getItem(this.USER_KEY);
    console.log(
      "🔍 [AuthService.getUser] Raw user data from localStorage:",
      user
    );

    if (!user) {
      console.log(
        "🔍 [AuthService.getUser] No user data found in localStorage"
      );
      return null;
    }

    try {
      const parsed = JSON.parse(user);
      console.log("🔍 [AuthService.getUser] Parsed user data:", parsed);
      return parsed;
    } catch (e) {
      console.error("🔍 [AuthService.getUser] Error parsing user data:", e);
      return null;
    }
  }

  static removeUser(): void {
    localStorage.removeItem(this.USER_KEY);
  }

  static isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) return false;

    try {
      const decoded = jwtDecode<DecodedToken>(token);
      return decoded.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }

  private static scheduleTokenRefresh(token: string): void {
    try {
      const decoded = jwtDecode<DecodedToken>(token);
      const expirationTime = decoded.exp * 1000; // Convert to milliseconds
      const currentTime = Date.now();
      const timeUntilExpiration = expirationTime - currentTime;

      // Validate token expiry time
      if (timeUntilExpiration <= 0) {
        console.warn(
          "⚠️ [AuthService] Token already expired, not scheduling refresh"
        );
        return;
      }

      if (timeUntilExpiration > 86400000) {
        // More than 24 hours in milliseconds
        console.warn(
          "⚠️ [AuthService] Token expiry too far in future, not scheduling refresh"
        );
        return;
      }

      // If token is already expired or will expire in less than threshold, refresh immediately
      if (timeUntilExpiration <= this.REFRESH_THRESHOLD) {
        this.refreshToken();
        return;
      }

      // Schedule refresh before expiration
      const timeUntilRefresh = timeUntilExpiration - this.REFRESH_THRESHOLD;

      // Ensure we don't schedule negative or extremely long timeouts
      if (timeUntilRefresh > 0 && timeUntilRefresh < 86400000) {
        setTimeout(() => this.refreshToken(), timeUntilRefresh);
        console.log(
          `🔄 [AuthService] Token refresh scheduled in ${Math.round(
            timeUntilRefresh / 1000
          )} seconds`
        );
      }
    } catch (error) {
      console.error("❌ [AuthService] Error scheduling token refresh:", error);
      // Don't schedule refresh if token is malformed
    }
  }

  private static async refreshToken(): Promise<void> {
    try {
      const refreshToken = localStorage.getItem("refreshToken");
      if (!refreshToken) {
        throw new Error("No refresh token available");
      }

      const response = await auth.refresh(refreshToken);
      if (response.data.success && response.data.data) {
        this.setToken(response.data.data.token);
        if (response.data.data.refreshToken) {
          localStorage.setItem("refreshToken", response.data.data.refreshToken);
        }
      } else {
        throw new Error("Token refresh failed");
      }
    } catch (error) {
      console.error("Token refresh failed:", error);
      this.logout();
    }
  }

  static async logout(): Promise<void> {
    try {
      // Call the backend logout endpoint
      await auth.logout();
    } catch (error) {
      console.error("Logout error:", error);
      // Even if the backend call fails, we still want to clear local storage
    } finally {
      // Clear all auth-related data
      this.removeToken();
      this.removeUser();

      // Clear any other auth-related data that might be in localStorage
      localStorage.removeItem("refreshToken");
      localStorage.removeItem("authState");
      localStorage.removeItem("userAvatar");

      // Clear all localStorage items that might contain auth data
      this.clearAllAuthData();

      // Use router navigation instead of page reload
      router.push("/sign-in");
    }
  }

  static clearAllAuthData(): void {
    // Clear all possible auth-related keys
    const authKeys = [
      this.TOKEN_KEY,
      this.USER_KEY,
      "refreshToken",
      "authState",
      "userAvatar",
      "user",
      "token",
      "access_token",
      "jwt",
      "auth_token",
      "userProfile",
      "documents",
      "settings",
      "preferences",
      "lastActivity",
      "sessionData",
      "documentCache",
      "userCache",
      "adminCache",
      "statsCache",
    ];

    authKeys.forEach((key) => {
      localStorage.removeItem(key);
      sessionStorage.removeItem(key);
    });

    // Clear any keys that might start with common prefixes
    const prefixes = [
      "user_",
      "auth_",
      "token_",
      "session_",
      "cache_",
      "document_",
      "admin_",
      "stats_",
    ];
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (key && prefixes.some((prefix) => key.startsWith(prefix))) {
        localStorage.removeItem(key);
        i--; // Adjust index since we removed an item
      }
    }

    // Clear session storage completely
    sessionStorage.clear();

    console.log("🧹 [AuthService] All authentication and cache data cleared");
  }
}
