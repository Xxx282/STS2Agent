"""CLI 入口 - 整合爬取与上传"""

import argparse
import logging
import os
import sys
from pathlib import Path

from dotenv import load_dotenv

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from src.Data.scraper import STS2CardScraper, ALL_CHARS, CHAR_TO_FOLDER
from src.Data.uploader import Uploader

logging.basicConfig(
    level=logging.INFO,
    format="[%(levelname)s] %(message)s",
)
logger = logging.getLogger(__name__)


def main() -> int:
    parser = argparse.ArgumentParser(description="STS2 卡牌数据爬取与上传")
    parser.add_argument("--scrape-only", action="store_true", help="仅抓取，不上传")
    parser.add_argument("--upload-only", action="store_true", help="仅上传本地数据文件")
    parser.add_argument("--url", action="store_true", help="输出当前数据 URL")
    parser.add_argument("--output", default="./card_stats", help="本地输出目录（默认 ./card_stats，按角色分文件保存）")
    parser.add_argument("--char", default=None, help="指定单个角色抓取（如 ironclad），默认抓取全部")
    args = parser.parse_args()

    # 加载 .env
    env_path = Path(__file__).parent.parent.parent / ".env"
    if env_path.exists():
        load_dotenv(env_path)
        logger.info("已加载 .env 配置")
    else:
        logger.warning(".env 文件不存在，将使用系统环境变量")

    # --url 模式
    if args.url:
        uploader = Uploader() if _has_oss_config() else None
        if uploader:
            url = uploader.get_current_url()
            if url:
                print(url)
                return 0
            else:
                logger.error("未找到数据 URL，请先运行抓取+上传")
                return 1
        else:
            logger.error("未配置 OSS 环境变量")
            return 1

    # --upload-only 模式：扫描输出目录，上传所有角色文件夹
    if args.upload_only:
        uploader = Uploader()
        out_dir = Path(args.output)
        if not out_dir.exists():
            logger.error("输出目录不存在: %s", out_dir)
            return 1

        success_count = 0
        for char, folder in CHAR_TO_FOLDER.items():
            char_file = out_dir / folder / "card_stats.json"
            if char_file.exists():
                try:
                    url = uploader.upload(char_file, object_key=f"STS2/{folder}/card_stats.json")
                    logger.info("[%s] 上传成功: %s", char, url)
                    success_count += 1
                except Exception as e:
                    logger.error("[%s] 上传失败: %s", char, e)
            else:
                logger.warning("[%s] 文件不存在，跳过: %s", char, char_file)

        if success_count == 0:
            logger.error("没有成功上传任何文件")
            return 1
        return 0

    # 默认：抓取 + 上传
    scraper = STS2CardScraper()

    # 解析 --char 参数（接受 API 角色名或文件夹名）
    target_chars = None
    if args.char:
        # 支持传 API 角色名（如 IRONCLAD）或文件夹名（如 ironclad）
        char_upper = args.char.upper()
        char_lower = args.char.lower()
        matched = None
        for api_char in ALL_CHARS:
            if api_char == char_upper or CHAR_TO_FOLDER.get(api_char) == char_lower:
                matched = api_char
                break
        if matched:
            target_chars = [matched]
            logger.info("仅抓取角色: %s", matched)
        else:
            logger.error("未知角色: %s，可选: %s", args.char, ALL_CHARS)
            return 1

    # 构建 scraper 的 chars 参数（API 用大写）
    chars_to_scrape = target_chars if target_chars else ALL_CHARS
    logger.info("开始抓取角色数据: %s", chars_to_scrape)

    output = scraper.scrape_all()
    if not output.updated_at:
        logger.error("抓取失败")
        return 1

    # 按角色分文件保存本地
    saved_files = output.save_per_char(args.output)
    logger.info("抓取完成，共 %d 个角色文件", len(saved_files))

    # 上传（如果配置了 OSS）
    if not args.scrape_only:
        if _has_oss_config():
            uploader = Uploader()
            success_count = 0
            for char, file_path in saved_files.items():
                folder = CHAR_TO_FOLDER.get(char, char.lower())
                try:
                    url = uploader.upload(file_path, object_key=f"STS2/{folder}/card_stats.json")
                    logger.info("[%s] 上传成功: %s", char, url)
                    success_count += 1
                except Exception as e:
                    logger.error("[%s] 上传失败: %s", char, e)
            if success_count == 0:
                logger.error("没有成功上传任何文件")
                return 1
        else:
            logger.warning("未配置 OSS，跳过上传（环境变量: OSS_ENDPOINT, OSS_BUCKET, OSS_KEY, OSS_SECRET）")

    return 0


def _has_oss_config() -> bool:
    return bool(os.getenv("OSS_ENDPOINT") and os.getenv("OSS_BUCKET") and os.getenv("OSS_KEY") and os.getenv("OSS_SECRET"))


if __name__ == "__main__":
    sys.exit(main())
