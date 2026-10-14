FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    DIRECTORY_HOST=0.0.0.0 \
    DIRECTORY_PORT=8090 \
    DIRECTORY_DB=/data/community-directory.sqlite3

WORKDIR /app
COPY directory/service.py /app/service.py

RUN mkdir -p /data \
    && useradd --system --uid 10001 --home-dir /app directory \
    && chown directory:directory /data

USER 10001:10001
EXPOSE 8090
VOLUME ["/data"]

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8090/api/community/servers', timeout=2)" || exit 1

CMD ["python", "/app/service.py"]
