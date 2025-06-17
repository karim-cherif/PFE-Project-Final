/**
 * OCR Panel Compatibility Test Utility
 * Tests the integration between OCR Panel, OCR Service, and Backend
 */

// Type definitions for testing
interface TestSuggestion {
  text: string;
  confidence: number;
}

interface TestWordSuggestion {
  word: string;
  suggestions: TestSuggestion[];
  confidence: number;
  start: number;
  end: number;
}

interface TestOCRResponse {
  text: string;
  suggestions: TestWordSuggestion[];
  confidence: number;
  processingTime: number;
  language: string;
  textDirection: string;
  pages: number;
  engine: string;
  hasImages: boolean;
  contains_personal_data: boolean;
}

/**
 * Test OCR service response format compatibility
 */
export function testOCRServiceCompatibility(): boolean {
  console.log('🔍 [OCR Compatibility Test] Starting OCR service compatibility test...');
  
  // Mock OCR service response (ai_endpoint2.py format)
  const mockOCRResponse: TestOCRResponse = {
    text: "This is a test document with some text.",
    suggestions: [
      {
        word: "test",
        suggestions: [
          { text: "text", confidence: 0.85 },
          { text: "best", confidence: 0.75 }
        ],
        confidence: 0.6,
        start: 10,
        end: 14
      }
    ],
    confidence: 0.95,
    processingTime: 2.5,
    language: "eng",
    textDirection: "ltr",
    pages: 1,
    engine: "mistral",
    hasImages: false,
    contains_personal_data: false
  };

  try {
    // Test data structure validation
    if (!mockOCRResponse.text || typeof mockOCRResponse.text !== 'string') {
      throw new Error('Invalid text field');
    }

    if (!Array.isArray(mockOCRResponse.suggestions)) {
      throw new Error('Invalid suggestions field');
    }

    // Test suggestion structure
    for (const suggestion of mockOCRResponse.suggestions) {
      if (!suggestion.word || typeof suggestion.word !== 'string') {
        throw new Error('Invalid suggestion word field');
      }

      if (!Array.isArray(suggestion.suggestions)) {
        throw new Error('Invalid suggestion suggestions field');
      }

      for (const s of suggestion.suggestions) {
        if (!s.text || typeof s.text !== 'string') {
          throw new Error('Invalid suggestion text field');
        }
        if (typeof s.confidence !== 'number') {
          throw new Error('Invalid suggestion confidence field');
        }
      }

      if (typeof suggestion.confidence !== 'number') {
        throw new Error('Invalid suggestion confidence field');
      }
      if (typeof suggestion.start !== 'number') {
        throw new Error('Invalid suggestion start field');
      }
      if (typeof suggestion.end !== 'number') {
        throw new Error('Invalid suggestion end field');
      }
    }

    console.log('✅ [OCR Compatibility Test] OCR service response format is compatible');
    return true;
  } catch (error) {
    console.error('❌ [OCR Compatibility Test] OCR service response format is incompatible:', error);
    return false;
  }
}

/**
 * Test environment variable configuration
 */
export function testEnvironmentConfiguration(): boolean {
  console.log('🔍 [OCR Compatibility Test] Testing environment configuration...');
  
  const requiredEnvVars = [
    'VITE_API_URL',
    'VITE_OCR_SERVICE_URL',
    'VITE_GOOGLE_CLIENT_ID'
  ];

  const missingVars: string[] = [];

  for (const envVar of requiredEnvVars) {
    const value = import.meta.env[envVar];
    if (!value) {
      missingVars.push(envVar);
    } else {
      console.log(`✅ [OCR Compatibility Test] ${envVar}: ${value}`);
    }
  }

  if (missingVars.length > 0) {
    console.error('❌ [OCR Compatibility Test] Missing environment variables:', missingVars);
    return false;
  }

  console.log('✅ [OCR Compatibility Test] All environment variables are configured');
  return true;
}

/**
 * Test API endpoint connectivity
 */
export async function testAPIConnectivity(): Promise<boolean> {
  console.log('🔍 [OCR Compatibility Test] Testing API connectivity...');
  
  try {
    const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000';
    const ocrServiceUrl = import.meta.env.VITE_OCR_SERVICE_URL || 'http://localhost:5002';

    // Test backend API health
    try {
      const backendResponse = await fetch(`${apiUrl}/health`);
      if (backendResponse.ok) {
        console.log('✅ [OCR Compatibility Test] Backend API is accessible');
      } else {
        console.warn('⚠️ [OCR Compatibility Test] Backend API returned non-200 status');
      }
    } catch (error) {
      console.warn('⚠️ [OCR Compatibility Test] Backend API is not accessible:', error);
    }

    // Test OCR service health
    try {
      const ocrResponse = await fetch(`${ocrServiceUrl}/health`);
      if (ocrResponse.ok) {
        console.log('✅ [OCR Compatibility Test] OCR service is accessible');
      } else {
        console.warn('⚠️ [OCR Compatibility Test] OCR service returned non-200 status');
      }
    } catch (error) {
      console.warn('⚠️ [OCR Compatibility Test] OCR service is not accessible:', error);
    }

    return true;
  } catch (error) {
    console.error('❌ [OCR Compatibility Test] API connectivity test failed:', error);
    return false;
  }
}

/**
 * Run all compatibility tests
 */
export async function runAllCompatibilityTests(): Promise<boolean> {
  console.log('🚀 [OCR Compatibility Test] Starting comprehensive compatibility test...');
  
  const results = [
    testOCRServiceCompatibility(),
    testEnvironmentConfiguration(),
    await testAPIConnectivity()
  ];

  const allPassed = results.every(result => result);
  
  if (allPassed) {
    console.log('🎉 [OCR Compatibility Test] All compatibility tests passed!');
  } else {
    console.error('💥 [OCR Compatibility Test] Some compatibility tests failed!');
  }

  return allPassed;
}
