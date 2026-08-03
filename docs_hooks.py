"""MkDocs hooks — copy AI entrypoints into the built site root."""

from __future__ import annotations

import shutil
from pathlib import Path


def on_post_build(config, **kwargs) -> None:
    docs_dir = Path(config["docs_dir"])
    site_dir = Path(config["site_dir"])
    for name in ("llms.txt",):
        src = docs_dir / name
        if src.is_file():
            shutil.copy2(src, site_dir / name)
