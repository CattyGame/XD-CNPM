"""Read-only Sprint3 handoff contract/config checks; no Docker, credentials, HTTP or SQL."""
import copy
import hashlib
import json
import re
import sys
from pathlib import Path

import yaml
from jsonschema import Draft202012Validator, FormatChecker
from openapi_spec_validator import validate
from openapi_spec_validator.readers import read_from_filename

ROOT = Path(__file__).resolve().parents[2]
S3 = ROOT / 'infra/sprint3'


def need(condition, message):
    if not condition:
        raise ValueError(message)


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    handoff = read(S3 / 'handoff.json')
    need(handoff['integrationReady'] is False and handoff['deploymentAllowed'] is False,
         'Handoff must not claim deployment or integration acceptance')
    for name, expected in handoff['fileSha256'].items():
        path = (ROOT / name).resolve()
        need(path.is_relative_to(ROOT) and path.is_file(), 'Missing handoff file: ' + name)
        need(hashlib.sha256(path.read_bytes()).hexdigest() == expected, 'Changed file: ' + name)
    for name, expected in handoff['legacyFileSha256'].items():
        need(hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == expected,
             'Legacy baseline changed: ' + name)

    contracts = S3 / 'contracts'
    schema_count = api_count = example_count = negative_count = 0
    for path in contracts.glob('*.schema.json'):
        Draft202012Validator.check_schema(read(path))
        schema_count += 1
    for path in contracts.glob('*openapi*.yaml'):
        document, base_uri = read_from_filename(str(path))
        validate(document, base_uri=base_uri)
        api_count += 1

    def validator(name):
        return Draft202012Validator(read(contracts / name), format_checker=FormatChecker())

    event = validator('analysis-events-0.7.schema.json')
    job = validator('analysis-job-0.6.schema.json')
    examples = S3 / 'examples'
    for schema, names in [
        (event, ['event-requested', 'event-completed', 'event-failed-terminal']),
        (job, ['job-run']),
        (validator('ai-request-1.2.schema.json'), ['ai-request-shape']),
        (validator('ai-response-1.2.schema.json'), ['ai-response-shape']),
        (validator('settlement-resolution.schema.json'), ['settlement-capture', 'settlement-stale']),
        (validator('notification-context.schema.json'), ['notification-context']),
    ]:
        for name in names:
            schema.validate(read(examples / (name + '.json')))
            example_count += 1
    for filename in ['analysis-events-0.7.schema.json', 'analysis-job-0.6.schema.json']:
        need((ROOT / 'contracts' / filename).read_bytes() == (contracts / filename).read_bytes(),
             'Publisher contract differs from target: ' + filename)
    for filename in ['ai-request-1.2.schema.json', 'ai-response-1.2.schema.json', 'ai-error.schema.json', 'ai-openapi-1.2.yaml']:
        need(yaml.safe_load((ROOT / 'contracts' / filename).read_text()) == yaml.safe_load((contracts / filename).read_text()),
             'AI1.2 runtime contract differs from shared target: ' + filename)

    completed = read(examples / 'event-completed.json')
    failed = read(examples / 'event-failed-terminal.json')
    bad_cases = []
    bad = copy.deepcopy(completed); bad['data']['riskLevel'] = 'LOW'; bad_cases.append((event, bad))
    bad = copy.deepcopy(completed); bad['schemaVersion'] = '0.6'; bad_cases.append((event, bad))
    bad = copy.deepcopy(completed); bad['image_base64'] = 'forbidden'; bad_cases.append((event, bad))
    bad = copy.deepcopy(completed); del bad['tenantId']; bad_cases.append((event, bad))
    bad = copy.deepcopy(completed); bad['correlationId'] = 'unsafe\nheader'; bad_cases.append((event, bad))
    bad = copy.deepcopy(failed); bad['data']['retryable'] = True; bad_cases.append((event, bad))
    bad = read(examples / 'job-run.json'); bad['attempt'] = 0; bad_cases.append((job, bad))
    bad = read(examples / 'job-run.json'); bad['eventId'] = bad.pop('messageId'); bad_cases.append((job, bad))
    settlement = validator('settlement-resolution.schema.json')
    for key, value in [('isMock', True), ('applicable', False), ('processingStatus', 'PROCESSING'), ('reservationId', None)]:
        bad = read(examples / 'settlement-capture.json'); bad[key] = value; bad_cases.append((settlement, bad))
    for schema, bad in bad_cases:
        need(not schema.is_valid(bad), 'Invalid message/settlement accepted')
        negative_count += 1

    registry = read(ROOT / 'infra/service-auth/clients.sprint3.example.json')
    need(registry['issuer'] == 'aura-identity-s3-v1' and registry['algorithm'] == 'RS256'
         and registry['tokenLifetimeSeconds'] == 300 and len(registry['clients']) == 7, 'Registry contract differs')
    callers = {x['clientId']: x for x in registry['clients']}
    need(len(callers) == 7 and callers['analysis-worker']['allowedTargets']['aura-ai'] == ['ai:analyze'],
         'Worker AI scope differs')
    need(all('clientSecret' not in x and 'clientSecretHash' not in x for x in registry['clients']),
         'Public registry contains runtime material')
    need('aura-billing' not in callers['analysis-worker']['allowedTargets'], 'Worker unexpectedly owns Billing settlement')

    common = yaml.safe_load((S3 / 'common.reference.yaml').read_text())
    need(common['versions'] == handoff['versions'], 'Cross-service versions differ')
    need(not any(common['gates'][name] for name in ['workerEnabledByDefault', 'publishEnabledByDefault', 'newNamespaceReady']),
         'Reference runtime gates must stay closed')
    publisher = read(S3 / 'appsettings.publisher.example.json')
    need(publisher['Outbox']['Enabled'] is False and publisher['Outbox']['MinOccurredAtUtc'] is None,
         'Publisher example must fail closed until scope is issued')
    topology = read(S3 / 'rabbitmq.topology.reference.json')
    need(not topology.get('users') and len(topology['queues']) == 11 and len(topology['exchanges']) == 10,
         'Topology resource or secret scope differs')
    pub = next(x for x in topology['permissions'] if x['user'] == 's3-publisher')
    need(pub['configure'] == '^$' and pub['read'] == '^$'
         and all(re.fullmatch(pub['write'], name) for name in ['aura.s3.jobs', 'aura.s3.events'])
         and not re.fullmatch(pub['write'], 'aura.s3.retry.jobs'), 'Publisher permissions exceed scope')
    for binding in topology['bindings']:
        need(binding['source'] in {x['name'] for x in topology['exchanges']}
             and binding['destination'] in {x['name'] for x in topology['queues']}, 'Dangling topology binding')

    compose = yaml.safe_load((S3 / 'compose.auth-ai.example.yaml').read_text())
    need(set(compose['services']) == {'identity-api', 'ai-core'}
         and compose['networks']['backend']['internal'] is True, 'Compose profile scope differs')
    targets = {'identity-api': {'/run/aura-service-auth/issuer/identity-clients.json', '/run/aura-service-auth/issuer/signing-key.pem'},
               'ai-core': {'/run/aura-service-auth/public/signing-public.pem'}}
    for name, service in compose['services'].items():
        need(service['profiles'] == ['auth-ai-review'] and not service.get('ports') and not service.get('container_name'),
             'Compose may expose/start unintended resources')
        need(service['read_only'] is True and service['restart'] == 'no'
             and service['cap_drop'] == ['ALL'], 'Compose isolation differs')
        need({x['target'] for x in service['volumes']} == targets[name], 'Credential mount scope differs')
        need(all(x['read_only'] is True and x['type'] == 'bind' and x['bind']['create_host_path'] is False
                 for x in service['volumes']), 'Credential mounts must be read-only existing files')
        build = service['build']; context = (S3 / build['context']).resolve()
        need(context.is_relative_to(ROOT) and (context / build['dockerfile']).resolve().is_file(), 'Invalid build path')
    need(compose['services']['ai-core']['user'] == '1001:1001'
         and compose['services']['identity-api']['user'] == '1654:1654', 'Runtime users differ')
    for name, image in handoff['baseImages'].items():
        need(re.fullmatch(r'[^\s]+@sha256:[0-9a-f]{64}', image) is not None, 'Unpinned base: ' + name)
    for filename, kinds in [('ai-core.Dockerfile', ['python']), ('identity.Dockerfile', ['sdk', 'aspnet'])]:
        text = (S3 / 'docker' / filename).read_text()
        need(all(handoff['baseImages'][key] in text for key in kinds), 'Build base differs')
    need(read(S3 / 'docker/global.json')['sdk'] == {'version': '10.0.403', 'rollForward': 'disable'},
         'Container SDK pin differs')
    print(f'PASS Sprint3 handoff: schemas={schema_count}, OpenAPI={api_count}, examples={example_count}, negativeCases={negative_count}')
    print(f'PASS file bindings={len(handoff["fileSha256"])}, legacy files unchanged={len(handoff["legacyFileSha256"])}; gates closed; scoped mounts/topology')
    print('Read-only source/contract validation. Docker builds, PostgreSQL concurrency, broker and end-to-end integration are pending.')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print('FAIL Sprint3 handoff: ' + str(error), file=sys.stderr)
        sys.exit(1)
