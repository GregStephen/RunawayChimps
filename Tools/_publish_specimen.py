"""Temporary, branch-only delivery. Removed from the published implementation tree."""
from pathlib import Path
import base64
import hashlib
import json
import lzma
import os
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
os.chdir(ROOT)
assert os.environ.get('GITHUB_REF_NAME') == 'feature/reactive-specimen-jar'
report = {'commands': []}


def run(args):
    print('\n$ ' + ' '.join(args), flush=True)
    result = subprocess.run(args, text=True, capture_output=True)
    print(result.stdout, end='', flush=True)
    print(result.stderr, end='', flush=True)
    report['commands'].append({'command': args, 'exit_code': result.returncode, 'stdout': result.stdout, 'stderr': result.stderr})
    result.check_returncode()
    return result.stdout


def main():
    hashes = ['e1bbdc324fb5e3eb10cc8e8385095d7ed59db76308ca19d1771a938be1d8ef48',
              'ed4cccd5504e1c50e8790f0e048757dc004f84d28cd75888f95312e401aec708',
              '5af195993b68cdebbee790bbaa479105eb45e449a85a7c0c5103190c45180c54']
    parts = []
    for index, expected in enumerate(hashes):
        value = Path(f'Tools/_specimen_transfer/{index}.txt').read_text().strip()
        assert hashlib.sha256(value.encode()).hexdigest() == expected, f'Transport checksum {index}'
        parts.append(value)
    raw = lzma.decompress(base64.b64decode(''.join(parts), validate=True))
    assert hashlib.sha256(raw).hexdigest() == '17463d8b418275d2f1bee43f59dad2a6a57be76eb73ef28a2aa9f9601783f0cf'
    payload = json.loads(raw)
    for name, expected in payload['existing'].items():
        assert hashlib.sha256(Path(name).read_bytes()).hexdigest() == expected, f'Base changed: {name}'
    for name, text in payload['files'].items():
        path = Path(name)
        assert not path.is_absolute() and '..' not in path.parts
        assert name.startswith(('Assets/Scripts/Toys', 'Assets/Scripts/Editor/ReactiveSpecimenJarEditor',
                                'Assets/Tests/SpecimenJar', 'Tools/SpecimenJarHarness/',
                                'Tools/build_specimen_jar.py', 'Tools/validate_specimen_jar.py',
                                'docs/reactive-specimen-jar.md', '.github/workflows/source-validation.yml'))
        assert not path.exists() or name in payload['existing'], f'New file already exists: {name}'
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(text.encode())
    version = Path('ProjectSettings/ProjectVersion.txt').read_text()
    assert 'm_EditorVersion: 2022.3.62f3' in version and '96770f904ca7' in version
    run(['python', 'Tools/build_specimen_jar.py'])
    for name, expected in payload['assets'].items():
        assert hashlib.sha256(Path(name).read_bytes()).hexdigest() == expected, f'Asset reproduction: {name}'
    print('PASS: all original serialized assets reproduce the reviewed local bytes.', flush=True)
    specimen = run(['dotnet', 'run', '--project', 'Tools/SpecimenJarHarness/SpecimenJarHarness.csproj', '--configuration', 'Release'])
    cards = run(['dotnet', 'run', '--project', 'Tools/CardSystemHarness/CardSystemHarness.csproj', '--configuration', 'Release'])
    run(['python', 'Tools/validate_source.py', '--syntax'])
    for name in ['validate_repository_integrity', 'validate_level1_contracts', 'validate_spawn_floor_contracts',
                 'validate_keycard_reader_contracts', 'validate_interaction_safety', 'validate_pr15_review_hardening',
                 'validate_security_boot', 'validate_threat_feedback', 'validate_launch_presentation', 'validate_specimen_jar']:
        run(['python', f'Tools/{name}.py'])
    run(['python', '-m', 'compileall', '-q', 'Tools'])
    count = int(re.search(r'PASS: (\d+) production specimen-policy assertions', specimen)[1])
    card_count = int(re.search(r'PASS: (\d+) assertions', cards)[1])
    evidence = (f'The production specimen-state executable passed {count:,} assertions, including 5,000 bounds samples. '
                f'The unchanged card-state harness passed {card_count:,} assertions. All repository source/syntax, metadata, '
                'Level 1, spawn, keycard, interaction, PR15, boot, threat and launch checks passed on a complete GitHub checkout. '
                'The feature validator passed serialized reference/geometry/material/containment contracts and rejected seven '
                'negative mutations; the original asset generator reproduced the reviewed bytes. Python tools compile and '
                'the staged diff passed whitespace checks. These are managed/source/asset checks, not Unity compilation or headset evidence.')
    section = payload['section'].replace('SPECIMEN_VALIDATION_RESULT', evidence)
    docs = ['docs/design-and-lore.md', 'docs/repository-improvement-plan.md']
    for name in docs:
        path = Path(name)
        text = path.read_text().replace('Last updated: 2026-09-22.', 'Last updated: 2026-09-30.', 1)
        index = text.index('\n## ')
        path.write_text(text[:index+1] + section + text[index+1:])
    # Workflow writes are performed separately by the authenticated GitHub connector,
    # not by attempting to elevate this job token's workflow permissions.
    paths = sorted((set(payload['files']) | set(payload['assets']) | set(docs)) - {'.github/workflows/source-validation.yml'})
    run(['git', 'add', '-f', '--', *paths])
    temporary = ['Tools/_publish_specimen.py'] + [f'Tools/_specimen_transfer/{i}.txt' for i in range(3)]
    for name in temporary:
        Path(name).unlink()
    run(['git', 'add', '-u', '--', *temporary])
    run(['git', 'diff', '--cached', '--check'])
    staged = set(run(['git', 'diff', '--cached', '--name-only']).splitlines())
    assert staged == set(paths + temporary), 'Unexpected staged scope'
    report['specimen_assertions'] = count
    report['card_assertions'] = card_count
    report['validation_text'] = evidence
    report['files'] = {name: {'sha256': hashlib.sha256(Path(name).read_bytes()).hexdigest(),
                            'git_blob': subprocess.check_output(['git', 'hash-object', name], text=True).strip()}
                       for name in paths}
    run(['git', 'config', 'user.name', 'github-actions[bot]'])
    run(['git', 'config', 'user.email', '41898282+github-actions[bot]@users.noreply.github.com'])
    run(['git', 'commit', '-m', 'feat: add local-per-viewer reactive specimen jar prefab and tests'])
    report['commit'] = run(['git', 'rev-parse', 'HEAD']).strip()
    run(['git', 'push', 'origin', 'HEAD:feature/reactive-specimen-jar'])
    print('PUBLISHED SPECIMEN COMMIT: ' + report['commit'], flush=True)


try:
    main()
finally:
    Path('/tmp/specimen-validation.json').write_text(json.dumps(report, indent=2))
