# The tables of prod_report.py on the committed snapshots and on small made-up ones:
#   python3 -m unittest discover -s backend/measurements/tools/tests
import tempfile
import unittest
from pathlib import Path

from support import MONOLITH, PROD_BEFORE  # first: it puts the tools on the path

import prod_report  # noqa: E402

MIB = 1024 * 1024


def write_snapshot(folder, name, containers):
    """containers: (name, project, image, [pss KiB of each process], anon MiB)."""
    labels = [f"/{c}\\trun\\t{project}\\t\\t\\t{image}\\t2026-11-25T00:00:00Z\\t1" for c, project, image, _, _ in containers]
    pss = [f"/{c} {100 + i} 1 {value} 1" for c, _, _, values, _ in containers for i, value in enumerate(values)]
    text = ["#host test 2026-11-25T00:05:00Z nproc=6 uptime=1", "Mem: 1 2 3", "#stats", "#labels"] + labels + ["#pss"] + pss
    (folder / f"{name}.txt").write_text("\n".join(text) + "\n")
    cgroups = [f"/{c}\t{anon * MIB}\t0\t{anon * MIB}\t0\t0" for c, _, _, _, anon in containers]
    (folder / f"{name}.cgroup.txt").write_text("\n".join(cgroups) + "\n")


class ProdBefore(unittest.TestCase):
    def setUp(self):
        self.text = prod_report.report(PROD_BEFORE)

    def test_a_table_per_host(self):
        self.assertEqual(["### firstvds", "### ovh"], [line for line in self.text.splitlines() if line.startswith("###")])
        self.assertIn("#host vps-bf8f6dbe 2026-11-25T04:39:24Z nproc=6 uptime=12031920.20", self.text)

    def test_the_parts_of_a_project(self):
        self.assertIn("| pixel-swarm | aspire-dashboard | 1 | 86 | 75 | 0 | 75 | 103 |", self.text)
        self.assertIn("| pixel-swarm | dotnet | 5 | 816 | 542 | 0 | 542 | 926 |", self.text)
        self.assertIn("| pixel-swarm | postgres | 1 | 56 | 10 | 26 | 36 | 53 |", self.text)
        self.assertIn("| **pixel-swarm** | **total** |  | **968** | **636** | **30** | **666** | **1092** |", self.text)

    def test_swap_is_part_of_the_cost(self):
        self.assertIn("| peace-data | aspire-dashboard | 1 | 344 | 302 | 902 | 1205 | 323 |", self.text)

    def test_the_totals_of_the_summary_of_results(self):
        rows = prod_report.summary([PROD_BEFORE]).splitlines()
        cells = [[c.strip() for c in r.strip("|").split("|")] for r in rows[2:]]
        totals = {(c[0], c[1]): c[6] for c in cells}

        self.assertEqual("666", totals[("2026-11-25-prod-before/ovh", "pixel-swarm")])
        self.assertEqual("818", totals[("2026-11-25-prod-before/ovh", "minesleader")])
        self.assertEqual("564", totals[("2026-11-25-prod-before/ovh", "autobattler")])
        self.assertEqual("343", totals[("2026-11-25-prod-before/ovh", "coolify")])
        self.assertEqual("1931", totals[("2026-11-25-prod-before/firstvds", "peace-data")])
        self.assertEqual("3233", totals[("2026-11-25-prod-before/firstvds", "storytale")])
        self.assertEqual("395", totals[("2026-11-25-prod-before/firstvds", "coolify")])


class Monolith(unittest.TestCase):
    def test_one_dotnet_container(self):
        self.assertIn("| pixel-swarm | dotnet | 1 | 172 | 99 | 0 | 99 | 206 |", prod_report.report(MONOLITH))

    def test_the_stack_is_observability_not_web(self):
        self.assertIn("| observability | observability | 3 | 14 | 11 | 0 | 11 | 34 |", prod_report.report(MONOLITH))

    def test_the_summary_splits_dotnet_telemetry_and_data(self):
        row = "| 2026-11-25-monolith/ovh | pixel-swarm | 99 | 95 | 59 |  | 252 | 411 | 332 |"
        self.assertIn(row, prod_report.summary([MONOLITH]))


class Kinds(unittest.TestCase):
    def kind(self, name, image="some/image"):
        return prod_report.kind_of(name, {"image": image})

    def test_by_name_and_image(self):
        self.assertEqual("aspire-dashboard", self.kind("aspire-dashboard-abc-1"))
        self.assertEqual("coolify", self.kind("coolify-redis", "redis:7-alpine"))
        self.assertEqual("observability", self.kind("otlp-abc-1", "nginx:1.30.8-alpine-slim"))
        self.assertEqual("observability", self.kind("victorialogs-measure-run"))
        self.assertEqual("postgres", self.kind("vjgu3p4ptf5bxxs7di7f8sw1", "postgres:18-alpine"))
        self.assertEqual("dotnet:backend", self.kind("backend-measure-run", "backend-measurements/backend:local"))
        self.assertEqual("dotnet:silo", self.kind("silo-abc-1"))
        self.assertEqual("pgbouncer", self.kind("pgbouncer-abc-1", "edoburu/pgbouncer:v1.26.1-p0"))
        self.assertEqual("web", self.kind("web-abc", "nginx:1.27-alpine"))
        self.assertEqual("other:minio", self.kind("minio-abc", "minio/minio"))


class MadeUp(unittest.TestCase):
    def setUp(self):
        self.folder = Path(tempfile.mkdtemp())

    def test_a_local_variant_is_grouped_like_a_host(self):
        write_snapshot(self.folder, "monolith-workstation-aspire", [
            ("backend-measure-run", "pixel-swarm", "backend-measurements/backend:local", [2048, 1024], 100),
            ("aspire-dashboard-measure-run", "pixel-swarm", "mcr.microsoft.com/dotnet/aspire-dashboard:13.6", [1024], 50),
            ("postgres-measure-run", "pixel-swarm", "postgres:18-alpine", [512, 512], 10),
        ])
        (self.folder / "conditions.info").write_text("source x\n")

        text = prod_report.report(self.folder)

        self.assertIn("### monolith-workstation-aspire", text)
        self.assertIn("| pixel-swarm | dotnet | 1 | 100 | 100 | 0 | 100 | 3 |", text)
        self.assertIn("| monolith-workstation-aspire | pixel-swarm | 100 | 50 | 10 |  | 160 | 5 | 160 |",
                      prod_report.summary([self.folder]).replace(f"{self.folder.name}/", ""))

    def test_pss_is_the_sum_of_the_processes(self):
        pss = prod_report.read_pss(["/a 1 10 1024 5", "/a 2 10 2048 5", "/b 3 10 512 5"])

        self.assertEqual({"a": 3 * 1024 * 1024, "b": 512 * 1024}, dict(pss))

    def test_a_process_without_values_fails(self):
        with self.assertRaisesRegex(ValueError, "without its values"):
            prod_report.read_pss(["/a 1"])

    def test_a_container_without_smaps_fails(self):
        write_snapshot(self.folder, "h", [("a-1", "p", "x", [], 1)])

        with self.assertRaisesRegex(ValueError, "no smaps lines"):
            prod_report.report(self.folder)

    def test_an_empty_folder_fails(self):
        with self.assertRaisesRegex(ValueError, "no snapshot"):
            prod_report.report(self.folder)

    def test_usage(self):
        with self.assertRaises(SystemExit):
            prod_report.main(["--summary"])


if __name__ == "__main__":
    unittest.main()
