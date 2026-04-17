from __future__ import annotations

import hashlib
import hmac
import json
import logging
import re
import sys
import time
from collections import OrderedDict
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib.parse import urlencode, urlparse

import requests

SITE = "https://sts2log.com"
API_CARDS_URL = f"{SITE}/api/cards"
OUTPUT_DIR = Path(__file__).parent
OUTPUT_FILE = OUTPUT_DIR / "cards_data.json"

logging.basicConfig(
    level=logging.INFO,
    format="[%(levelname)s] %(message)s",
)
logger = logging.getLogger(__name__)

ALL_CHARS = ["IRONCLAD", "SILENT", "DEFECT", "AWAKENEDONE"]


@dataclass
class ScraperResult:
    success: bool
    data: list[dict[str, Any]] | None = None
    error: str | None = None


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
        """根据当前时间生成 x-skada-t，并用 HMAC-SHA256 生成 x-skada-s"""
        t = str(int(time.time()))

        # 按 key 排序参数
        sorted_params = OrderedDict(sorted(params.items()))
        query_string = urlencode(sorted_params)

        # 构造签名原文: t:pathname?排序后的参数
        path = urlparse(url).path
        sign_string = f"{t}:{path}?{query_string}" if query_string else f"{t}:{path}"

        # HMAC-SHA256 签名，密钥硬编码在 JS 中
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
        """访问主页获取 cookies"""
        logger.info("初始化 session，访问主页获取 cookies...")
        try:
            self.session.get(SITE, timeout=15)
            self.session.get(f"{SITE}/cards", timeout=15)
            logger.info("主页 cookies 获取完成，当前 cookies: %s", dict(self.session.cookies))
            return True
        except Exception as e:
            logger.error("初始化 session 失败: %s", e)
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

        logger.info("请求 URL: %s", API_CARDS_URL)
        logger.info("请求参数: %s", params)
        skada_s = headers.get("x-skada-s", "N/A")
        skada_t = headers.get("x-skada-t", "N/A")
        logger.info("请求头 x-skada-s: %s", skada_s)
        logger.info("请求头 x-skada-t: %s", skada_t)

        try:
            resp = self.session.get(API_CARDS_URL, params=params, headers=headers, timeout=30)
            logger.info("响应状态码: %d", resp.status_code)
            logger.info("响应头 Content-Type: %s", resp.headers.get("Content-Type"))

            resp.raise_for_status()
            data = resp.json()

            # 输出完整的响应结构
            logger.info("=" * 60)
            logger.info("原始响应结构 (keys): %s", list(data.keys()) if isinstance(data, dict) else type(data))
            if isinstance(data, dict):
                for key, value in data.items():
                    if isinstance(value, list):
                        logger.info("  %s: list[dict], 长度=%d", key, len(value))
                        if value:
                            logger.info("  %s[0] keys: %s", key, list(value[0].keys()) if isinstance(value[0], dict) else type(value[0]))
                    elif isinstance(value, dict):
                        logger.info("  %s: dict, keys=%s", key, list(value.keys()))
                    else:
                        logger.info("  %s: %s", key, type(value).__name__)
            logger.info("=" * 60)

            return data
        except requests.exceptions.RequestException as e:
            logger.error("API 请求失败: %s", e)
            return None
        except json.JSONDecodeError as e:
            logger.error("JSON 解析失败: %s，响应内容: %s", e, resp.text[:500])
            return None

    def scrape(self, chars: list[str] | None = None) -> ScraperResult:
        chars = chars or ["IRONCLAD"]  # 默认只爬 IRONCLAD

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

                # 调试：打印原始响应结构
                if page == 1:
                    logger.info("[DEBUG] 原始响应 keys: %s",
                                list(raw.keys()) if isinstance(raw, dict) else type(raw))

                # 提取数据列表（API 返回 { cards: [...], pagination: {...} }）
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

                logger.info("[%s] 第 %d/%d 页: 获取 %d 条卡牌", char, page, total_pages, len(items))
                all_cards.extend(items)

                if page == 1:
                    logger.info("[%s] 共 %d 页，总计 %d 条", char, total_pages, total_items)
                    logger.info("[DEBUG] 第一张卡牌字段: %s", list(items[0].keys()) if items else "无")

                page += 1
                page_count += 1
                if page_count > 1:
                    time.sleep(1)

        if not all_cards:
            return ScraperResult(success=False, error="未获取到任何卡牌数据")

        return ScraperResult(success=True, data=all_cards)

        if not all_cards:
            return ScraperResult(success=False, error="未获取到任何卡牌数据")

        return ScraperResult(success=True, data=all_cards)


def main() -> int:
    print("=" * 60)
    print("STS2 卡牌数据抓取测试")
    print("=" * 60)

    scraper = STS2CardScraper()

    result = scraper.scrape()

    if not result.success:
        print(f"\n抓取失败: {result.error}")
        return 1

    data = result.data
    logger.info("成功抓取 %d 张卡牌", len(data))

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    logger.info("数据已保存至: %s", OUTPUT_FILE)

    print("\n前 5 张卡牌预览:")
    for card in data[:5]:
        logger.info("card keys: %s", list(card.keys()))
        print(f"  - {card}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
