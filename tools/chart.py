"""Стовпчикові діаграми цикломатичної складності за файлами metrics-*.csv.

python tools/chart.py before docs/metrics-before.csv docs/cc-before.png
python tools/chart.py compare docs/metrics-before.csv src docs/cc-compare.png

Для порівняння складність «після» рахується з коду для всіх методів (у CSV
потрапляють лише методи довші за 5 рядків), а перенесені й перейменовані
методи зіставляються за таблицею RENAMES.
"""
import csv
import sys

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

THRESHOLD = 8
TOP = 15
RENAMES = {
    "SchedulePlanner.normPhone": "SmsNotifier.NormalizePhone",
    "SchedulePlanner.formatSms": "SmsNotifier.FormatSms",
    "SchedulePlanner.SendSms": "SmsNotifier.SendSms",
}


def load(path):
    with open(path, encoding="utf-8") as fh:
        return [r for r in csv.DictReader(fh, delimiter=";")]


def label(row):
    return f'{row["Class"]}.{row["Method"]}'


def before(src, out):
    rows = sorted(load(src), key=lambda r: -int(r["CC"]))[:TOP]
    names = [label(r) for r in rows]
    values = [int(r["CC"]) for r in rows]
    fig, ax = plt.subplots(figsize=(10, 5.5))
    ax.bar(range(len(rows)), values, color="#4c72b0")
    ax.axhline(THRESHOLD, color="#c44e52", linestyle="--", linewidth=1.5, label=f"поріг CC = {THRESHOLD}")
    ax.set_xticks(range(len(rows)), names, rotation=60, ha="right", fontsize=9)
    ax.set_xlabel("Метод")
    ax.set_ylabel("Цикломатична складність")
    for x, v in enumerate(values):
        ax.text(x, v + 0.3, str(v), ha="center", fontsize=8)
    ax.legend()
    fig.tight_layout()
    fig.savefig(out, dpi=150)


def measure_all(src):
    import glob
    import os
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import metrics
    rows = []
    for f in sorted(glob.glob(os.path.join(src, "*.cs"))):
        rows += metrics.parse(f, set())[0]
    return {f'{r["Class"]}.{r["Method"]}': r["CC"] for r in rows}


def compare(src_before, src_dir, out):
    old = sorted(load(src_before), key=lambda r: -int(r["CC"]))[:TOP]
    new = measure_all(src_dir)
    names = [label(r) for r in old]
    before_cc = [int(r["CC"]) for r in old]
    after_cc = [new.get(RENAMES.get(n, n), 0) for n in names]
    fig, ax = plt.subplots(figsize=(10, 5.5))
    xs = range(len(names))
    ax.bar([x - 0.2 for x in xs], before_cc, width=0.4, color="#4c72b0", label="до рефакторингу")
    ax.bar([x + 0.2 for x in xs], after_cc, width=0.4, color="#55a868", label="після рефакторингу")
    ax.axhline(THRESHOLD, color="#c44e52", linestyle="--", linewidth=1.5, label=f"поріг CC = {THRESHOLD}")
    ticks = [RENAMES[n] if n in RENAMES else n + (" *" if n not in new else "") for n in names]
    ax.set_xticks(list(xs), ticks, rotation=60, ha="right", fontsize=9)
    ax.set_xlabel("Метод (перенесені підписано новим іменем; * - методу не стало, показано 0)")
    ax.set_ylabel("Цикломатична складність")
    ax.legend()
    fig.tight_layout()
    fig.savefig(out, dpi=150)


if __name__ == "__main__":
    if sys.argv[1] == "before":
        before(sys.argv[2], sys.argv[3])
    else:
        compare(sys.argv[2], sys.argv[3], sys.argv[4])
