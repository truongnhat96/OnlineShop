"""Explicit Gemini live diagnostic: no DB; metadata only, never keys/provider text."""
import argparse
import json
import os
from pathlib import Path
import urllib.error
import urllib.request


def local_token(root):
    for path in (root / 'Infrastructure' / '.env', root / '.env'):
        if not path.is_file():
            continue
        for line in path.read_text(encoding='utf-8-sig').splitlines():
            line = line.strip().removeprefix('export ').lstrip()
            name, separator, value = line.partition('=')
            if not separator or name.strip() != 'GEMINI_API_KEY':
                continue
            value = value.strip()
            if value.startswith(('"', "'")):
                closing = value.find(value[0], 1)
                if closing < 0:
                    continue
                suffix = value[closing + 1:].lstrip()
                if suffix and not suffix.startswith('#'):
                    continue
                value = value[1:closing]
            else:
                value = value.split(' #', 1)[0].rstrip()
            if value:
                return value
    return None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--live', action='store_true', help='Send one minimal paid-capable inference request.')
    parser.add_argument('--model', default='gemini-3.5-flash-lite')
    args = parser.parse_args()
    if not args.live:
        parser.error('Refusing provider call without --live.')
    root = Path(__file__).resolve().parents[2]
    token = os.getenv('GEMINI_API_KEY') or local_token(root)
    if not token:
        print(json.dumps({'tokenLoaded': False}))
        return
    import re
    if not re.fullmatch(r'gemini-[A-Za-z0-9._-]{1,93}', args.model):
        parser.error('Invalid Gemini model id.')
    payload = {
        'contents': [{'role': 'user', 'parts': [{'text': 'Reply with exactly one word: OK.'}]}],
        'generationConfig': {'maxOutputTokens': 256, 'thinkingConfig': {'thinkingLevel': 'minimal', 'includeThoughts': False}},
    }
    request = urllib.request.Request('https://generativelanguage.googleapis.com/v1beta/models/' + args.model + ':generateContent',
        data=json.dumps(payload).encode(), headers={
            'x-goog-api-key': token, 'Content-Type': 'application/json',
        }, method='POST')
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, headers, newurl):
            return None
    try:
        with urllib.request.build_opener(NoRedirect()).open(request, timeout=45) as response:
            report(response.status, response.read(), args.model)
    except urllib.error.HTTPError as error:
        report(error.code, error.read(), args.model)
    except Exception as error:
        # Exceptions can contain URLs/headers: only print the type and socket errno.
        reason = getattr(error, 'reason', None)
        print(json.dumps({'networkError': type(error).__name__, 'reasonType': type(reason).__name__,
                          'errno': getattr(reason, 'errno', None)}))


def report(status, body, model):
    result = {'tokenLoaded': True, 'model': model, 'httpStatus': status}
    try:
        data = json.loads(body)
        result['responseKeys'] = list(data) if isinstance(data, dict) else type(data).__name__
        if isinstance(data, dict):
            choices = data.get('candidates') or []
            result['choiceCount'] = len(choices)
            if choices:
                parts = (choices[0].get('content') or {}).get('parts') or []
                result['contentNonEmpty'] = any(part.get('text') and not part.get('thought') for part in parts)
                finish = choices[0].get('finishReason')
                result['finishReason'] = finish if finish in ('STOP', 'MAX_TOKENS', 'SAFETY', 'RECITATION') else 'OTHER'
            error = data.get('error')
            if error:
                # Classify without printing any provider-supplied message or account information.
                google_status = error.get('status') if isinstance(error, dict) else None
                allowed = ('INVALID_ARGUMENT', 'FAILED_PRECONDITION', 'UNAUTHENTICATED', 'PERMISSION_DENIED', 'RESOURCE_EXHAUSTED', 'NOT_FOUND', 'UNAVAILABLE', 'INTERNAL')
                result['googleStatus'] = google_status if google_status in allowed else 'OTHER'
                result['errorType'] = type(error).__name__
    except (ValueError, AttributeError, TypeError, IndexError):
        result['invalidResponse'] = True
    print(json.dumps(result))


if __name__ == '__main__':
    main()
