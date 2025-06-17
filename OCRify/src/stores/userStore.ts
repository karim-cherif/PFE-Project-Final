import { ref, computed, onUnmounted } from "vue";
import { defineStore } from "pinia";
import { user, type UserProfile, type UpdateUserDto } from "@/services/api";

export const useUserStore = defineStore("user", () => {
  const isLoading = ref(false);
  const userData = ref<UserProfile | null>(null);
  const error = ref<string | null>(null);
  const isInitialized = ref(false);
  const fetchPromise = ref<Promise<UserProfile | null> | null>(null);
  const storedAvatar = ref(localStorage.getItem("userAvatar"));

  // Set up storage event listener
  const handleStorageChange = (e: StorageEvent) => {
    if (e.key === "userAvatar") {
      storedAvatar.value = e.newValue;
    } else if (e.key === "user") {
      // Force refresh user data when user changes
      fetchUserProfile(true);
    }
  };

  // Add cleanup function
  const cleanup = () => {
    window.removeEventListener("storage", handleStorageChange);
    // Clear reactive state
    userData.value = null;
    error.value = null;
    isLoading.value = false;
    isInitialized.value = false;
    fetchPromise.value = null;
  };

  window.addEventListener("storage", handleStorageChange);

  // Clean up the event listener when the store is destroyed
  onUnmounted(() => {
    cleanup();
  });

  // Computed properties
  const userName = computed(() => {
    if (!userData.value) return "Utilisateur";
    return (
      `${userData.value.firstName || ""} ${
        userData.value.lastName || ""
      }`.trim() || "Utilisateur"
    );
  });

  const userEmail = computed(() => userData.value?.email || "");

  const userAvatar = computed(() => {
    // Priority order: backend profilePictureUrl > localStorage > generate default
    if (userData.value?.profilePictureUrl) {
      return userData.value.profilePictureUrl;
    }

    if (storedAvatar.value) {
      return storedAvatar.value;
    }

    // Generate a consistent default avatar based on user data
    if (userData.value?.email) {
      const seed = userData.value.email.toLowerCase();
      return `https://api.dicebear.com/9.x/croodles/svg?seed=${seed}`;
    }

    return "";
  });

  // Actions
  const fetchUserProfile = async (force = false) => {
    // If we're already initialized and have data, and not forcing refresh, return it
    if (!force && isInitialized.value && userData.value) {
      return userData.value;
    }

    // If we're already fetching, return the existing promise
    if (fetchPromise.value) {
      return fetchPromise.value;
    }

    // Start a new fetch
    fetchPromise.value = (async () => {
      try {
        isLoading.value = true;
        const response = await user.getProfile();

        if (!response?.data) {
          throw new Error("Invalid profile data received");
        }

        userData.value = response.data;
        isInitialized.value = true;
        error.value = null;

        return response.data;
      } catch (err: any) {
        error.value = err.message;
        return null;
      } finally {
        isLoading.value = false;
        fetchPromise.value = null;
      }
    })();

    return fetchPromise.value;
  };

  const updateProfile = async (data: UpdateUserDto) => {
    try {
      isLoading.value = true;
      const response = await user.updateProfile(data);
      if (response?.data) {
        userData.value = response.data;
        // Force refresh user data after update
        await fetchUserProfile(true);
      }
      return response;
    } catch (err) {
      throw err;
    } finally {
      isLoading.value = false;
    }
  };

  const setUserAvatar = (avatarUrl: string) => {
    if (avatarUrl) {
      localStorage.setItem("userAvatar", avatarUrl);
      storedAvatar.value = avatarUrl;
    } else {
      localStorage.removeItem("userAvatar");
      storedAvatar.value = null;
    }
  };

  const reset = () => {
    userData.value = null;
    error.value = null;
    isInitialized.value = false;
    fetchPromise.value = null;
    storedAvatar.value = null;
  };

  return {
    // State
    userData,
    isLoading,
    error,
    isInitialized,

    // Getters
    userName,
    userEmail,
    userAvatar,

    // Actions
    fetchUserProfile,
    updateProfile,
    setUserAvatar,
    reset,
  };
});
