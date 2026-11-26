#!/usr/bin/env python3
"""Generator of victoria_trace.json: sends one OTLP/HTTP protobuf trace to VictoriaTraces and saves its Jaeger answer.

The trace looks like the authorization of a client socket: a root of the meta with nested spans, a second service (silo),
attributes of every type, a link to another trace, an error span with a message, an unset span kind.

Usage (stdlib only), against a local store of the version the stack runs (deploy/observability/docker-compose.yaml):
  docker run -d --rm --name vt-fixture --network host victoriametrics/victoria-traces:v0.13.0 \
      -storageDataPath=/tmp/vt -httpListenAddr=127.0.0.1:10528 -insert.indexFlushInterval=1s
  victoria_trace_fixture.py --traces http://127.0.0.1:10528 --out victoria_trace.json
  docker stop vt-fixture
"""
import argparse
import json
import struct
import time
import urllib.error
import urllib.request

TRACE_ID = "4bf92f3577b34da6a3ce929d0e0e4736"


def varint(n):
    out = b""
    while True:
        low, n = n & 0x7F, n >> 7
        if not n:
            return out + bytes([low])
        out += bytes([low | 0x80])


def field(number, payload):
    return varint(number << 3 | 2) + varint(len(payload)) + payload


def number(number_, value):
    return varint(number_ << 3) + varint(value)


def fixed64(number_, value):
    return varint(number_ << 3 | 1) + struct.pack("<Q", value)


def text(number_, value):
    return field(number_, value.encode())


def any_value(value):
    if isinstance(value, bool):
        return number(2, 1 if value else 0)
    if isinstance(value, int):
        return number(3, value)
    if isinstance(value, float):
        return varint(4 << 3 | 1) + struct.pack("<d", value)
    return text(1, value)


def attribute(key, value):
    return text(1, key) + field(2, any_value(value))


def span(span_id, parent, name, kind, start, end, attributes=(), status=None, link=None):
    body = field(1, bytes.fromhex(TRACE_ID)) + field(2, bytes.fromhex(span_id))
    if parent:
        body += field(4, bytes.fromhex(parent))
    body += text(5, name)
    if kind:
        body += number(6, kind)
    body += fixed64(7, start) + fixed64(8, end)
    body += b"".join(field(9, attribute(k, v)) for k, v in attributes)
    if link:
        body += field(13, field(1, bytes.fromhex(link[0])) + field(2, bytes.fromhex(link[1])))
    if status:
        body += field(15, text(2, status[1]) + number(3, status[0]))
    return body


def resource_spans(service, scope, spans):
    resource = b"".join(field(1, attribute(k, v)) for k, v in [
        ("service.namespace", "fixture"), ("service.name", service), ("deployment.environment", "test")])
    return field(1, field(1, resource) + field(2, field(1, text(1, scope)) + b"".join(field(2, s) for s in spans)))


def build(base):
    ms = 1_000_000
    meta = resource_spans("meta", "MetaGateway.Connection", [
        span("30751cd7aa06e015", None, "Meta connect", 2, base, base + 550 * ms,
             link=("0a9d3fa7fc86d24739d6dad448a47faa", "db9ce5ba4546b546")),
        span("30cb93fa92590191", "30751cd7aa06e015", "Meta read request", 1, base + 2 * ms, base + 20 * ms,
             attributes=[("meta.request.bytes", 512), ("meta.request.gzip", True), ("meta.request.ratio", 0.25)]),
        span("39ca646731c14b67", "30751cd7aa06e015", "Meta auth", 1, base + 21 * ms, base + 340 * ms,
             attributes=[("meta.auth.guest", "new"), ("meta.auth.status", "Ok")]),
        span("5d1a2b3c4d5e6f70", "30751cd7aa06e015", "Meta projections", 0, base + 345 * ms, base + 540 * ms,
             status=(2, "projection failed")),
    ])
    silo = resource_spans("silo", "Orleans.Runtime", [
        span("6a7b8c9d0e1f2a3b", "39ca646731c14b67", "Transaction.Process", 3, base + 30 * ms, base + 300 * ms),
        span("7b8c9d0e1f2a3b4c", "5d1a2b3c4d5e6f70", "Transaction.Process", 3, base + 350 * ms, base + 500 * ms,
             status=(1, "")),
    ])
    return meta + silo


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--traces", required=True, help="VictoriaTraces base URL")
    parser.add_argument("--out", required=True, help="the fixture file")
    args = parser.parse_args()

    base = (time.time_ns() // 1_000_000 - 60_000) * 1_000_000
    request = urllib.request.Request(f"{args.traces}/insert/opentelemetry/v1/traces", data=build(base),
                                     headers={"Content-Type": "application/x-protobuf"})
    with urllib.request.urlopen(request, timeout=10) as response:
        print(f"POST -> {response.status}")

    url = f"{args.traces}/select/jaeger/api/traces/{TRACE_ID}"
    deadline = time.time() + 60
    while time.time() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=10) as response:
                answer = json.loads(response.read())
            if answer["data"] and len(answer["data"][0]["spans"]) == 6:
                with open(args.out, "w") as out:
                    json.dump(answer, out, indent=1)
                    out.write("\n")
                print(f"saved {args.out}, trace start {base} ns")
                return
        except urllib.error.HTTPError as error:
            if error.code != 404:
                raise
        time.sleep(2)
    raise SystemExit(f"the trace is not at {url} in 60 s")


if __name__ == "__main__":
    main()
