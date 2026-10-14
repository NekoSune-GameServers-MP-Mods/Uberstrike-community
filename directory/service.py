"""Small SQLite-backed community directory API (Python 3.11+, stdlib only).

This service is the directory control plane. It deliberately does not mark a server
online from a heartbeat: a trusted realtime probe integration must do that.
"""

from __future__ import annotations

import hashlib
import hmac
import ipaddress
import json
import os
import re
import sqlite3
import threading
import time
from contextlib import closing
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any


MAX_BODY = 16_384
TOKEN_RE = re.compile(r"^[A-Za-z0-9_-]{40,128}$")
SERVER_ID_RE = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$")
PUBLIC_FIELDS = (
    "id", "name", "address", "port", "region", "game_mode", "current_map",
    "player_count", "max_players", "protocol_version", "software_version",
    "server_software", "usage_type", "status", "last_successful_probe",
)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def validate_public_ipv4(value: Any) -> str:
    if not isinstance(value, str):
        raise ValueError("address must be an IPv4 address")
    try:
        address = ipaddress.ip_address(value)
    except ValueError as exc:
        raise ValueError("address must be a public IPv4 address literal") from exc
    if address.version != 4 or not address.is_global:
        raise ValueError("address must be a public IPv4 address")
    return str(address)


def validate_metadata(data: dict[str, Any]) -> dict[str, Any]:
    name = data.get("name")
    if not isinstance(name, str) or not name.strip() or len(name.strip()) > 64:
        raise ValueError("name must contain 1 to 64 characters")
    address = validate_public_ipv4(data.get("address"))
    port = data.get("port")
    if type(port) is not int or not 1 <= port <= 65535:
        raise ValueError("port must be an integer from 1 to 65535")
    maximum = data.get("max_players")
    players = data.get("player_count", 0)
    if type(maximum) is not int or not 1 <= maximum <= 256:
        raise ValueError("max_players must be an integer from 1 to 256")
    if type(players) is not int or not 0 <= players <= maximum:
        raise ValueError("player_count must be between zero and max_players")

    result: dict[str, Any] = {
        "name": name.strip(), "address": address, "port": port,
        "region": _short_text(data.get("region", ""), 32, "region"),
        "game_mode": _short_text(data.get("game_mode", ""), 32, "game_mode"),
        "current_map": _short_text(data.get("current_map", ""), 64, "current_map"),
        "player_count": players, "max_players": maximum,
        "protocol_version": _short_text(data.get("protocol_version", ""), 32, "protocol_version"),
        "software_version": _short_text(data.get("software_version", ""), 64, "software_version"),
        "server_software": data.get("server_software", "other"),
        "usage_type": data.get("usage_type", "game"),
        "visibility": data.get("visibility", "public"),
    }
    if result["server_software"] not in ("nekonexus", "paradise", "other"):
        raise ValueError("server_software must be nekonexus, paradise, or other")
    if result["usage_type"] not in ("game", "comm"):
        raise ValueError("usage_type must be game or comm")
    if result["visibility"] not in ("public", "private"):
        raise ValueError("visibility must be public or private")
    return result


def _short_text(value: Any, limit: int, field: str) -> str:
    if not isinstance(value, str) or len(value) > limit:
        raise ValueError(f"{field} must be a string of at most {limit} characters")
    if any(ord(ch) < 32 for ch in value):
        raise ValueError(f"{field} contains control characters")
    return value.strip()


class Directory:
    def __init__(self, database: str):
        self.db_path = database
        self._lock = threading.Lock()
        with closing(self._connect()) as db, db:
            db.execute("""CREATE TABLE IF NOT EXISTS servers (
              id TEXT PRIMARY KEY, credential_hash TEXT NOT NULL,
              name TEXT NOT NULL, address TEXT NOT NULL, port INTEGER NOT NULL,
              region TEXT NOT NULL, game_mode TEXT NOT NULL, current_map TEXT NOT NULL,
              player_count INTEGER NOT NULL, max_players INTEGER NOT NULL,
              protocol_version TEXT NOT NULL, software_version TEXT NOT NULL,
              server_software TEXT NOT NULL DEFAULT 'other', usage_type TEXT NOT NULL DEFAULT 'game',
              visibility TEXT NOT NULL DEFAULT 'public', enabled INTEGER NOT NULL DEFAULT 1,
              status TEXT NOT NULL DEFAULT 'unknown', last_heartbeat REAL,
              last_probe TEXT, last_nonce TEXT, created_at TEXT NOT NULL
            )""")
            columns = {row["name"] for row in db.execute("PRAGMA table_info(servers)")}
            if "server_software" not in columns:
                db.execute("ALTER TABLE servers ADD COLUMN server_software TEXT NOT NULL DEFAULT 'other'")
            if "usage_type" not in columns:
                db.execute("ALTER TABLE servers ADD COLUMN usage_type TEXT NOT NULL DEFAULT 'game'")

    def _connect(self) -> sqlite3.Connection:
        db = sqlite3.connect(self.db_path, timeout=10)
        db.row_factory = sqlite3.Row
        return db

    def register(self, server_id: str, token: str, metadata: dict[str, Any]) -> None:
        if not SERVER_ID_RE.fullmatch(server_id):
            raise ValueError("id must be a random UUID v4")
        if not TOKEN_RE.fullmatch(token):
            raise ValueError("credential must be a random URL-safe token of at least 40 characters")
        record = validate_metadata(metadata)
        with self._lock, closing(self._connect()) as db, db:
            try:
                db.execute("""INSERT INTO servers
                    (id,credential_hash,name,address,port,region,game_mode,current_map,
                     player_count,max_players,protocol_version,software_version,server_software,
                     usage_type,visibility,created_at)
                    VALUES (:id,:credential_hash,:name,:address,:port,:region,:game_mode,:current_map,
                     :player_count,:max_players,:protocol_version,:software_version,:server_software,
                     :usage_type,:visibility,:created_at)""",
                    {**record, "id": server_id,
                     "credential_hash": self._hash(token), "created_at": utc_now()})
            except sqlite3.IntegrityError as exc:
                raise ValueError("server id is already registered") from exc

    def heartbeat(self, server_id: str, token: str, timestamp: int, nonce: str,
                  signature: str, metadata: dict[str, Any]) -> None:
        now = int(time.time())
        if type(timestamp) is not int or abs(now - timestamp) > 60:
            raise ValueError("heartbeat timestamp is outside the 60 second window")
        if not isinstance(nonce, str) or not re.fullmatch(r"[A-Za-z0-9_-]{16,128}", nonce):
            raise ValueError("nonce must be a fresh random value")
        if not isinstance(signature, str) or not re.fullmatch(r"[0-9a-f]{64}", signature):
            raise ValueError("signature must be a SHA-256 HMAC")
        record = validate_metadata(metadata)
        message = f"{server_id}\n{timestamp}\n{nonce}\n".encode() + json.dumps(
            record, sort_keys=True, separators=(",", ":")
        ).encode()
        with self._lock, closing(self._connect()) as db, db:
            row = db.execute("SELECT credential_hash,last_nonce FROM servers WHERE id=?", (server_id,)).fetchone()
            if row is None or not hmac.compare_digest(row["credential_hash"], self._hash(token)):
                raise PermissionError("unknown server or invalid credential")
            expected = hmac.new(token.encode(), message, hashlib.sha256).hexdigest()
            if not hmac.compare_digest(expected, signature):
                raise PermissionError("invalid heartbeat signature")
            if row["last_nonce"] == nonce:
                raise PermissionError("heartbeat replay rejected")
            db.execute("""UPDATE servers SET name=:name,address=:address,port=:port,region=:region,
                game_mode=:game_mode,current_map=:current_map,player_count=:player_count,
                max_players=:max_players,protocol_version=:protocol_version,
                software_version=:software_version,server_software=:server_software,
                usage_type=:usage_type,visibility=:visibility,
                last_heartbeat=:last_heartbeat,last_nonce=:last_nonce WHERE id=:id""",
                {**record, "last_heartbeat": float(timestamp), "last_nonce": nonce, "id": server_id})

    def set_probe_result(self, server_id: str, reachable: bool, checked_at: str | None = None) -> None:
        """Called only by a future trusted, protocol-aware probe worker."""
        status = "online" if reachable else "offline"
        with self._lock, closing(self._connect()) as db, db:
            db.execute("UPDATE servers SET status=?,last_probe=? WHERE id=?",
                       (status, checked_at or utc_now(), server_id))

    def public_servers(self) -> list[dict[str, Any]]:
        now = time.time()
        with closing(self._connect()) as db, db:
            rows = db.execute("""SELECT *, last_probe AS last_successful_probe FROM servers WHERE enabled=1 AND visibility='public'
                AND status='online' AND last_heartbeat >= ? ORDER BY name COLLATE NOCASE""",
                (now - 90,)).fetchall()
        return [{field: row[field] for field in PUBLIC_FIELDS} for row in rows]

    @staticmethod
    def _hash(token: str) -> str:
        return hashlib.sha256(token.encode()).hexdigest()


def signature_for(token: str, server_id: str, timestamp: int, nonce: str,
                  metadata: dict[str, Any]) -> str:
    record = validate_metadata(metadata)
    message = f"{server_id}\n{timestamp}\n{nonce}\n".encode() + json.dumps(
        record, sort_keys=True, separators=(",", ":")
    ).encode()
    return hmac.new(token.encode(), message, hashlib.sha256).hexdigest()


def make_handler(directory: Directory) -> type[BaseHTTPRequestHandler]:
    request_history: dict[tuple[str, str], list[float]] = {}
    rate_lock = threading.Lock()

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, fmt: str, *args: Any) -> None:
            print(f"[{utc_now()}] {self.client_address[0]} {fmt % args}")

        def _body(self) -> dict[str, Any]:
            length = int(self.headers.get("Content-Length", "0"))
            if not 0 < length <= MAX_BODY:
                raise ValueError("request body must be between 1 and 16384 bytes")
            data = json.loads(self.rfile.read(length))
            if not isinstance(data, dict):
                raise ValueError("request body must be a JSON object")
            return data

        def _json(self, status: int, payload: Any) -> None:
            body = json.dumps(payload, separators=(",", ":")).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(body)

        def _rate_limited(self, bucket: str, limit: int) -> bool:
            now = time.time()
            key = (self.client_address[0], bucket)
            with rate_lock:
                hits = [hit for hit in request_history.get(key, []) if hit > now - 60]
                request_history[key] = hits
                if len(request_history) > 10_000:
                    for old_key in list(request_history)[:2_000]:
                        request_history.pop(old_key, None)
                if len(hits) >= limit:
                    return True
                hits.append(now)
                request_history[key] = hits
                return False

        def do_GET(self) -> None:
            if self._rate_limited("read", 120):
                self._json(429, {"error": "rate_limited"})
                return
            if self.path != "/api/community/servers":
                self._json(404, {"error": "not_found"})
                return
            self._json(200, {"servers": directory.public_servers()})

        def do_POST(self) -> None:
            bucket = "register" if self.path == "/api/v1/servers/register" else "heartbeat"
            limit = 5 if bucket == "register" else 60
            if self._rate_limited(bucket, limit):
                self._json(429, {"error": "rate_limited"})
                return
            try:
                data = self._body()
                if self.path == "/api/v1/servers/register":
                    directory.register(data.get("id", ""), data.get("credential", ""), data)
                    self._json(201, {"id": data["id"], "status": "registered_unverified"})
                elif self.path.startswith("/api/v1/servers/") and self.path.endswith("/heartbeat"):
                    server_id = self.path.removeprefix("/api/v1/servers/").removesuffix("/heartbeat").strip("/")
                    directory.heartbeat(server_id, data.get("credential", ""), data.get("timestamp"),
                                        data.get("nonce", ""), data.get("signature", ""), data)
                    self._json(200, {"status": "heartbeat_received_unverified"})
                else:
                    self._json(404, {"error": "not_found"})
            except PermissionError as exc:
                self._json(401, {"error": str(exc)})
            except (ValueError, TypeError, KeyError, json.JSONDecodeError) as exc:
                self._json(400, {"error": str(exc)})

    return Handler


def main() -> None:
    host = os.environ.get("DIRECTORY_HOST", "127.0.0.1")
    port = int(os.environ.get("DIRECTORY_PORT", "8090"))
    database = os.environ.get("DIRECTORY_DB", "community-directory.sqlite3")
    directory = Directory(database)
    server = ThreadingHTTPServer((host, port), make_handler(directory))
    print(f"Community directory listening on http://{host}:{port}")
    server.serve_forever()


if __name__ == "__main__":
    main()
