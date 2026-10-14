"""Prepare SQLite volume permissions, then run the API as an unprivileged user."""

from __future__ import annotations

import os
import pwd
import sys
from pathlib import Path


def main() -> None:
    database = Path(os.environ.get("DIRECTORY_DB", "/data/community-directory.sqlite3"))
    parent = database.parent
    parent.mkdir(parents=True, exist_ok=True)

    if os.geteuid() == 0:
        account = pwd.getpwnam("directory")
        os.chown(parent, account.pw_uid, account.pw_gid)
        if database.exists():
            os.chown(database, account.pw_uid, account.pw_gid)
        os.setgid(account.pw_gid)
        os.setuid(account.pw_uid)

    os.umask(0o077)
    os.execv(sys.executable, [sys.executable, "/app/service.py"])


if __name__ == "__main__":
    main()
