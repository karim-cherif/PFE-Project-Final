# =============================================================================
# OCR SERVICE DOCKERFILE - Python FastAPI
# =============================================================================
# Optimized for OCR processing with extended timeouts

FROM python:3.13-slim

# Install system dependencies for OCR processing
RUN apt-get update && apt-get install -y \
    tesseract-ocr \
    tesseract-ocr-eng \
    tesseract-ocr-fra \
    tesseract-ocr-ara \
    libtesseract-dev \
    poppler-utils \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create app user for security
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --gid 1001 appuser

# Set working directory
WORKDIR /app

# Copy requirements and install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY . .

# Set permissions
RUN chown -R appuser:appgroup /app && \
    chmod -R 755 /app

# Switch to non-root user
USER appuser

# Environment variables for OCR service
ENV PYTHONPATH=/app
ENV PYTHONUNBUFFERED=1

# Extended timeouts for OCR processing
ENV UVICORN_TIMEOUT_KEEP_ALIVE=300
ENV UVICORN_TIMEOUT_GRACEFUL_SHUTDOWN=30

# Expose port
EXPOSE 5002

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5002/health || exit 1

# Start the application with extended timeouts
CMD ["uvicorn", "ai_endpoint2:app", "--host", "0.0.0.0", "--port", "5002", "--timeout-keep-alive", "300", "--timeout-graceful-shutdown", "30"]
