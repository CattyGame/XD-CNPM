import base64
import os
import hashlib
import hmac
import urllib.request
import urllib.error
import xml.etree.ElementTree as ET
from email.utils import formatdate

account = "devstoreaccount1"
key = base64.b64decode(
    "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="
)
endpoint = os.environ.get(
    "AURA_STORAGE_TEST_ENDPOINT",
    "http://azurite:10000",
).rstrip("/")

containers = ["aura-originals", "aura-annotations", "aura-reports"]

def request(method, container, query):
    path = f"/{account}/{container}"
    headers = {
        "x-ms-date": formatdate(usegmt=True),
        "x-ms-version": "2023-11-03",
    }

    canonical_headers = "".join(
        f"{name}:{headers[name]}\n" for name in sorted(headers)
    )
    canonical_resource = f"/{account}{path}" + "".join(
        f"\n{name}:{query[name]}" for name in sorted(query)
    )

    # Content-Length 0 is represented by an empty string in the signature.
    string_to_sign = (
        "\n".join([method] + [""] * 11)
        + "\n"
        + canonical_headers
        + canonical_resource
    )

    signature = base64.b64encode(
        hmac.new(key, string_to_sign.encode("utf-8"), hashlib.sha256).digest()
    ).decode("ascii")

    headers["Authorization"] = f"SharedKey {account}:{signature}"
    if method == "PUT":
        headers["Content-Length"] = "0"

    query_string = "&".join(
        f"{name}={query[name]}" for name in sorted(query)
    )
    req = urllib.request.Request(
        f"{endpoint}{path}?{query_string}",
        headers=headers,
        method=method,
    )

    try:
        with urllib.request.urlopen(req, timeout=15) as response:
            return response.status, response.headers, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.headers, error.read()

def error_code(body):
    try:
        return ET.fromstring(body).findtext("Code") or "Unknown"
    except ET.ParseError:
        return "Unknown"

for container in containers:
    status, headers, body = request(
        "PUT", container, {"restype": "container"}
    )

    if status == 201:
        print(f"CREATED: {container}", flush=True)
    elif status == 409 and error_code(body) == "ContainerAlreadyExists":
        print(f"EXISTS: {container}; giu nguyen du lieu", flush=True)
    else:
        raise RuntimeError(
            f"{container}: create failed, HTTP {status}, code={error_code(body)}"
        )

    status, headers, body = request(
        "GET", container, {"restype": "container", "comp": "acl"}
    )

    if status != 200:
        raise RuntimeError(
            f"{container}: ACL check failed, HTTP {status}, code={error_code(body)}"
        )

    public_access = headers.get("x-ms-blob-public-access")
    if public_access:
        raise RuntimeError(
            f"{container}: public access is {public_access}; stop for review"
        )

    print(f"PASS: {container} private (anonymous public access disabled)", flush=True)

print("PASS: Ca 3 container ton tai va khong bat public access.", flush=True)
