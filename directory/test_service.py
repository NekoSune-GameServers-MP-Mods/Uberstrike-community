import os
import http.client
import json
import tempfile
import threading
import time
from http.server import ThreadingHTTPServer
import unittest

from service import Directory, make_handler, signature_for


SERVER_ID = "123e4567-e89b-42d3-a456-426614174000"
TOKEN = "A" * 48
META = {
    "name": "Community Arena", "address": "8.8.8.8", "port": 5155,
    "region": "EU", "game_mode": "DM", "current_map": "SpaceCity",
    "player_count": 2, "max_players": 16, "protocol_version": "4.3.10",
    "software_version": "0.1.0", "server_software": "nekonexus",
    "usage_type": "game", "visibility": "public",
}


class DirectoryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Directory(os.path.join(self.temp.name, "directory.sqlite3"))
        self.directory.register(SERVER_ID, TOKEN, META)

    def tearDown(self):
        if hasattr(self, "http_server"):
            self.http_server.shutdown()
            self.http_server.server_close()
            self.http_thread.join(timeout=2)
        self.temp.cleanup()

    def heartbeat(self, nonce="abcdefghijklmnop", token=TOKEN):
        timestamp = int(time.time())
        signature = signature_for(token, SERVER_ID, timestamp, nonce, META)
        self.directory.heartbeat(SERVER_ID, token, timestamp, nonce, signature, META)

    def test_heartbeat_alone_never_publishes_server(self):
        self.heartbeat()
        self.assertEqual([], self.directory.public_servers())

    def test_only_fresh_public_verified_record_is_returned_without_credentials(self):
        self.heartbeat()
        self.directory.set_probe_result(SERVER_ID, True)
        records = self.directory.public_servers()
        self.assertEqual(1, len(records))
        self.assertEqual(SERVER_ID, records[0]["id"])
        self.assertNotIn("credential", records[0])
        self.assertNotIn("credential_hash", records[0])
        self.assertNotIn("last_heartbeat", records[0])

    def test_replay_and_invalid_credential_are_rejected(self):
        self.heartbeat()
        with self.assertRaises(PermissionError):
            self.heartbeat()
        with self.assertRaises(PermissionError):
            self.heartbeat(nonce="qrstuvwxyzabcdef", token="B" * 48)

    def test_private_or_offline_server_is_not_public(self):
        self.heartbeat()
        private = {**META, "visibility": "private"}
        # Update through a valid owner heartbeat.
        timestamp = int(time.time())
        nonce = "qrstuvwxyzabcdef"
        signature = signature_for(TOKEN, SERVER_ID, timestamp, nonce, private)
        self.directory.heartbeat(SERVER_ID, TOKEN, timestamp, nonce, signature, private)
        self.directory.set_probe_result(SERVER_ID, True)
        self.assertEqual([], self.directory.public_servers())

    def test_paradise_records_are_supported(self):
        server_id = "523e4567-e89b-42d3-a456-426614174000"
        token = "F" * 48
        metadata = {**META, "name": "Paradise Arena", "server_software": "paradise"}
        self.directory.register(server_id, token, metadata)
        timestamp = int(time.time())
        nonce = "paradiseprobe1234"
        signature = signature_for(token, server_id, timestamp, nonce, metadata)
        self.directory.heartbeat(server_id, token, timestamp, nonce, signature, metadata)
        self.directory.set_probe_result(server_id, True)
        records = self.directory.public_servers()
        paradise = next(row for row in records if row["id"] == server_id)
        self.assertEqual("paradise", paradise["server_software"])
        self.assertEqual("game", paradise["usage_type"])

    def test_address_and_player_count_are_validated(self):
        with self.assertRaises(ValueError):
            self.directory.register("223e4567-e89b-42d3-a456-426614174000", "C" * 48,
                                    {**META, "address": "127.0.0.1"})
        with self.assertRaises(ValueError):
            self.directory.register("323e4567-e89b-42d3-a456-426614174000", "D" * 48,
                                    {**META, "player_count": 17})

    def test_http_registration_and_public_endpoint(self):
        self.http_server = ThreadingHTTPServer(("127.0.0.1", 0), make_handler(self.directory))
        self.http_thread = threading.Thread(target=self.http_server.serve_forever, daemon=True)
        self.http_thread.start()
        conn = http.client.HTTPConnection("127.0.0.1", self.http_server.server_port)
        body = json.dumps({**META, "id": "423e4567-e89b-42d3-a456-426614174000",
                           "credential": "E" * 48})
        conn.request("POST", "/api/v1/servers/register", body,
                     {"Content-Type": "application/json"})
        response = conn.getresponse()
        self.assertEqual(201, response.status)
        self.assertEqual("registered_unverified", json.loads(response.read())["status"])
        conn.request("GET", "/api/community/servers")
        response = conn.getresponse()
        self.assertEqual(200, response.status)
        self.assertEqual([], json.loads(response.read())["servers"])
        for expected in (400, 400, 400, 400, 429):
            conn.request("POST", "/api/v1/servers/register", body,
                         {"Content-Type": "application/json"})
            response = conn.getresponse()
            self.assertEqual(expected, response.status)
            response.read()
        conn.close()


if __name__ == "__main__":
    unittest.main()
