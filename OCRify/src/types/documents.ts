export interface BackendDocumentDto {
  id: string
  fileName: string
  filePath: string
  fileSize: number
  fileType: string
  status: number
  language: string
  topic?: string
  description?: string
  tags: string[]
  isFavorite: boolean
  uploadDate: string
  lastModified?: string
  deletedAt?: string
  ocrText?: string
  ocrResponse?: string
  enhancedText?: string
  errorMessage?: string
  pageCount?: number
  corrections: any[]
  accuracy?: number
  isDeleted: boolean
}

export interface DocumentDto {
  id: string
  name: string
  path: string
  size: number
  type: string
  status: string
  language: string
  topic?: string
  description?: string
  tags: string[]
  isFavorite: boolean
  createdAt: string
  updatedAt: string
  deletedAt?: string
  accuracy?: number
  thumbnail?: string
  isDeleted: boolean
  pageCount?: number
}

export interface DocumentSearchResultDto {
  success: boolean
  message: string
  data: {
    items: BackendDocumentDto[]
    totalCount: number
    pageSize: number
    currentPage: number
  }
}

export interface DocumentSearchDto {
  searchTerm?: string;
  language?: string;
  topic?: string;
  status?: string;
  dateFrom?: string;
  dateTo?: string;
  type?: string;
  documentType?: number;
  page?: number;
  pageSize?: number;
  includeDeletedNum?: number;
  favoritesOnly?: boolean;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
}

export interface DashboardDataDto {
  // Basic Statistics
  totalDocuments: number
  processedDocuments: number
  processingSuccessRate: number
  totalStorageUsed: number

  // Document Status Statistics
  documentsByStatus: Record<string, number>
  
  // Language Statistics
  documentsByLanguage: Record<string, number>
  
  // Recent Documents
  recentDocuments: BackendDocumentDto[]
  
  // Recent Activity
  recentActivity: Array<{
    id: string
    userId: string
    action: string
    timestamp: string
  }>
  
  // Processing Statistics
  documentsInProcessing: number
  failedDocuments: number
  averageProcessingTime: number
}

export interface DocumentTypeDistribution {
    type: string;
    nombre: number;
}

export interface DashboardResponse {
  documentsProcessed: number;
  processedDocuments: number;
  documentsToday: number;
  processingTime: string;
  processingQueue: number;
  averageAccuracy: number;
  successRate: number;
  errorRate: number;
  recoveryRate: number;
  characterExtractionRate: number;
  totalStorage: number;
  storageGrowth: number;
  languagesDetected: string[];
  totalCharacters: string;
  weeklyActivity: {
    day: string;
    documents: number;
    accuracy: number;
  }[];
  monthlyTrends: {
    month: string;
    active: number;
    deleted: number;
  }[];
  languageDistribution: {
    name: string;
    docs: number;
  }[];
  processingMetrics: {
    metric: string;
    value: number;
  }[];
  documentTypeDistribution: DocumentTypeDistribution[];
}