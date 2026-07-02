# encoding: utf-8
"""設定值持久化（比照原 SketchUp 版 defaults.rb 的 save/load），
以使用者家目錄下的 JSON 檔保存每個圖案的參數與目前選用的圖案。
"""

import json
import os

SETTINGS_PATH = os.path.join(os.path.expanduser('~'), '.floora_rhino_settings.json')


def _load_all():
    if not os.path.exists(SETTINGS_PATH):
        return {}
    try:
        with open(SETTINGS_PATH, 'r') as f:
            return json.load(f)
    except Exception:
        return {}


def _save_all(data):
    try:
        with open(SETTINGS_PATH, 'w') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
    except Exception:
        pass


def load_pattern_opts(pattern_id, fallback):
    data = _load_all()
    saved = data.get('patterns', {}).get(pattern_id)
    merged = dict(fallback)
    if saved:
        merged.update(saved)
    return merged


def save_pattern_opts(pattern_id, opts):
    data = _load_all()
    patterns = data.setdefault('patterns', {})
    patterns[pattern_id] = opts
    data['current_pattern'] = pattern_id
    _save_all(data)


def load_current_pattern(default_id):
    data = _load_all()
    return data.get('current_pattern', default_id)


def reset_all():
    _save_all({})
