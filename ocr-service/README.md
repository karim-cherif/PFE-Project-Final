# OCR Service

A FastAPI-based OCR service that uses Tesseract for text recognition and Mistral AI for advanced text processing.

## Prerequisites

- Python 3.8+
- Tesseract OCR engine installed on your system
- For Windows: Download and install Tesseract from [GitHub](https://github.com/UB-Mannheim/tesseract/wiki)
- For macOS: `brew install tesseract`
- For Linux: `sudo apt-get install tesseract-ocr`
- Ollama installed with llama3.2 model
  - Install Ollama from [ollama.ai](https://ollama.ai)
  - Pull the llama3.2 model: `ollama pull llama3.2`
- Mistral API key for OCR processing

## Setup

1. Create a virtual environment:
```bash
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate
```

2. Install dependencies:
```bash
pip install -r requirements.txt
```

3. Create a `.env` file in the root directory with your Mistral API key:
```bash
MISTRAL_API_KEY=your_api_key_here
```

4. Start the service:
```bash
uvicorn ai_endpoint:app --reload
```

The service will be available at `http://localhost:8000`

## API Endpoints

### POST /api/ocr/process

Process an image file for OCR.

**Request:**
- Form data:
  - `file`: Image file (jpg, png, pdf, etc.)
  - `language`: Language code (e.g., 'eng', 'ara', 'fra')

**Response:**
```json
{
  "text": "Extracted text",
  "confidence": 95.5,
  "page_count": 1
}
```

## Error Handling

The service returns appropriate error messages with HTTP status codes:
- 400: Bad request (invalid file or language)
- 500: Internal server error (OCR processing failed)
- 401: Unauthorized (invalid or missing Mistral API key) 