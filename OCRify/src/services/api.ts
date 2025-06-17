import axios from "axios";
import type {
  InternalAxiosRequestConfig,
  AxiosResponse,
  AxiosError,
} from "axios";
import type { RegisterDto, LoginDto, AuthResponse } from "@/types/auth";
import type {
  BackendDocumentDto,
  DocumentSearchResultDto,
  DocumentSearchDto,
  DashboardResponse,
} from "@/types/documents";
import router from "@/router";
import { useToast } from "@/components/ui/toast/use-toast";

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "http://localhost:5000",
  headers: {
    "Content-Type": "application/json",
    Accept: "application/json",
  },
});

// Create a toast instance
const { toast } = useToast();

// Request interceptor
api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = localStorage.getItem("token");
  if (token && config.headers) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Track refresh state to prevent race conditions
let isRefreshing = false;
let refreshPromise: Promise<any> | null = null;

// Response interceptor
api.interceptors.response.use(
  (response: AxiosResponse) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config;

    // If the error is 401 and we haven't tried to refresh the token yet
    if (
      error.response?.status === 401 &&
      originalRequest &&
      !originalRequest.headers["X-Retry"]
    ) {
      // If we're already refreshing, wait for that to complete
      if (isRefreshing && refreshPromise) {
        try {
          await refreshPromise;
          // Retry with the new token
          const token = localStorage.getItem("token");
          if (token) {
            originalRequest.headers["Authorization"] = `Bearer ${token}`;
            originalRequest.headers["X-Retry"] = "true";
            return api(originalRequest);
          }
        } catch (refreshError) {
          // If refresh failed, fall through to logout logic
        }
      } else {
        // Start the refresh process
        isRefreshing = true;
        refreshPromise = (async () => {
          try {
            // Try to refresh the token
            const refreshToken = localStorage.getItem("refreshToken");
            if (refreshToken) {
              const response = await auth.refresh(refreshToken);
              if (response.data.success && response.data.data) {
                // Update the token
                localStorage.setItem("token", response.data.data.token);
                if (response.data.data.refreshToken) {
                  localStorage.setItem(
                    "refreshToken",
                    response.data.data.refreshToken
                  );
                }
                if (response.data.data.user) {
                  localStorage.setItem(
                    "user",
                    JSON.stringify(response.data.data.user)
                  );
                }

                // Retry the original request
                originalRequest.headers[
                  "Authorization"
                ] = `Bearer ${response.data.data.token}`;
                originalRequest.headers["X-Retry"] = "true";
                return api(originalRequest);
              }
            }
            throw new Error("Refresh failed");
          } catch (refreshError) {
            console.error("Token refresh failed:", refreshError);
            throw refreshError;
          } finally {
            isRefreshing = false;
            refreshPromise = null;
          }
        })();

        try {
          return await refreshPromise;
        } catch (refreshError) {
          // Fall through to logout logic
        }
      }
    }

    // If we get here, either the refresh failed or we've already tried to refresh
    if (error.response?.status === 401) {
      // Show toast notification
      toast({
        title: "Session expirée",
        description: "Votre session a expiré. Veuillez vous reconnecter.",
        variant: "destructive",
        duration: 5000,
      });

      // Clear auth data
      localStorage.removeItem("token");
      localStorage.removeItem("refreshToken");
      localStorage.removeItem("user");

      // Redirect to login after a short delay to allow the user to see the message
      setTimeout(() => {
        router.push("/sign-in");
      }, 1000);

      // Return a resolved promise to prevent other error handlers from showing messages
      return Promise.resolve();
    }

    // For other errors, show a generic error message
    if (
      error.response?.data &&
      typeof error.response.data === "object" &&
      "message" in error.response.data
    ) {
      toast({
        title: "Erreur",
        description: error.response.data.message as string,
        variant: "destructive",
        duration: 5000,
      });
    }

    return Promise.reject(error);
  }
);

interface ResetPasswordDto {
  email: string;
  token: string;
  newPassword: string;
}

interface ChangePasswordDto {
  currentPassword: string;
  newPassword: string;
}

interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
}

interface DocumentUploadResponse {
  success: boolean;
  message: string;
  data?: {
    id: string;
    name: string;
    path: string;
    size: number;
    type: string;
    uploadedAt: string;
  };
}

interface BulkUploadResponse {
  success: boolean;
  message: string;
  data?: {
    uploadedFiles: Array<{
      id: string;
      name: string;
      path: string;
      size: number;
      type: string;
      uploadedAt: string;
    }>;
    failedFiles?: Array<{
      name: string;
      error: string;
    }>;
  };
}

interface DocumentDto {
  id: string;
  name: string;
  path: string;
  size: number;
  type: string;
  status: string;
  language: string;
  topic?: string;
  description?: string;
  tags: string[];
  isFavorite: boolean;
  createdAt: string;
  updatedAt: string;
  accuracy?: number;
  thumbnail?: string;
  fileName: string;
  lastModified: string;
}

// Auth endpoints
export const auth = {
  register: (data: RegisterDto) =>
    api.post<AuthResponse>("/api/Auth/register", data),
  login: (data: LoginDto) => api.post<AuthResponse>("/api/Auth/login", data),
  googleAuth: (data: {
    idToken: string;
    email: string;
    firstName: string;
    lastName: string;
    picture?: string;
  }) => api.post<AuthResponse>("/api/Auth/google-auth", data),
  refresh: (refreshToken: string) =>
    api.post<AuthResponse>("/api/Auth/refresh", { refreshToken }),
  forgotPassword: (email: string) =>
    api.post("/api/Auth/forgot-password", { email }),
  resetPassword: (data: ResetPasswordDto) =>
    api.post("/api/Auth/reset-password", data),
  changePassword: (data: ChangePasswordDto) =>
    api.post("/api/Auth/change-password", data),
  logout: async () => {
    try {
      const response = await api.post("/api/Auth/logout");
      // Clear auth headers after successful logout
      delete api.defaults.headers.common["Authorization"];
      return response;
    } catch (error) {
      // Even if the request fails, clear the headers
      delete api.defaults.headers.common["Authorization"];
      throw error;
    }
  },
};

// User DTOs
interface UpdateUserDto {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  address?: string;
  company?: string;
  birthDate?: string;
  profilePictureUrl?: string;
}

export interface UserProfile {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  phone?: string;
  address?: string;
  company?: string;
  birthDate?: string;
  createdAt: string;
  updatedAt?: string;
  createdBy?: string;
  updatedBy?: string;
  deletedAt?: string;
  deletedBy?: string;
  isDeleted: boolean;
  avatar?: string;
  profilePictureUrl?: string;
}

// User service
const user = {
  getProfile: async (): Promise<ApiResponse<UserProfile>> => {
    const response = await api.get<ApiResponse<UserProfile>>("/api/Users/me");
    return response.data;
  },

  async updateProfile(data: UpdateUserDto) {
    try {
      console.log("🔵 [updateProfile] Input data:", data);

      // Only send fields that are present in the data object
      const requestData = {
        firstName: data.firstName,
        lastName: data.lastName,
        email: data.email,
        ...(data.phone && { phone: data.phone }),
        ...(data.address && { address: data.address }),
        ...(data.company && { company: data.company }),
        ...(data.birthDate && { birthDate: data.birthDate }),
        ...(data.profilePictureUrl && {
          profilePictureUrl: data.profilePictureUrl,
        }),
      };

      console.log("🔵 [updateProfile] Prepared request data:", requestData);
      console.log(
        "🔵 [updateProfile] Request URL:",
        `${api.defaults.baseURL}/api/Users/me`
      );
      console.log("🔵 [updateProfile] Request headers:", api.defaults.headers);

      const response = await api.put<ApiResponse<UserProfile>>(
        "/api/Users/me",
        requestData
      );
      console.log("✅ [updateProfile] Success response:", response.data);
      return response.data;
    } catch (error: any) {
      console.error("❌ [updateProfile] Error:", error);
      console.error("❌ [updateProfile] Error response:", error.response?.data);
      throw error;
    }
  },
};

// User endpoints
export const users = {
  getProfile: () => api.get("/api/Users/me"),
  updateProfile: (data: UpdateUserDto) => user.updateProfile(data),
  updatePassword: (data: { current_password: string; password: string }) =>
    api.put("/api/Auth/change-password", data),
  getActivity: (params: { page: number; pageSize: number }) =>
    api.get("/api/Users/activity", { params }),
};

// Admin endpoints
export const admin = {
  // User Management
  getAllUsers: (params: {
    page: number;
    pageSize: number;
    includeDeleted?: boolean;
  }) => api.get("/api/Users", { params }),
  getUserById: (id: string) => api.get(`/api/Users/${id}`),
  updateUser: (id: string, data: AdminUpdateUserDto) =>
    api.put(`/api/Users/${id}`, data),
  deleteUser: (id: string) => api.delete(`/api/Users/${id}`),
  restoreUser: (id: string) => api.post(`/api/Users/${id}/restore`),
  bulkDeleteUsers: (userIds: string[]) =>
    api.post("/api/Users/bulk-delete", userIds),
  bulkRestoreUsers: (userIds: string[]) =>
    api.post("/api/Users/bulk-restore", userIds),
  permanentlyDeleteUser: (id: string) =>
    api.delete(`/api/Users/${id}/permanent`),
  bulkPermanentlyDeleteUsers: (userIds: string[]) =>
    api.post("/api/Users/bulk-permanent-delete", userIds),

  // System Statistics (Admin)
  getDashboardStats: () => api.get("/api/Dashboard/admin/stats"),
  getSystemStats: () => api.get("/api/Dashboard/document-type-distribution"),
  getOcrStatistics: () => api.get("/api/Dashboard/admin/ocr-stats"),

  // Document Management (Admin view of all documents)
  getAllDocuments: (params?: DocumentSearchDto) =>
    api.get("/api/Documents/admin/all", { params }),

  // Activity Monitoring
  getUserActivity: (
    userId: string,
    params: { page: number; pageSize: number }
  ) => api.get(`/api/Users/${userId}/activity`, { params }),
  getSystemActivity: (params?: { page?: number; pageSize?: number }) =>
    api.get("/api/Users/system-activity", { params }),
};

// Admin DTOs
export interface AdminUpdateUserDto {
  firstName: string;
  lastName: string;
  email: string;
  role: string;
}

// Document endpoints
export const documents = {
  getAll: async (params?: DocumentSearchDto) => {
    try {
      // Ensure params is an object
      const searchParams = params || {};

      const response = await api.get("/api/Documents", {
        params: searchParams,
      });
      return response.data;
    } catch (error: any) {
      throw error;
    }
  },
  get: (id: number) => api.get(`/documents/${id}`),
  updateFavorite: async (id: string, isFavorite: boolean) => {
    try {
      console.log("🔵 [documents.updateFavorite] Updating favorite status:", {
        id,
        isFavorite,
      });

      // First get the current document
      const currentDoc = await documents.getById(id);
      if (!currentDoc.data?.success || !currentDoc.data?.data) {
        throw new Error("Failed to fetch current document");
      }

      const doc = currentDoc.data.data;

      // Convert language string to enum value
      const languageMap: { [key: string]: number } = {
        English: 0,
        French: 1,
        Arabic: 2,
      };
      const language = languageMap[doc.language] || 0; // Default to English if unknown

      // Update with existing values plus the new favorite status
      const response = await api.put<ApiResponse<DocumentDto>>(
        `/api/Documents/${id}`,
        {
          fileName: doc.fileName,
          language: language,
          topic: doc.topic || "",
          description: doc.description || "",
          tags: doc.tags || [],
          isFavorite: isFavorite,
          lastModified: new Date().toISOString(),
        }
      );

      console.log(
        "✅ [documents.updateFavorite] Favorite status updated successfully:",
        response.data
      );
      return response.data;
    } catch (error) {
      console.error(
        "❌ [documents.updateFavorite] Error updating favorite status:",
        error
      );
      throw error;
    }
  },
  create: (data: FormData) => api.post("/documents", data),
  delete: async (id: string) => {
    try {
      console.log("🔵 [documents.delete] Deleting document:", id);
      const response = await api.delete<ApiResponse<void>>(
        `/api/Documents/${id}`
      );
      console.log(
        "✅ [documents.delete] Document deleted successfully:",
        response.data
      );
      return response.data;
    } catch (error) {
      console.error("❌ [documents.delete] Error deleting document:", error);
      throw error;
    }
  },
  restore: async (id: string) => {
    try {
      console.log("🔵 [documents.restore] Restoring document:", id);
      const response = await api.post<ApiResponse<void>>(
        `/api/Documents/${id}/restore`
      );
      console.log(
        "✅ [documents.restore] Document restored successfully:",
        response.data
      );
      return response.data;
    } catch (error) {
      console.error("❌ [documents.restore] Error restoring document:", error);
      throw error;
    }
  },
  permanentDelete: async (documentId: string) => {
    try {
      console.log(
        "🔵 [documents.permanentDelete] Permanently deleting document:",
        documentId
      );
      const response = await api.delete(
        `/api/Documents/${documentId}/permanent`
      );
      console.log(
        "✅ [documents.permanentDelete] Document permanently deleted successfully:",
        response.data
      );
      return response.data;
    } catch (error) {
      console.error(
        "❌ [documents.permanentDelete] Error permanently deleting document:",
        error
      );
      throw error;
    }
  },
  bulkDelete: async (ids: string[]) => {
    try {
      console.log("🔵 [documents.bulkDelete] Bulk deleting documents:", ids);
      const response = await api.post<ApiResponse<void>>(
        "/api/Documents/bulk-delete",
        ids
      );
      console.log(
        "✅ [documents.bulkDelete] Documents bulk deleted successfully:",
        response.data
      );
      return response.data;
    } catch (error) {
      console.error(
        "❌ [documents.bulkDelete] Error bulk deleting documents:",
        error
      );
      throw error;
    }
  },
  bulkRestore: async (ids: string[]) => {
    try {
      console.log("🔵 [documents.bulkRestore] Bulk restoring documents:", ids);
      const response = await api.post<ApiResponse<void>>(
        "/api/Documents/bulk-restore",
        ids
      );
      console.log(
        "✅ [documents.bulkRestore] Documents bulk restored successfully:",
        response.data
      );
      return response.data;
    } catch (error) {
      console.error(
        "❌ [documents.bulkRestore] Error bulk restoring documents:",
        error
      );
      throw error;
    }
  },
  bulkPermanentDelete: async (ids: string[]) => {
    try {
      const response = await api.post(
        "/api/Documents/bulk-permanent-delete",
        ids,
        {
          headers: {
            "Content-Type": "application/json",
          },
        }
      );

      console.log(
        "✅ [documents.bulkPermanentDelete] Response:",
        response.data
      );
      return {
        data: {
          success: true,
          message: "Documents supprimés définitivement avec succès",
        },
      };
    } catch (error: any) {
      console.error("❌ [documents.bulkPermanentDelete] Error:", error);
      console.error(
        "❌ [documents.bulkPermanentDelete] Request config:",
        error.config
      );
      console.error(
        "❌ [documents.bulkPermanentDelete] Request data:",
        error.config?.data
      );
      console.error(
        "❌ [documents.bulkPermanentDelete] Response data:",
        error.response?.data
      );
      throw error;
    }
  },
  scan: (id: number) => api.post(`/documents/${id}/scan`),
  upload: async (file: File, metadata?: { [key: string]: any }) => {
    try {
      console.log("🔵 [documents.upload] Preparing to upload file:", file.name);
      console.log("🔵 [documents.upload] Metadata:", metadata);

      const formData = new FormData();
      formData.append("file", file);

      // Add metadata fields individually
      if (metadata) {
        // Handle language properly - don't default to French if language is 0
        const language =
          metadata.language !== undefined ? metadata.language : "1";
        formData.append("language", String(language));
        formData.append("topic", metadata.topic || "");
        formData.append("description", metadata.description || "");
        // Properly serialize the tags array
        if (metadata.tags && Array.isArray(metadata.tags)) {
          metadata.tags.forEach((tag, index) => {
            formData.append(`tags[${index}]`, tag);
          });
        }
        formData.append("isFavorite", "false");
      }

      console.log("🔵 [documents.upload] FormData contents:");
      for (const [key, value] of formData.entries()) {
        console.log(`🔵 [documents.upload] ${key}:`, value);
      }

      const response = await api.post<DocumentUploadResponse>(
        "/api/Documents/upload",
        formData,
        {
          headers: {
            "Content-Type": "multipart/form-data",
          },
        }
      );

      console.log("✅ [documents.upload] Upload successful:", response.data);
      return response;
    } catch (error: any) {
      console.error(
        "❌ [documents.upload] Upload failed:",
        error.response?.data || error.message
      );
      throw error;
    }
  },
  bulkUpload: async (
    files: File[],
    metadata: Array<{
      language: string;
      topic: string;
      description: string;
      tags: string[];
      isFavorite: boolean;
    }>
  ) => {
    try {
      console.log("🔵 [documents.bulkUpload] Starting bulk upload process");
      console.log("📦 [documents.bulkUpload] Files to upload:", {
        count: files.length,
        files: files.map((f) => ({
          name: f.name,
          type: f.type,
          size: f.size,
        })),
      });
      console.log("📦 [documents.bulkUpload] Metadata:", metadata);

      const formData = new FormData();

      // Add files and metadata to FormData
      files.forEach((file, index) => {
        console.log(
          `📦 [documents.bulkUpload] Adding file ${index + 1} to FormData:`,
          {
            name: file.name,
            type: file.type,
            size: file.size,
          }
        );

        // Add file
        formData.append(`uploadDtos[${index}].File`, file);

        // Add metadata
        const meta = metadata[index];
        formData.append(`uploadDtos[${index}].Language`, meta.language);
        formData.append(`uploadDtos[${index}].Topic`, meta.topic || "");
        formData.append(
          `uploadDtos[${index}].Description`,
          meta.description || ""
        );

        // Add each tag individually
        if (meta.tags && meta.tags.length > 0) {
          meta.tags.forEach((tag, tagIndex) => {
            formData.append(`uploadDtos[${index}].Tags[${tagIndex}]`, tag);
          });
        }

        formData.append(
          `uploadDtos[${index}].IsFavorite`,
          String(meta.isFavorite)
        );
      });

      // Log the complete FormData
      console.log("📦 [documents.bulkUpload] Complete FormData contents:");
      for (const [key, value] of formData.entries()) {
        console.log(`  ${key}:`, value);
      }

      // Log the request configuration
      const config = {
        headers: {
          "Content-Type": "multipart/form-data",
          Authorization: `Bearer ${localStorage.getItem("token")}`,
        },
      };
      console.log("🔵 [documents.bulkUpload] Request configuration:", config);

      const endpoint = `${
        import.meta.env.VITE_API_URL
      }/api/Documents/bulk-upload`;
      console.log("🔵 [documents.bulkUpload] Sending request to:", endpoint);

      // Make the request
      console.log("🔵 [documents.bulkUpload] Sending request...");
      const response = await axios.post(endpoint, formData, config);

      console.log("✅ [documents.bulkUpload] Response received:", {
        status: response.status,
        statusText: response.statusText,
        data: response.data,
      });

      return response;
    } catch (error) {
      console.error("❌ [documents.bulkUpload] Error:", error);
      if (axios.isAxiosError(error)) {
        console.error("❌ [documents.bulkUpload] Error details:", {
          status: error.response?.status,
          statusText: error.response?.statusText,
          data: error.response?.data,
          headers: error.response?.headers,
        });
      }
      throw error;
    }
  },
  getRecent: async () => {
    try {
      console.log("🔵 [documents.getRecent] Fetching dashboard data");
      const response = await api.get<DashboardResponse>(
        "/api/Documents/dashboard"
      );
      console.log(
        "✅ [documents.getRecent] Dashboard data fetched successfully:",
        response.data
      );
      return response;
    } catch (error: any) {
      console.error(
        "❌ [documents.getRecent] Failed to fetch dashboard data:",
        error.response?.data || error.message
      );
      throw error;
    }
  },
  getById: async (id: string) => {
    try {
      console.log("🔵 [documents.getById] Fetching document:", id);
      const response = await api.get(`/api/Documents/${id}`);
      console.log(
        "✅ [documents.getById] Document fetched successfully:",
        response.data
      );
      return response;
    } catch (error: any) {
      console.error(
        "❌ [documents.getById] Failed to fetch document:",
        error.response?.data || error.message
      );
      throw error;
    }
  },
  update: async (id: string, data: any) => {
    try {
      console.log("🔵 [documents.update] Updating document:", id);
      console.log(
        "📦 [documents.update] Request data:",
        JSON.stringify(data, null, 2)
      );
      console.log(
        "🔍 [documents.update] RecoveryProcess in request:",
        data.recoveryProcess
      );
      console.log(
        "🔍 [documents.update] PageCount in request:",
        data.pageCount
      );

      // Ensure pageCount is not null
      if (data.pageCount === null || data.pageCount === undefined) {
        data.pageCount = 1;
      }

      const response = await api.put(`/api/Documents/${id}`, data);
      console.log(
        "✅ [documents.update] Document updated successfully:",
        response.data
      );
      console.log(
        "📦 [documents.update] Response data:",
        JSON.stringify(response.data, null, 2)
      );
      return response;
    } catch (error: any) {
      console.error(
        "❌ [documents.update] Failed to update document:",
        error.response?.data || error.message
      );
      console.error(
        "❌ [documents.update] Request data that failed:",
        JSON.stringify(data, null, 2)
      );
      throw error;
    }
  },
  getDocumentContent: async (id: string) => {
    return await api.get(`/api/Documents/${id}/content`, {
      responseType: "blob",
    });
  },
  getFile: async (docId: string) => {
    try {
      const response = await api.get(`/api/Documents/${docId}/download`, {
        responseType: "blob",
        headers: {
          Accept: "*/*",
          "Cache-Control": "no-cache",
        },
      });

      return response;
    } catch (error: any) {
      throw error;
    }
  },
  processOcr: async (id: string) => {
    try {
      console.log("🔵 [documents.processOcr] Processing OCR for document:", id);
      // Return sample data instead of making API call
      return {
        success: true,
        message: "OCR processing completed successfully",
        data: {
          text: "This is a sample OCR text result.\nIt contains multiple lines.\nAnd some formatting.",
          confidence: 0.95,
          language: "English",
          processingTime: 2.5,
          suggestions: [],
        },
      };
    } catch (error: any) {
      console.error("❌ [documents.processOcr] Error processing OCR:", error);
      throw error;
    }
  },
  saveOcrText: async (id: string, text: string) => {
    const response = await api.put(`/documents/${id}`, {
      ocrText: text,
    });
    return response.data;
  },
  performOcr: async (id: string) => {
    try {
      console.log("🔵 [documents.performOcr] Processing OCR for document:", id);
      const response = await api.post(`/api/Documents/${id}/ocr`);
      console.log(
        "✅ [documents.performOcr] OCR processing completed:",
        response.data
      );
      return response;
    } catch (error: any) {
      console.error("❌ [documents.performOcr] Error processing OCR:", error);
      throw error;
    }
  },
  getDeletionStats: () => {
    return axios.get<DashboardResponse>(
      `${import.meta.env.VITE_API_URL}/documents/stats/deletions`
    );
  },
  trackDeletion: async (documentId: string, isPermanent: boolean) => {
    try {
      console.log(
        "🔵 [documents.trackDeletion] Tracking deletion for document:",
        documentId
      );
      const response = await api.post("/api/Documents/track-deletion", {
        documentId,
        isPermanent,
      });
      console.log(
        "✅ [documents.trackDeletion] Deletion tracked successfully:",
        response.data
      );
      return response.data;
    } catch (error) {
      console.error(
        "❌ [documents.trackDeletion] Error tracking deletion:",
        error
      );
      throw error;
    }
  },
  // Document sharing endpoints
  shareDocument: async (id: string, data: { email: string; type: string }) => {
    try {
      console.log("🔵 [documents.shareDocument] Sharing document:", id, data);
      const response = await api.post(`/api/Documents/${id}/share`, data);
      console.log(
        "✅ [documents.shareDocument] Document shared successfully:",
        response.data
      );
      return response.data;
    } catch (error: any) {
      console.error(
        "❌ [documents.shareDocument] Error sharing document:",
        error
      );
      throw error;
    }
  },
  getDocumentShares: async (id: string) => {
    try {
      console.log(
        "🔵 [documents.getDocumentShares] Getting shares for document:",
        id
      );
      const response = await api.get(`/api/Documents/${id}/shares`);
      console.log(
        "✅ [documents.getDocumentShares] Shares retrieved successfully:",
        response.data
      );
      return response.data;
    } catch (error: any) {
      console.error(
        "❌ [documents.getDocumentShares] Error getting shares:",
        error
      );
      throw error;
    }
  },
  revokeDocumentShare: async (shareId: string) => {
    try {
      console.log(
        "🔵 [documents.revokeDocumentShare] Revoking share:",
        shareId
      );
      const response = await api.delete(`/api/Documents/shares/${shareId}`);
      console.log(
        "✅ [documents.revokeDocumentShare] Share revoked successfully:",
        response.data
      );
      return response.data;
    } catch (error: any) {
      console.error(
        "❌ [documents.revokeDocumentShare] Error revoking share:",
        error
      );
      throw error;
    }
  },
  getSharedWithMe: async (params: { page: number; pageSize: number }) => {
    try {
      console.log(
        "🔵 [documents.getSharedWithMe] Getting shared documents:",
        params
      );
      const response = await api.get("/api/Documents/shared-with-me", {
        params,
      });
      console.log(
        "✅ [documents.getSharedWithMe] Shared documents retrieved successfully:",
        response.data
      );
      return response.data;
    } catch (error: any) {
      console.error(
        "❌ [documents.getSharedWithMe] Error getting shared documents:",
        error
      );
      throw error;
    }
  },
};

// OCR endpoints
export const ocr = {
  process: (documentId: string) => {
    return api.post(`/api/Documents/${documentId}/ocr`);
  },
  processFile: async (
    file: File,
    language: string = "eng",
    page: number | null = null,
    suggestionModel: string = "LLAMA",
    ocrEngine: string = "MISTRAL",
    documentId: string
  ) => {
    try {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("language", language);

      if (page !== null) {
        formData.append("page", String(page));
      }

      formData.append("suggestion_model", suggestionModel.toLowerCase());
      formData.append("ocr_engine", ocrEngine.toLowerCase());
      formData.append("document_id", documentId);

      console.log("🔵 [ocr.processFile] Sending OCR request to OCR service");
      console.log(
        "🔵 [ocr.processFile] File:",
        file.name,
        file.type,
        file.size
      );
      console.log("🔵 [ocr.processFile] Language:", language);
      console.log("🔵 [ocr.processFile] Model:", suggestionModel);
      console.log("🔵 [ocr.processFile] Engine:", ocrEngine);

      // Use the configured base URL from environment variables
      const ocrServiceUrl =
        import.meta.env.VITE_OCR_SERVICE_URL || "http://localhost:5002";

      const response = await axios.post(
        `${ocrServiceUrl}/extract-text/`,
        formData,
        {
          headers: {
            "Content-Type": "multipart/form-data",
          },
        }
      );

      // Validate response structure
      if (!response.data || typeof response.data !== "object") {
        throw new Error("Invalid OCR response format");
      }

      // Ensure required fields are present with more lenient validation
      if (typeof response.data.text === "undefined") {
        console.warn("Missing text field in OCR response, using empty string");
        response.data.text = "";
      }

      if (!Array.isArray(response.data.suggestions)) {
        console.warn(
          "Missing or invalid suggestions field in OCR response, using empty array"
        );
        response.data.suggestions = [];
      }

      // Ensure confidence is a number
      if (typeof response.data.confidence !== "number") {
        console.warn(
          "Missing or invalid confidence field in OCR response, using 0"
        );
        response.data.confidence = 0;
      }

      // Ensure processingTime is a number
      if (typeof response.data.processingTime !== "number") {
        console.warn(
          "Missing or invalid processingTime field in OCR response, using 0"
        );
        response.data.processingTime = 0;
      }

      // Make callback to backend
      try {
        console.log("🔵 [ocr.processFile] Making callback to backend");
        const callbackResponse = await api.post("/api/Documents/ocr-callback", {
          documentId: documentId,
          text: response.data.text,
          suggestions: response.data.suggestions,
          corrections: response.data.corrections || [],
          confidence: response.data.confidence,
          processingTime: response.data.processingTime,
        });
        console.log(
          "✅ [ocr.processFile] Callback successful:",
          callbackResponse.data
        );
      } catch (error) {
        console.error("❌ [ocr.processFile] Callback failed:", error);
        // Don't throw here, we still want to return the OCR response
      }

      return response;
    } catch (error: any) {
      console.error("❌ [ocr.processFile] Error processing file:", error);
      throw error;
    }
  },
  getLanguages: () => api.get("/ocr/languages"),
  summarize: async (
    text: string,
    language: string,
    model: string = "llama"
  ) => {
    try {
      const ocrServiceUrl =
        import.meta.env.VITE_OCR_SERVICE_URL || "http://localhost:5002";

      // 🚀 ENHANCED: Better logging and validation
      console.log("🚀 [ocr.summarize] Starting summary request");
      console.log(
        `📏 [ocr.summarize] Text length: ${text.length} chars, ${
          text.split(/\s+/).length
        } words`
      );
      console.log(`🌍 [ocr.summarize] Language: ${language}, Model: ${model}`);
      console.log(`🔗 [ocr.summarize] Service URL: ${ocrServiceUrl}`);

      // Validate inputs
      if (!text || text.trim().length === 0) {
        throw new Error("Text cannot be empty");
      }

      if (!language) {
        throw new Error("Language must be specified");
      }

      const requestData = {
        text: text.trim(),
        language,
        model,
      };

      console.log("📡 [ocr.summarize] Sending request:", requestData);

      const response = await axios.post(
        `${ocrServiceUrl}/summarize`,
        requestData,
        {
          timeout: 60000, // 60 second timeout
          headers: {
            "Content-Type": "application/json",
          },
        }
      );

      console.log("📨 [ocr.summarize] Response received:", {
        status: response.status,
        statusText: response.statusText,
        dataKeys: Object.keys(response.data || {}),
        hasData: !!response.data,
        hasSummary: !!(response.data && response.data.summary),
      });

      // 🚀 ENHANCED: Validate response structure
      if (!response.data) {
        throw new Error("Empty response from summary service");
      }

      if (response.data.error) {
        throw new Error(`Summary service error: ${response.data.error}`);
      }

      if (!response.data.summary) {
        console.warn(
          "⚠️ [ocr.summarize] No summary in response:",
          response.data
        );
        // Don't throw error, let the frontend handle it
      }

      console.log("✅ [ocr.summarize] Summary request completed successfully");
      return response;
    } catch (error: any) {
      console.error("❌ [ocr.summarize] Error summarizing text:", error);

      // 🚀 ENHANCED: Better error context
      if (error.response) {
        console.error("❌ [ocr.summarize] HTTP Error:", {
          status: error.response.status,
          statusText: error.response.statusText,
          data: error.response.data,
          url: error.config?.url,
        });
      } else if (error.request) {
        console.error("❌ [ocr.summarize] Network Error:", {
          message: error.message,
          code: error.code,
          url: error.config?.url,
        });
      } else {
        console.error("❌ [ocr.summarize] Request Setup Error:", error.message);
      }

      throw error;
    }
  },
};

export const ocrStatistics = {
  async incrementTotalAttempts() {
    return api.post("/api/ocrstatistics/increment-total");
  },
  async incrementSuccessfulAttempts() {
    return api.post("/api/ocrstatistics/increment-success");
  },
  async incrementFailedAttempts() {
    return api.post("/api/ocrstatistics/increment-failed");
  },
  async getStatistics() {
    return api.get("/api/ocrstatistics");
  },
};

// Export all services and types
export { user };
export type { RegisterDto, LoginDto, AuthResponse, UpdateUserDto, ApiResponse };
export default api;
