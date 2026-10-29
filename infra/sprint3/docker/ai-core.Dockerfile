# Use official lightweight Python image
FROM docker.io/library/python@sha256:1148dbb014615300fa7e713b2f94f550c0d1c69780c0805f84f093ceee34dd0b

# Set environment variables
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PYTHONPATH=/app

WORKDIR /app

# Install system dependencies (curl for container healthcheck)
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy and install python dependencies
COPY requirements.txt requirements.lock ./
RUN pip install --only-binary=:all: --no-cache-dir \
    -c requirements.lock \
    -r requirements.txt

# Create non-root user for container security
RUN groupadd -g 1001 appuser && useradd -m -u 1001 -g 1001 appuser

# Copy application source code and configuration
COPY ./app /app/app
COPY ./config /app/config

# Set ownership
RUN chown -R appuser:appuser /app
USER 1001:1001

# Expose service port
EXPOSE 8000

# Container healthcheck
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

# Start FastAPI via Uvicorn
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
