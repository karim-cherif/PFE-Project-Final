import { auth } from "./api";
import { AuthService } from "./auth-service";
import type { Router } from "vue-router";

interface GoogleOAuthResponse {
  access_token: string;
  error?: string;
  error_description?: string;
}

interface GoogleUserInfo {
  email: string;
  name: string;
  given_name?: string;
  family_name?: string;
  picture?: string;
}

// Type definitions for Google OAuth2 APIs
interface GoogleTokenClient {
  requestAccessToken(): void;
}

interface GoogleOAuth2 {
  initTokenClient(config: {
    client_id: string;
    scope: string;
    callback: (response: GoogleOAuthResponse) => void;
    error_callback: (error: any) => void;
  }): GoogleTokenClient;
}

interface GoogleAccounts {
  oauth2: GoogleOAuth2;
}

interface GoogleAPI {
  accounts: GoogleAccounts;
}

declare global {
  interface Window {
    google?: GoogleAPI;
  }
}

export class GoogleOAuthService {
  private static instance: GoogleOAuthService | null = null;
  private tokenClient: GoogleTokenClient | null = null;
  private isInitialized = false;
  private initializationPromise: Promise<void> | null = null;

  private constructor() {}

  static getInstance(): GoogleOAuthService {
    if (!GoogleOAuthService.instance) {
      GoogleOAuthService.instance = new GoogleOAuthService();
    }
    return GoogleOAuthService.instance;
  }

  // Utility function to wait for Google APIs to be ready
  private async waitForGoogleAPIs(
    maxAttempts = 10,
    delay = 500
  ): Promise<boolean> {
    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      if (window.google?.accounts?.oauth2) {
        console.log(`Google APIs ready after ${attempt} attempts`);
        return true;
      }
      console.log(
        `Waiting for Google APIs... attempt ${attempt}/${maxAttempts}`
      );
      await new Promise((resolve) => setTimeout(resolve, delay));
    }
    return false;
  }

  // Enhanced Google script loader with better error handling
  private async loadGoogleScript(): Promise<void> {
    return new Promise((resolve, reject) => {
      // Check if script is already loaded
      const existingScript = document.querySelector(
        'script[src*="accounts.google.com/gsi/client"]'
      );
      if (existingScript) {
        console.log("Google script already exists, waiting for APIs...");
        this.waitForGoogleAPIs().then((ready) => {
          if (ready) resolve();
          else reject(new Error("Google APIs not ready after script load"));
        });
        return;
      }

      console.log("Loading Google Sign-In script...");
      const script = document.createElement("script");
      script.src = "https://accounts.google.com/gsi/client";
      script.async = true;
      script.defer = true;

      script.onload = async () => {
        console.log("Google script loaded, waiting for APIs...");
        const ready = await this.waitForGoogleAPIs();
        if (ready) {
          resolve();
        } else {
          reject(new Error("Google APIs not ready after script load"));
        }
      };

      script.onerror = () => {
        reject(new Error("Failed to load Google Sign-In script"));
      };

      document.head.appendChild(script);
    });
  }

  async initialize(): Promise<void> {
    // Return existing promise if initialization is already in progress
    if (this.initializationPromise) {
      return this.initializationPromise;
    }

    // Return immediately if already initialized
    if (this.isInitialized && this.tokenClient) {
      return Promise.resolve();
    }

    this.initializationPromise = (async () => {
      try {
        console.log("Starting Google OAuth initialization...");

        // Load Google script and wait for APIs
        await this.loadGoogleScript();

        // Double-check that APIs are available
        if (!window.google?.accounts?.oauth2) {
          throw new Error("Google OAuth2 library not available after loading");
        }

        console.log("Google OAuth APIs are ready");
        this.isInitialized = true;
      } catch (err: any) {
        console.error("Google OAuth initialization error:", err);
        this.isInitialized = false;
        this.tokenClient = null;
        throw err;
      }
    })();

    return this.initializationPromise;
  }

  async signIn(
    router: Router,
    onError: (message: string) => void,
    onLoading: (loading: boolean) => void
  ): Promise<void> {
    try {
      console.log("Google Sign-In requested");

      // Ensure Google is initialized
      if (!this.isInitialized) {
        console.log("Google not initialized, initializing now...");
        await this.initialize();
      }

      // Create token client if not exists or if previous one failed
      if (!this.tokenClient) {
        console.log("Creating Google OAuth2 token client...");

        // Ensure Google APIs are available with proper null checks
        if (!window.google?.accounts?.oauth2) {
          throw new Error("Google OAuth2 APIs not available");
        }

        this.tokenClient = window.google.accounts.oauth2.initTokenClient({
          client_id: import.meta.env.VITE_GOOGLE_CLIENT_ID,
          scope: "openid email profile",
          callback: async (response: GoogleOAuthResponse) => {
            await this.handleOAuthCallback(
              response,
              router,
              onError,
              onLoading
            );
          },
          error_callback: (error: any) => {
            console.error("Google OAuth error callback:", error);
            onError(
              "Erreur lors de l'initialisation de l'authentification Google"
            );
            onLoading(false);
          },
        });

        if (!this.tokenClient) {
          throw new Error("Failed to create Google OAuth2 token client");
        }
      }

      // Double-check that APIs are still available
      if (!window.google?.accounts?.oauth2) {
        throw new Error("Google OAuth2 APIs not available");
      }

      console.log("Requesting Google access token...");
      this.tokenClient.requestAccessToken();
    } catch (err: any) {
      console.error("Error during Google Sign-In:", err);
      onError(err.message || "Erreur lors du déclenchement de Google Sign-In");

      // Reset state to allow retry
      this.reset();
    }
  }

  private async handleOAuthCallback(
    response: GoogleOAuthResponse,
    router: Router,
    onError: (message: string) => void,
    onLoading: (loading: boolean) => void
  ): Promise<void> {
    console.log("Google OAuth callback received:", {
      hasToken: !!response.access_token,
      error: response.error,
      errorDescription: response.error_description,
    });

    if (response.error) {
      console.error(
        "Google OAuth error:",
        response.error,
        response.error_description
      );
      onError(
        `Erreur Google OAuth: ${response.error_description || response.error}`
      );
      onLoading(false);
      return;
    }

    if (!response.access_token) {
      console.error("No access token received from Google");
      onError("Aucun token d'accès reçu de Google");
      onLoading(false);
      return;
    }

    onLoading(true);
    try {
      console.log("Processing Google authentication...");

      // Get user info using access token with timeout
      const userInfo = await this.fetchUserInfo(response.access_token);

      if (!userInfo.email) {
        throw new Error("No email received from Google user info");
      }

      const userData = {
        idToken: response.access_token,
        email: userInfo.email,
        firstName:
          userInfo.given_name || userInfo.name?.split(" ")[0] || "User",
        lastName:
          userInfo.family_name ||
          userInfo.name?.split(" ").slice(1).join(" ") ||
          "",
        picture: userInfo.picture,
      };

      console.log("Sending authentication request to backend...");
      const authResponse = await auth.googleAuth(userData);

      if (authResponse.data?.success && authResponse.data?.data?.token) {
        console.log("Authentication successful, setting up user session...");

        // Store authentication data
        AuthService.setToken(authResponse.data.data.token);
        if (authResponse.data.data.refreshToken) {
          localStorage.setItem(
            "refreshToken",
            authResponse.data.data.refreshToken
          );
        }

        await AuthService.setUser({
          ...authResponse.data.data.user,
          email: userData.email,
          firstName: userData.firstName,
          lastName: userData.lastName,
          picture: userData.picture,
        });

        console.log("Redirecting to dashboard...");
        await router.push("/dashboard");
      } else {
        console.error(
          "Backend authentication failed:",
          authResponse.data?.message
        );
        onError(
          authResponse.data?.message ||
            "Erreur lors de l'authentification avec le serveur"
        );
      }
    } catch (err: any) {
      console.error("Google authentication processing error:", err);
      if (err.name === "AbortError") {
        onError("Timeout lors de la récupération des informations utilisateur");
      } else {
        onError(
          err.message ||
            "Erreur lors du traitement de l'authentification Google"
        );
      }
    } finally {
      onLoading(false);
    }
  }

  private async fetchUserInfo(accessToken: string): Promise<GoogleUserInfo> {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 10000); // 10 second timeout

    try {
      const userInfoResponse = await fetch(
        `https://www.googleapis.com/oauth2/v2/userinfo?access_token=${accessToken}`,
        { signal: controller.signal }
      );
      clearTimeout(timeoutId);

      if (!userInfoResponse.ok) {
        throw new Error(
          `Failed to fetch user info: ${userInfoResponse.status} ${userInfoResponse.statusText}`
        );
      }

      const userInfo = await userInfoResponse.json();
      console.log("User info received:", {
        email: userInfo.email,
        name: userInfo.name,
      });

      return userInfo;
    } catch (error) {
      clearTimeout(timeoutId);
      throw error;
    }
  }

  reset(): void {
    console.log("Resetting Google OAuth service");
    this.isInitialized = false;
    this.tokenClient = null;
    this.initializationPromise = null;
  }

  isReady(): boolean {
    return (
      this.isInitialized &&
      !!this.tokenClient &&
      !!window.google?.accounts?.oauth2
    );
  }
}

export const googleOAuth = GoogleOAuthService.getInstance();
