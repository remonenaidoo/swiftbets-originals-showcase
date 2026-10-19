"""Renders several prompts.json items, 3 at a time, retrying on BFL 429. Usage: python3 batch.py name[:seed] ..."""
import json, os, sys, time, urllib.error
from concurrent.futures import ThreadPoolExecutor
os.environ.setdefault('PROMPTS', 'prompts.json'); os.environ.setdefault('OUT', 'raw')
sys.path.insert(0, '..')
import gen

items = json.load(open(os.environ['PROMPTS']))['items']

def run(arg):
    name, _, seed = arg.partition(':')
    it = items[name]
    out = name + (f'-s{seed}' if seed else '')
    for attempt in range(12):
        try:
            p = gen.generate(out, it['prompt'], ultra=it.get('ultra', False), aspect=it.get('aspect', '1:1'),
                             w=it.get('w', 1024), h=it.get('h', 1024), seed=int(seed) if seed else it.get('seed'))
            return p
        except urllib.error.HTTPError as e:
            if e.code == 429:
                time.sleep(5 + attempt * 3); continue
            return f'{out}: HTTP {e.code}'
        except Exception as e:
            time.sleep(3)
            last = e
    return f'{out}: failed {last if "last" in dir() else ""}'

with ThreadPoolExecutor(3) as ex:
    for r in ex.map(run, sys.argv[1:]):
        print(r, flush=True)
