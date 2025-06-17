import { createRouter, createWebHistory } from "vue-router";
import { AuthService } from "@/services/auth-service";
import Home from "@/views/Home.vue";
import SignIn from "@/views/SignIn.vue";
import SignUp from "@/views/SignUp.vue";
import Dashboard from "@/views/Dashboard.vue";
import Documents from "@/views/Documents.vue";
import Scanner from "@/views/Scanner.vue";
import Settings from "@/views/Settings.vue";
import Help from "@/views/Help.vue";
import Trash from "@/views/Trash.vue";
import ForgotPassword from "@/views/ForgotPassword.vue";
import Historique from "@/views/Historique.vue";
import AdminDashboard from "@/views/AdminDashboard.vue";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/",
      name: "home",
      component: Home,
    },
    {
      path: "/sign-in",
      name: "signin",
      component: SignIn,
      meta: {
        requiresAuth: false,
      },
    },
    {
      path: "/sign-up",
      name: "signup",
      component: SignUp,
      meta: {
        requiresAuth: false,
      },
    },
    {
      path: "/dashboard",
      name: "dashboard",
      component: Dashboard,
      meta: {
        requiresAuth: true,
      },
    },
    {
      path: "/documents",
      name: "documents",
      component: Documents,
      meta: {
        requiresAuth: true,
      },
    },
    {
      path: "/scanner",
      name: "scanner",
      component: Scanner,
      meta: {
        requiresAuth: true,
      },
    },
    {
      path: "/parametres",
      name: "settings",
      component: Settings,
      meta: {
        requiresAuth: true,
      },
    },
    {
      path: "/aide",
      name: "help",
      component: Help,
      meta: {
        requiresAuth: true,
      },
    },
    {
      path: "/corbeille",
      name: "corbeille",
      component: Trash,
      meta: {
        requiresAuth: true,
      },
    },
    {
      path: "/historique",
      name: "historique",
      component: Historique,
      meta: {
        requiresAuth: true,
      },
    },
    {
      path: "/admin",
      name: "admin",
      component: AdminDashboard,
      meta: {
        requiresAuth: true,
        requiresAdmin: true,
      },
    },
    {
      path: "/forgot-password",
      name: "ForgotPassword",
      component: ForgotPassword,
      meta: {
        requiresAuth: false,
        layout: "auth",
      },
    },
  ],
});

// Global navigation guard
router.beforeEach(async (to, from, next) => {
  const isAuthenticated = AuthService.isAuthenticated();
  const user = AuthService.getUser();

  console.log("🔍 [Router Guard] Navigation to:", to.path);
  console.log("🔍 [Router Guard] Is authenticated:", isAuthenticated);
  console.log("🔍 [Router Guard] User:", user);
  console.log("🔍 [Router Guard] Route meta:", to.meta);

  // Special handling for home page - clear auth if coming from logout or forced clear
  if (to.path === "/" && to.query.clearAuth === "true") {
    console.log("🧹 [Router Guard] Clearing authentication data");
    AuthService.clearAllAuthData();
    next();
    return;
  }

  // Check if route requires authentication
  if (to.meta.requiresAuth && !isAuthenticated) {
    console.log(
      "🚫 [Router Guard] Authentication required, redirecting to sign-in"
    );
    next("/sign-in");
    return;
  }

  // Check if route requires admin role
  if (to.meta.requiresAdmin && (!user || user.role !== "Admin")) {
    console.log(
      "🚫 [Router Guard] Admin role required, user role:",
      user?.role
    );
    next("/dashboard");
    return;
  }

  // Redirect authenticated users away from auth pages
  if ((to.path === "/sign-in" || to.path === "/sign-up") && isAuthenticated) {
    console.log(
      "🔄 [Router Guard] Authenticated user accessing auth page, redirecting to dashboard"
    );
    next("/dashboard");
    return;
  }

  next();
});

export default router;
