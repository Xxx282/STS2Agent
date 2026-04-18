"""阿里云 OSS 上传模块"""

from __future__ import annotations

import logging
import os
from pathlib import Path
from typing import Optional

import oss2

logging.basicConfig(level=logging.INFO, format="[%(levelname)s] %(message)s")
logger = logging.getLogger(__name__)


class Uploader:
    def __init__(self):
        self.endpoint = os.getenv("OSS_ENDPOINT", "")
        self.bucket_name = os.getenv("OSS_BUCKET", "")
        self.access_key_id = os.getenv("OSS_KEY", "")
        self.access_key_secret = os.getenv("OSS_SECRET", "")
        self.object_key = os.getenv("OSS_OBJECT_KEY", "card_stats.json")
        self.url_file = os.getenv("DATA_URL_FILE", "./data_url.txt")

        self._bucket: Optional[oss2.Bucket] = None
        self._validate()

    def _validate(self) -> None:
        required = [self.endpoint, self.bucket_name, self.access_key_id, self.access_key_secret]
        if not all(required):
            missing = [n for n, v in zip(["endpoint", "bucket", "key", "secret"], required) if not v]
            raise ValueError(f"缺少必需环境变量: {', '.join(missing)}")

    @property
    def bucket(self) -> oss2.Bucket:
        if self._bucket is None:
            auth = oss2.Auth(self.access_key_id, self.access_key_secret)
            self._bucket = oss2.Bucket(auth, self.endpoint, self.bucket_name)
        return self._bucket

    def upload(self, local_file: str | Path, object_key: str | None = None) -> str:
        """上传文件并返回公开访问 URL"""
        local_path = Path(local_file)
        if not local_path.exists():
            raise FileNotFoundError(f"本地文件不存在: {local_path}")

        key = object_key or self.object_key
        logger.info("正在上传 %s 到 OSS: %s/%s", local_path, self.bucket_name, key)
        self.bucket.put_object_from_file(key, str(local_path))
        logger.info("上传完成")

        # 构造公开 URL
        public_url = f"https://{self.bucket_name}.{self.endpoint.replace('https://', '')}/{key}"

        # 保存 URL 到文件（如果未指定自定义路径，则追加模式保存所有 URL）
        url_path = Path(self.url_file)
        url_path.parent.mkdir(parents=True, exist_ok=True)

        existing = []
        if url_path.exists():
            existing = [l.strip() for l in url_path.read_text(encoding="utf-8").splitlines() if l.strip()]

        if public_url not in existing:
            existing.append(public_url)

        url_path.write_text("\n".join(existing), encoding="utf-8")
        logger.info("数据 URL 已保存至: %s", url_path.absolute())

        return public_url

    def get_current_url(self) -> Optional[str]:
        """读取当前保存的数据 URL"""
        url_path = Path(self.url_file)
        if url_path.exists():
            return url_path.read_text(encoding="utf-8").strip()
        return None
