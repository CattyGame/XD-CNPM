import os
import base64
import hashlib
import hmac
import uuid
import urllib.request
import urllib.error
from datetime import datetime, timedelta, timezone
from email.utils import formatdate
from urllib.parse import urlencode

ACCOUNT = "devstoreaccount1"
KEY = base64.b64decode(
    "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="
)
ENDPOINT = os.environ.get("AURA_STORAGE_TEST_ENDPOINT", "http://azurite:10000")
VERSION = "2023-11-03"
PAYLOAD = b"AURA SCRUM-300 private storage verification"
CONTAINERS = ["aura-originals", "aura-annotations", "aura-reports"]

def signature(text):
    return base64.b64encode(
        hmac.new(KEY, text.encode("utf-8"), hashlib.sha256).digest()
    ).decode("ascii")

def send(method, url, headers=None, data=None):
    req = urllib.request.Request(
        url, data=data, headers=headers or {}, method=method
    )
    try:
        with urllib.request.urlopen(req, timeout=15) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()

def owner_request(method, path, data=None):
    headers = {
        "x-ms-date": formatdate(usegmt=True),
        "x-ms-version": VERSION,
    }

    content_length = ""
    content_type = ""

    if data is not None:
        content_length = str(len(data)) if len(data) else ""
        content_type = "application/octet-stream"
        headers["Content-Length"] = str(len(data))
        headers["Content-Type"] = content_type
        headers["x-ms-blob-type"] = "BlockBlob"

    standard_fields = [
        method, "", "", content_length, "", content_type,
        "", "", "", "", "", ""
    ]
    canonical_headers = "".join(
        f"{name}:{headers[name]}\n"
        for name in sorted(headers)
        if name.startswith("x-ms-")
    )
    canonical_resource = f"/{ACCOUNT}{path}"

    text = (
        "\n".join(standard_fields)
        + "\n"
        + canonical_headers
        + canonical_resource
    )
    headers["Authorization"] = f"SharedKey {ACCOUNT}:{signature(text)}"

    return send(method, ENDPOINT + path, headers, data)

def timestamp(value):
    return value.strftime("%Y-%m-%dT%H:%M:%SZ")

def read_sas(container, blob, expired=False):
    now = datetime.now(timezone.utc)
    start = now - timedelta(minutes=5)
    expiry = now + timedelta(seconds=120)

    if expired:
        start = now - timedelta(minutes=10)
        expiry = now - timedelta(minutes=5)

    st = timestamp(start)
    se = timestamp(expiry)
    canonical_resource = f"/blob/{ACCOUNT}/{container}/{blob}"

    fields = [
        "r", st, se, canonical_resource,
        "", "", "https,http", VERSION, "b",
        "", "", "", "", "", "", ""
    ]

    return urlencode({
        "sp": "r",
        "st": st,
        "se": se,
        "spr": "https,http",
        "sv": VERSION,
        "sr": "b",
        "sig": signature("\n".join(fields)),
    })

def require(condition, message):
    if not condition:
        raise RuntimeError(message)

for container in CONTAINERS:
    blob = "verification/" + uuid.uuid4().hex + ".txt"
    path = f"/{ACCOUNT}/{container}/{blob}"
    created = False

    try:
        status, _ = owner_request("PUT", path, PAYLOAD)
        require(status == 201, f"{container}: upload failed HTTP {status}")
        created = True

        status, body = send("GET", ENDPOINT + path)
        require(
            status in (401, 403, 404),
            f"{container}: anonymous GET unexpectedly returned HTTP {status}"
        )
        print(f"PASS: {container}: anonymous GET denied ({status})", flush=True)

        sas_url = ENDPOINT + path + "?" + read_sas(container, blob)

        status, body = send("GET", sas_url)
        require(
            status == 200 and body == PAYLOAD,
            f"{container}: read SAS failed HTTP {status}"
        )
        print(f"PASS: {container}: read SAS works, expiry 120 seconds", flush=True)

        status, _ = send(
            "PUT",
            sas_url,
            {
                "x-ms-version": VERSION,
                "x-ms-blob-type": "BlockBlob",
                "Content-Type": "application/octet-stream",
            },
            b"SHOULD NOT OVERWRITE",
        )
        require(
            status == 403,
            f"{container}: read-only SAS PUT expected 403, got {status}"
        )
        print(f"PASS: {container}: read-only SAS cannot write", flush=True)

        status, body = owner_request("GET", path)
        require(
            status == 200 and body == PAYLOAD,
            f"{container}: original content was not preserved"
        )
        print(f"PASS: {container}: original content preserved", flush=True)

        expired_url = (
            ENDPOINT + path + "?" + read_sas(container, blob, expired=True)
        )
        status, _ = send("GET", expired_url)
        require(
            status == 403,
            f"{container}: expired SAS expected 403, got {status}"
        )
        print(f"PASS: {container}: expired SAS denied", flush=True)

    finally:
        if created:
            status, _ = owner_request("DELETE", path)
            require(
                status == 202,
                f"{container}: cleanup failed HTTP {status}; blob={blob}"
            )
            print(f"PASS: {container}: test blob deleted", flush=True)

print("PASS: Private access and read-only SAS checks completed.", flush=True)
