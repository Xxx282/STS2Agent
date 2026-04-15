'''
Author: Mendax
Date: 2026-04-14 21:38:54
LastEditors: Mendax
LastEditTime: 2026-04-14 22:09:58
Description: 
FilePath: \soldier\test_api.py
'''
import requests
import json

BASE_URL = "http://localhost:8888"

def test_endpoint(path, description):
    try:
        response = requests.get(f"{BASE_URL}{path}", timeout=5)
        print(f"\n{'='*60}")
        print(f"测试: {description}")
        print(f"端点: {path}")
        print(f"状态码: {response.status_code}")
        if response.status_code == 200:
            data = response.json()
            print(f"数据:\n{json.dumps(data, ensure_ascii=False, indent=2)}")
        else:
            print(f"错误: {response.text}")
    except requests.exceptions.ConnectionError:
        print(f"连接失败! 请确保游戏正在运行且Mod已启用")
    except Exception as e:
        print(f"异常: {e}")

print("STS2Agent API 测试工具")
print("=" * 60)

# 依次测试所有端点
test_endpoint("/api/health", "健康检查")
test_endpoint("/api/state", "完整游戏状态")
test_endpoint("/api/player", "玩家状态")
test_endpoint("/api/enemies", "敌人状态")
test_endpoint("/api/combat", "战斗状态")