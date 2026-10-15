"""Generates Gilded Savanna artwork with Flux (BFL). Usage: python3 gen.py <name> [--ultra] [--aspect 16:9] [--w 1024 --h 1024]
Prompts live in prompts.json; output goes to raw/<name>.jpg. Reads BFL_API_KEY from the environment."""
import json, os, sys, time, urllib.request

KEY = os.environ["BFL_API_KEY"]
STYLE = json.load(open("prompts.json"))["style"]

def post(url, body):
    req = urllib.request.Request(url, data=json.dumps(body).encode(), headers={"x-key": KEY, "content-type": "application/json"})
    return json.load(urllib.request.urlopen(req, timeout=60))

def get(url):
    return json.load(urllib.request.urlopen(urllib.request.Request(url, headers={"x-key": KEY}), timeout=60))

def generate(name, prompt, ultra=False, aspect="1:1", w=1024, h=1024, seed=None):
    body = {"prompt": f"{prompt}. {STYLE}", "output_format": "png"}
    if seed is not None:
        body["seed"] = seed
    if ultra:
        body.update({"aspect_ratio": aspect, "raw": False})
        job = post("https://api.bfl.ai/v1/flux-pro-1.1-ultra", body)
    else:
        body.update({"width": w, "height": h})
        job = post("https://api.bfl.ai/v1/flux-pro-1.1", body)
    for _ in range(120):
        time.sleep(2)
        r = get(job["polling_url"])
        if r["status"] == "Ready":
            os.makedirs("raw", exist_ok=True)
            urllib.request.urlretrieve(r["result"]["sample"], f"raw/{name}.png")
            return f"raw/{name}.png"
        if r["status"] not in ("Pending", "Processing"):
            raise SystemExit(f"{name}: {r['status']} {r}")
    raise SystemExit(f"{name}: timed out")

if __name__ == "__main__":
    prompts = json.load(open("prompts.json"))["items"]
    a = sys.argv[1:]
    name = a[0]
    item = prompts[name]
    print(generate(name, item["prompt"], ultra=item.get("ultra", False), aspect=item.get("aspect", "1:1"),
                   w=item.get("w", 1024), h=item.get("h", 1024), seed=item.get("seed")))
