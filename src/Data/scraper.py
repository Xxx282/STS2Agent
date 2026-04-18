"""STS2 卡牌数据爬虫 - 从 sts2log.com 抓取社区统计数据"""

from __future__ import annotations

import hashlib
import hmac
import json
import logging
import time
from collections import OrderedDict
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Any
from urllib.parse import urlencode, urlparse

import requests

SITE = "https://sts2log.com"
API_CARDS_URL = f"{SITE}/api/cards"
ALL_CHARS = ["IRONCLAD", "SILENT", "DEFECT", "NECROBINDER", "REGENT"]

# OSS 角色路径映射（API 角色名 -> OSS 文件夹）
CHAR_TO_FOLDER = {
    "IRONCLAD": "ironclad",
    "SILENT": "silent",
    "DEFECT": "defect",
    "NECROBINDER": "necrobinder",
    "REGENT": "regent",
}

logging.basicConfig(
    level=logging.INFO,
    format="[%(levelname)s] %(message)s",
)
logger = logging.getLogger(__name__)


@dataclass
class ScraperResult:
    success: bool
    data: list[dict[str, Any]] | None = None
    error: str | None = None


@dataclass
class CardStatsOutput:
    """最终输出的 JSON 结构"""
    version: int = 1
    updated_at: str = ""
    characters: list[str] = None
    data: dict[str, list[dict[str, Any]]] = None

    def __post_init__(self):
        if self.characters is None:
            self.characters = ALL_CHARS.copy()
        if self.data is None:
            self.data = {char: [] for char in ALL_CHARS}

    def to_dict(self) -> dict[str, Any]:
        return {
            "version": self.version,
            "updated_at": self.updated_at,
            "characters": self.characters,
            "data": self.data,
        }

    def save(self, path: str | Path) -> None:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(self.to_dict(), f, ensure_ascii=False, indent=2)
        logger.info("数据已保存至: %s", path)

    def save_per_char(self, output_dir: str | Path) -> dict[str, Path]:
        """按角色分别保存为独立 JSON 文件，返回 {角色: 文件路径}"""
        out_dir = Path(output_dir)
        out_dir.mkdir(parents=True, exist_ok=True)

        saved = {}
        for char, cards in self.data.items():
            char_lower = char.lower()
            folder = CHAR_TO_FOLDER.get(char, char_lower)
            file_path = out_dir / folder / "card_stats.json"

            file_path.parent.mkdir(parents=True, exist_ok=True)

            char_data = {
                "version": self.version,
                "updated_at": self.updated_at,
                "characters": [char],
                "data": {char: cards},
            }
            with open(file_path, "w", encoding="utf-8") as f:
                json.dump(char_data, f, ensure_ascii=False, indent=2)

            saved[char] = file_path
            logger.info("角色 %s 数据已保存至: %s", char, file_path)

        return saved


class STS2CardScraper:
    def __init__(self, session: requests.Session | None = None):
        self.session = session or requests.Session()
        self._base_headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                          "(KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36 Edg/147.0.0.0",
            "Accept": "application/json",
            "Accept-Language": "zh-CN,zh;q=0.9,en;q=0.8",
            "Referer": f"{SITE}/cards",
            "sec-ch-ua": '"Microsoft Edge";v="147", "Not.A/Brand";v="8", "Chromium";v="147"',
            "sec-ch-ua-mobile": "?0",
            "sec-ch-ua-platform": '"Windows"',
            "sec-ch-ua-platform-version": '"10.0.0"',
            "sec-fetch-dest": "empty",
            "sec-fetch-mode": "cors",
            "sec-fetch-site": "same-origin",
        }

    def _update_skada_headers(self, url: str, params: dict[str, str]) -> dict[str, str]:
        t = str(int(time.time()))
        sorted_params = OrderedDict(sorted(params.items()))
        query_string = urlencode(sorted_params)
        path = urlparse(url).path
        sign_string = f"{t}:{path}?{query_string}" if query_string else f"{t}:{path}"
        secret = "xK7m2pQ9dR4wF1jN8sL3vB6hY0tG5cA"
        signature = hmac.new(secret.encode(), sign_string.encode(), hashlib.sha256).hexdigest()
        return {
            "x-skada-t": t,
            "x-skada-s": signature,
        }

    def _build_headers(self, url: str, params: dict[str, str]) -> dict[str, str]:
        skada_headers = self._update_skada_headers(url, params)
        return {
            **self._base_headers,
            "x-skada-s": skada_headers["x-skada-s"],
            "x-skada-t": skada_headers["x-skada-t"],
        }

    def init_session(self) -> bool:
        try:
            self.session.get(SITE, timeout=15)
            self.session.get(f"{SITE}/cards", timeout=15)
            logger.info("Session 初始化完成")
            return True
        except Exception as e:
            logger.error("Session 初始化失败: %s", e)
            return False

    def fetch_api(
        self,
        char: str,
        page: int = 1,
        page_size: int = 20,
        sort_by: str = "skada_score",
        sort_dir: str = "desc",
    ) -> dict[str, Any] | None:
        params = {
            "char": char,
            "page": str(page),
            "page_size": str(page_size),
            "sort_by": sort_by,
            "sort_dir": sort_dir,
        }
        headers = self._build_headers(API_CARDS_URL, params)

        try:
            resp = self.session.get(API_CARDS_URL, params=params, headers=headers, timeout=30)
            resp.raise_for_status()
            return resp.json()
        except Exception as e:
            logger.error("[%s] 第 %d 页请求失败: %s", char, page, e)
            return None

    def scrape(self, chars: list[str] | None = None) -> ScraperResult:
        chars = chars or ["IRONCLAD"]

        if not self.init_session():
            return ScraperResult(success=False, error="session 初始化失败")

        all_cards: list[dict[str, Any]] = []

        for char in chars:
            page = 1
            total_pages = 1
            page_count = 0

            while page <= total_pages:
                raw = self.fetch_api(char, page=page)
                if raw is None:
                    logger.warning("[%s] 请求失败，停止翻页", char)
                    break

                if isinstance(raw, dict):
                    items = raw.get("cards", [])
                    pagination = raw.get("pagination", {})
                    total_pages = pagination.get("total_pages", 1)
                    total_items = pagination.get("total_items", 0)
                else:
                    items = raw if isinstance(raw, list) else []
                    total_pages = 1
                    total_items = len(items)

                if not items:
                    logger.warning("[%s] 第 %d 页无数据，停止翻页", char, page)
                    break

                logger.info("[%s] 第 %d/%d 页: 获取 %d 条", char, page, total_pages, len(items))
                all_cards.extend(items)

                if page == 1:
                    logger.info("[%s] 共 %d 页，总计 %d 条", char, total_pages, total_items)

                page += 1
                page_count += 1
                if page_count > 1:
                    time.sleep(1)

        if not all_cards:
            return ScraperResult(success=False, error="未获取到任何卡牌数据")

        return ScraperResult(success=True, data=all_cards)

    def scrape_all(self) -> CardStatsOutput:
        """抓取所有角色数据并整理为标准输出格式"""
        result = self.scrape(ALL_CHARS)
        if not result.success:
            logger.error("抓取失败: %s", result.error)
            return CardStatsOutput()

        cards = result.data or []

        output = CardStatsOutput()
        output.updated_at = time.strftime("%Y-%m-%dT%H:%M:%S+08:00")

        # 按角色分组
        for card in cards:
            char = card.get("character", "UNKNOWN")
            if char not in output.data:
                output.data[char] = []

            # 提取核心统计字段
            stats = self._extract_stats(card)
            output.data[char].append(stats)

        # 对每个角色的数据按 skada_score 降序排列
        for char in output.data:
            output.data[char].sort(key=lambda x: x.get("skada_score", 0), reverse=True)
            # 添加 rank
            for i, item in enumerate(output.data[char], 1):
                item["rank"] = i

        return output

    def _extract_stats(self, card: dict[str, Any]) -> dict[str, Any]:
        """从原始卡牌数据中提取需要的统计字段"""
        # 尝试获取 display_name
        display_name = card.get("display_name", {})
        if isinstance(display_name, dict):
            name_zh = display_name.get("zh") or display_name.get("en", "")
            name_en = display_name.get("en", "")
        else:
            name_zh = ""
            name_en = str(display_name) if display_name else ""

        return {
            "card_id": card.get("card_id") or card.get("id") or "",
            "pick_rate": card.get("pick_rate") or 0.0,
            "win_rate_delta": card.get("win_rate_delta") or 0.0,
            "skada_score": card.get("skada_score") or 0.0,
            "rank": 0,
            "confidence": card.get("confidence", "low"),
            "display_name": {
                "en": name_en,
                "zh": name_zh,
            },
        }
