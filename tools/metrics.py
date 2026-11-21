"""Метрики методів успадкованого коду за методикою СР 2.

Запуск: python tools/metrics.py src docs/metrics-before.csv

LOC     - логічні рядки тіла методу: без сигнатури, порожніх рядків, рядків
          лише з дужкою і рядків лише з коментарем.
CC      - 1 + if (зокрема else if) + тернарний ?: + for/foreach/while/do
          + кожен case + кожне плече switch-виразу, крім _ + && + || + catch
          + ?? + ?.
Depth   - глибина вкладеності: тіло методу - рівень 1, кожен вкладений
          керуючий блок додає 1. Рахується за відступом найглибшого рядка
          (код відформатовано по 4 пробіли), рядки-продовження виразів
          не враховуються; секція case усередині switch теж дає рівень.
Params  - кількість параметрів.
FanOut  - кількість інших власних типів проєкту, згаданих у класі.

Друкує всі методи, а в CSV записує ті, що довші за 5 рядків, за спаданням CC.
Окремо шукає клони: однакові послідовності з 6 і більше непорожніх рядків.
"""
import csv
import glob
import os
import re
import sys

CONTROL = re.compile(r"^(if|else if|else|for|foreach|while|do|switch|try|catch|finally|case|default)\b")
TYPE_DECL = re.compile(r"^\s*(public|internal)?\s*(static\s+|sealed\s+|abstract\s+)*(class|record|struct|enum|interface)\s+(\w+)")
METHOD = re.compile(
    r"^\s+(public|private|internal|protected)\s+(static\s+|virtual\s+|override\s+|async\s+)*"
    r"(?!class\b|record\b|enum\b|interface\b|const\b|readonly\b)"
    r"([\w<>\[\]?,. ]+?\s+)?(\w+)\s*\(")


def strip(line):
    line = re.sub(r'@?"(\\.|[^"\\])*"', '""', line)
    line = re.sub(r"'(\\.|[^'\\])'", "''", line)
    return line.split("//")[0].rstrip()


def is_code(line):
    s = strip(line).strip()
    return bool(s) and s not in ("{", "}", "};", "});", ")", ");")


def complexity(text):
    cc = 1
    cc += len(re.findall(r"\bif\b", text))
    cc += len(re.findall(r"\b(for|foreach|while)\b", text))
    cc += len(re.findall(r"\bcase\b", text))
    cc += len(re.findall(r"\bcatch\b", text))
    cc += len(re.findall(r"&&|\|\|", text))
    cc += len(re.findall(r"\?\?", text))
    cc += len(re.findall(r"\?\.", text))
    cc += len(re.findall(r"\s\?\s", text))
    cc += len(re.findall(r"^\s+(?!_\s*=>)[\w.\"]+\s*=>", text, re.M)) if re.search(r"\bswitch\s*$", text, re.M) else 0
    return cc


def depth(body):
    lines = [strip(l) for l in body]
    code_lines = [l for l in lines if l.strip()]
    if not code_lines:
        return 1
    base = None
    for l in code_lines:
        if l.strip() not in ("{", "}"):
            base = len(l) - len(l.lstrip())
            break
    if base is None:
        return 1
    best, prev = 1, ""
    for l in code_lines:
        s = l.strip()
        if s in ("{", "}", "};"):
            prev = s
            continue
        cont = prev and not (prev.endswith((";", "{", "}", ":")) or CONTROL.match(prev))
        prev = s
        if cont:
            continue
        indent = len(l) - len(l.lstrip())
        best = max(best, 1 + (indent - base) // 4)
    return best


def parse(path, own_types):
    lines = open(path, encoding="utf-8-sig").read().split("\n")
    methods = []
    cls = None
    i = 0
    while i < len(lines):
        t = TYPE_DECL.match(lines[i])
        if t:
            cls = t.group(4)
        m = METHOD.match(lines[i])
        if not m or (m.group(3) is None and m.group(4) != cls) or "=" in strip(lines[i]).split("(")[0]:
            i += 1
            continue
        name = m.group(4)
        sig = strip(lines[i])
        j = i
        while sig.count("(") > sig.count(")"):
            j += 1
            sig += strip(lines[j])
        open_at, level = sig.find("("), 0
        for pos in range(open_at, len(sig)):
            level += {"(": 1, ")": -1}.get(sig[pos], 0)
            if level == 0:
                break
        inner = sig[open_at + 1:pos].strip()
        params = 0 if not inner else len(inner.split(","))
        if sig.rstrip().endswith(";") or ("=>" in sig and "{" not in sig):
            k = j
            while not strip(lines[k]).rstrip().endswith(";"):
                k += 1
            body = lines[j:k + 1] if "=>" in sig else []
            end = k
        else:
            level, seen, k = 0, False, j
            while k < len(lines):
                c = strip(lines[k])
                level += c.count("{") - c.count("}")
                seen = seen or "{" in c
                if seen and level == 0:
                    break
                k += 1
            # метод в один рядок: { return x; } на рядку сигнатури
            body = lines[j + 1:k + 1] if k > j else [sig[sig.find("{") + 1:sig.rfind("}")]]
            end = k
        text = "\n".join(strip(l) for l in body)
        methods.append({
            "Class": cls, "Method": name, "File": os.path.basename(path), "Line": i + 1,
            "LOC": sum(1 for l in body if is_code(l)), "CC": complexity(text),
            "Depth": depth(body), "Params": params,
        })
        i = end + 1
    return methods, lines


def main(src, out_csv=None):
    files = sorted(f for f in glob.glob(os.path.join(src, "**", "*.cs"), recursive=True)
                   if os.sep + "obj" + os.sep not in f and os.sep + "bin" + os.sep not in f)
    own = set()
    texts = {}
    for f in files:
        text = open(f, encoding="utf-8-sig").read()
        texts[f] = text
        own.update(m.group(4) for m in map(TYPE_DECL.match, text.split("\n")) if m)
    rows, physical, logical, per_file = [], 0, 0, []
    class_text = {}
    for f in files:
        ms, lines = parse(f, own)
        rows += ms
        phys = len(lines) - (1 if lines and lines[-1] == "" else 0)
        log = sum(1 for l in lines if is_code(l) and not strip(l).strip().startswith("using "))
        physical += phys
        logical += log
        cur = None
        for l in lines:
            t = TYPE_DECL.match(l)
            if t:
                cur = t.group(4)
            if cur:
                class_text.setdefault(cur, []).append(strip(l))
        per_file.append((os.path.basename(f), phys, log))
    fan = {}
    for c, ls in class_text.items():
        body = "\n".join(ls)
        fan[c] = sum(1 for t in own if t != c and re.search(r"\b" + t + r"\b", body))
    for r in rows:
        r["FanOut"] = fan.get(r["Class"], 0)
    for name, p, l in per_file:
        print(f"{name:24s} physical={p:4d} logical={l:4d}")
    print(f"TOTAL physical={physical} logical={logical} files={len(files)} types={len(own)} methods={len(rows)}")
    rows.sort(key=lambda r: (-r["CC"], -r["LOC"], r["Class"], r["Method"]))
    for r in rows:
        print(f'{r["Class"]:18s} {r["Method"]:22s} LOC={r["LOC"]:3d} CC={r["CC"]:2d} '
              f'Depth={r["Depth"]} Params={r["Params"]} FanOut={r["FanOut"]}  {r["File"]}:{r["Line"]}')
    big = [r for r in rows if r["LOC"] > 5]
    ccs = [r["CC"] for r in rows]
    print(f"methods>5 lines={len(big)} maxCC={max(ccs)} avgCC={sum(ccs) / len(ccs):.2f} "
          f"maxLOC={max(r['LOC'] for r in rows)} maxDepth={max(r['Depth'] for r in rows)} "
          f"maxFanOut={max(fan.values())}")
    clones(files)
    if out_csv:
        with open(out_csv, "w", encoding="utf-8", newline="") as fh:
            w = csv.writer(fh, delimiter=";")
            w.writerow(["Class", "Method", "LOC", "CC", "Depth", "Params", "FanOut", "File"])
            for r in big:
                w.writerow([r["Class"], r["Method"], r["LOC"], r["CC"], r["Depth"], r["Params"], r["FanOut"], r["File"]])


def clones(files, size=6):
    seqs = []
    for f in files:
        for n, l in enumerate(open(f, encoding="utf-8-sig").read().split("\n"), 1):
            s = re.sub(r"\s+", " ", strip(l)).strip()
            if s and s not in ("{", "}"):
                seqs.append((os.path.basename(f), n, s))
    seen = {}
    for k in range(len(seqs) - size + 1):
        window = seqs[k:k + size]
        if len({w[0] for w in window}) > 1:
            continue
        key = "\n".join(w[2] for w in window)
        seen.setdefault(key, []).append(f"{window[0][0]}:{window[0][1]}")
    groups = {}
    for key, places in seen.items():
        if len(places) > 1:
            groups[tuple(places)] = key
    merged = []
    for places in sorted(groups):
        if merged and all(any(abs(int(p.split(":")[1]) - int(q.split(":")[1])) <= 2 and p.split(":")[0] == q.split(":")[0]
                              for q in merged[-1]) for p in places):
            continue
        merged.append(places)
    print(f"clones (>= {size} lines): {len(merged)}")
    for places in merged:
        print("  " + ", ".join(places))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
