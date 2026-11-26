#!/usr/bin/env python3
"""Turns raw memory snapshots (of the Coolify hosts or of a local run) into markdown tables.

Input: a folder with `<host>.txt` (free, docker stats, labels, smaps of every process) and `<host>.cgroup.txt`
(name, memory.current, memory.swap.current, anon, file, inactive_file of every container cgroup), as written by
`snapshot.sh`; a local run (`local.sh`) names the pair after its variant instead of a host.

    prod_report.py <folder>                 one table per host (or variant): project, part, the columns below
    prod_report.py --summary <folder>...    one row per host and project: cost of the parts, PSS and stats of the total

Columns, MiB:
- stats: what `docker stats` shows (memory.current - inactive_file);
- anon: private memory of the container in RAM (heaps, JIT code);
- swap: its pages pushed to swap (`docker stats` does not show them);
- cost: anon + swap, what the container really costs the host besides the shared page cache;
- pss: the proportional set size of its processes (smaps_rollup): resident pages, the shared ones split between
  the processes that map them; next to cost, RSS would count the shared pages twice.
"""

import sys
from collections import defaultdict
from pathlib import Path

MIB = 1024 * 1024
KIB = 1024

DOTNET_ROLES = {"silo", "coordinator", "meta", "game", "console", "frontend", "resource-service", "backend"}
OBSERVABILITY = {"otlp", "victorialogs", "victoriatraces"}
COLUMNS = ("stats", "anon", "swap", "pss")

# The parts of a project in the summary, by the kind of a container (kind_of).
SUMMARY_PARTS = (
    (".NET", lambda group: group == "dotnet"),
    ("telemetry", lambda group: group in ("aspire-dashboard", "observability")),
    ("Postgres + pgbouncer + web", lambda group: group in ("postgres", "pgbouncer", "web")),
)


def read_sections(path):
    sections = defaultdict(list)
    current = "head"

    for line in path.read_text().splitlines():
        if line.startswith("#"):
            current = line[1:].split()[0]
            if current == "host":
                sections["host"].append(line)
            continue
        sections[current].append(line)

    return sections


def read_labels(lines):
    labels = {}

    for line in lines:
        parts = line.replace("\\t", "\t").split("\t")
        name = parts[0].lstrip("/")
        labels[name] = {"project": parts[2] or None, "service": parts[3] or None, "image": parts[5]}

    return labels


def read_pss(lines):
    """PSS of every container in bytes: the sum over its processes (lines `/name pid rss pss anon`, KiB)."""
    pss = defaultdict(int)

    for line in lines:
        parts = line.split()
        if len(parts) != 5:
            raise ValueError(f"a smaps line without its values (the snapshot could not read the process): {line!r}")
        pss[parts[0].lstrip("/")] += int(parts[3]) * KIB

    return pss


def read_cgroups(path):
    rows = {}

    for line in path.read_text().splitlines():
        parts = line.split("\t")
        name = parts[0].lstrip("/")
        current, swap, anon, file, inactive = (int(v) if v else 0 for v in parts[1:6])
        rows[name] = {"stats": current - inactive, "anon": anon, "swap": swap, "file": file}

    return rows


def kind_of(name, label):
    image = label["image"]
    role = name.split("-")[0] if "-" in name else name

    if name.startswith("aspire-dashboard"):
        return "aspire-dashboard"
    if name.startswith("coolify"):
        return "coolify"
    if role in OBSERVABILITY:
        return "observability"
    if "postgres" in image:
        return "postgres"
    if role in DOTNET_ROLES:
        return "dotnet:" + role
    if "pgbouncer" in image:
        return "pgbouncer"
    if "nginx" in image or role in ("web", "cdn"):
        return "web"
    return "other:" + role


def empty_cell():
    return {"stats": 0, "anon": 0, "swap": 0, "pss": 0, "n": 0}


def read_host(folder, host):
    """The cells of one snapshot: project -> group -> stats / anon / swap / pss in bytes and the container count."""
    sections = read_sections(folder / f"{host}.txt")
    labels = read_labels(sections["labels"])
    pss = read_pss(sections["pss"])
    cgroups = read_cgroups(folder / f"{host}.cgroup.txt")
    projects = defaultdict(lambda: defaultdict(empty_cell))

    for name, cg in cgroups.items():
        if name not in pss:
            raise ValueError(f"{host}: container {name} has a cgroup but no smaps lines")

        label = labels.get(name, {"project": None, "service": None, "image": "?"})
        project = label["project"] or ("coolify" if name.startswith("coolify") else "(other)")
        kind = kind_of(name, label)
        group = "dotnet" if kind.startswith("dotnet:") else kind
        cell = projects[project][group]
        cell["stats"] += cg["stats"]
        cell["anon"] += cg["anon"]
        cell["swap"] += cg["swap"]
        cell["pss"] += pss[name]
        cell["n"] += 1

    return sections, projects


def hosts_of(folder):
    hosts = sorted(f.name[:-len(".txt")] for f in folder.glob("*.txt") if not f.name.endswith(".cgroup.txt"))
    if not hosts:
        raise ValueError(f"no snapshot in {folder}")
    return hosts


def mib(value):
    return f"{value / MIB:.0f}"


def row(cells, bold=False):
    text = [f"**{c}**" if bold and c != "" else c for c in cells]
    return "| " + " | ".join(text) + " |"


def values(c):
    return [mib(c["stats"]), mib(c["anon"]), mib(c["swap"]), mib(c["anon"] + c["swap"]), mib(c["pss"])]


def add(total, c):
    for key in COLUMNS:
        total[key] += c[key]
    total["n"] += c["n"]


def report(folder):
    out = []

    for host in hosts_of(folder):
        sections, projects = read_host(folder, host)

        out.append(f"### {host}")
        out.append("")
        out.append("```")
        out.extend(sections["host"])
        out.extend(sections["head"][:3])
        out.append("```")
        out.append("")
        out.append("| project | part | containers | stats | anon | swap | cost | pss |")
        out.append("|---|---|---|---|---|---|---|---|")
        host_total = empty_cell()

        for project in sorted(projects):
            total = empty_cell()

            for group in sorted(projects[project]):
                c = projects[project][group]
                out.append(row([project, group, str(c["n"])] + values(c)))
                add(total, c)

            out.append(row([project, "total", ""] + values(total), bold=True))
            add(host_total, total)

        out.append(row(["all", "", ""] + values(host_total), bold=True))
        out.append("")

    return "\n".join(out)


def summary(folders):
    head = ["snapshot", "project"] + [f"{name}, cost" for name, _ in SUMMARY_PARTS] + \
           ["other, cost", "total, cost", "total, pss", "total, stats"]
    out = [row(head), "|" + "---|" * len(head)]

    for folder in folders:
        for host in hosts_of(folder):
            _, projects = read_host(folder, host)

            for project in sorted(projects):
                parts = [empty_cell() for _ in SUMMARY_PARTS]
                other, total = empty_cell(), empty_cell()

                for group, c in projects[project].items():
                    index = next((i for i, (_, test) in enumerate(SUMMARY_PARTS) if test(group)), None)
                    add(other if index is None else parts[index], c)
                    add(total, c)

                cost = [mib(p["anon"] + p["swap"]) if p["n"] else "" for p in parts + [other]]
                out.append(row([f"{folder.name}/{host}", project] + cost +
                               [mib(total["anon"] + total["swap"]), mib(total["pss"]), mib(total["stats"])]))

    return "\n".join(out)


def main(argv):
    if len(argv) >= 2 and argv[0] == "--summary":
        return summary([Path(a) for a in argv[1:]])
    if len(argv) == 1 and not argv[0].startswith("-"):
        return report(Path(argv[0]))
    raise SystemExit("usage: prod_report.py <folder> | prod_report.py --summary <folder>...")


if __name__ == "__main__":
    print(main(sys.argv[1:]))
