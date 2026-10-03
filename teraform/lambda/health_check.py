import json
import os
import urllib.request


def handler(_event, _context):
    url = os.environ["HEALTH_CHECK_URL"]
    request = urllib.request.Request(url, headers={"User-Agent": "SecureShare-AWS-HealthCheck/1.0"})
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            status = response.status
        if status != 200:
            raise RuntimeError(f"Unexpected status {status}")
        result = {"url": url, "status": status, "healthy": True}
        print(json.dumps(result))
        return result
    except Exception as error:
        print(json.dumps({"url": url, "healthy": False, "error": str(error)}))
        raise
