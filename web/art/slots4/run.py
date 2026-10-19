"""Runs ../gen.py for slots4 prompts with up to 3 jobs at a time and retries on 429.
Usage (from this folder): BFL_API_KEY=... python3 run.py name [name ...]   (name may be name@seed to override the seed)"""
import json, os, sys, time, urllib.error
from concurrent.futures import ThreadPoolExecutor
os.environ.setdefault('PROMPTS', 'prompts.json'); os.environ.setdefault('OUT', 'raw')
sys.path.insert(0, '..')
import gen

ITEMS = json.load(open(os.environ['PROMPTS']))['items']

def one(spec):
    name, _, seed = spec.partition('@')
    item = ITEMS[name]
    seed = int(seed) if seed else item.get('seed')
    out = f'{name}-s{seed}' if seed is not None else name
    for attempt in range(8):
        try:
            gen.generate(out, item['prompt'], ultra=item.get('ultra', False), aspect=item.get('aspect', '1:1'),
                         w=item.get('w', 1024), h=item.get('h', 1024), seed=seed)
            return f'ok {out}'
        except urllib.error.HTTPError as e:
            if e.code == 429:
                time.sleep(10 + attempt * 5); continue
            return f'FAIL {out} {e.code} {e.read()[:200]}'
        except BaseException as e:
            return f'FAIL {out} {e}'
    return f'FAIL {out} 429 x8'

with ThreadPoolExecutor(3) as ex:
    for r in ex.map(one, sys.argv[1:]):
        print(r, flush=True)
