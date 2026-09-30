"""Temporary branch-only review publisher; removed from the published tree."""
from pathlib import Path
import base64
import gzip
import hashlib
import importlib.util
import json
import os
import re
import subprocess

DATA = '''H4sIAAAAAAAC/+19iXbbRpbor1TU54xIiQSxECQht/NG3hL3OLZHktPzJsqJC0CBQpsE2ABoW3H87+/eW1XYCC6ynZ4558ndsSUst6ruvlXh05HPc3F09unoPM9FkY8ugyxewb9Pw7hIs9GF4EERvxeXKxHBeOjuwsh87/gpeskTkLxkXnRxAxC3zCciT91HM4s05yMx9fJcDj8otlcJ6enp184pX//dzZ07cGEncLfUwa/rtb+Ig5YLvhChCxY8DxnW2GwMyZ/uE5Y/c9/roHZsyROE+NFmr67SAtewC89YM8AhMWI0uwDz8IB+0'''
# The authenticated connector fills the patch below in the next commit. This
# placeholder deliberately fails closed and never writes unverified content.
ROOT = Path(__file__).resolve().parents[1]
os.chdir(ROOT)
report = {'commands': []}


def run(args, expected=0, **kwargs):
    result = subprocess.run(args, text=True, capture_output=True, **kwargs)
    print('$ ' + ' '.join(args), flush=True)
    print(result.stdout + result.stderr, flush=True)
    report['commands'].append({'command': args, 'exit_code': result.returncode,
                               'stdout': result.stdout, 'stderr': result.stderr})
    if result.returncode != expected:
        raise RuntimeError('Unexpected exit code: ' + ' '.join(args))
    return result.stdout


def main():
    assert os.environ.get('GITHUB_REF_NAME') == 'feature/reactive-specimen-jar'
    raw = gzip.decompress(base64.b64decode(DATA, validate=True))
    assert hashlib.sha256(raw).hexdigest() == 'd49c4c9d62f1ef6e61f0de1d7fd39dde6bd2bd514dbffb5b3b52b614e164536a'
    payload = json.loads(raw)
    for name, expected in payload['base'].items():
        path = Path(name)
        assert not path.is_absolute() and '..' not in path.parts
        assert not name.startswith(('.github/', 'ProjectSettings/', 'Packages/'))
        assert (hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else None) == expected, name
    old_editor = Path('Assets/Scripts/Editor/ReactiveSpecimenJarEditor.cs').read_text()
    run(['git', 'apply', '--check', '-'], input=payload['patch'])
    run(['git', 'apply', '-'], input=payload['patch'])
    for name, expected in payload['hashes'].items():
        assert hashlib.sha256(Path(name).read_bytes()).hexdigest() == expected, name
    spec = importlib.util.spec_from_file_location('review_checker', ROOT/'Tools/validate_specimen_jar.py')
    checker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(checker)
    runtime = Path('Assets/Scripts/Toys/ReactiveSpecimenJar.cs').read_text()
    prefab = Path('Assets/RunawayChimps/Toys/ReactiveSpecimenJar/ReactiveSpecimenJar.prefab').read_text()
    before = checker.contracts(runtime, old_editor, prefab)
    assert len(before) >= 4, 'Pre-review authoring defects must fail the new contracts'
    report['pre_review_contract_failures'] = before
    print('EXPECTED pre-review failures: ' + '; '.join(before), flush=True)
    current_editor = Path('Assets/Scripts/Editor/ReactiveSpecimenJarEditor.cs').read_text()
    assert checker.contracts(runtime, current_editor.replace('if (!IsSceneInstance(handle)) return;', ''), prefab)
    report['second_pass_handle_regression_reproduced'] = True
    policy = run(['dotnet', 'run', '--project', 'Tools/SpecimenJarHarness/SpecimenJarHarness.csproj', '--configuration', 'Release'])
    cards = run(['dotnet', 'run', '--project', 'Tools/CardSystemHarness/CardSystemHarness.csproj', '--configuration', 'Release'])
    run(['python', 'Tools/validate_source.py', '--syntax'])
    for name in ['validate_repository_integrity', 'validate_level1_contracts', 'validate_spawn_floor_contracts',
                 'validate_keycard_reader_contracts', 'validate_interaction_safety', 'validate_pr15_review_hardening',
                 'validate_security_boot', 'validate_threat_feedback', 'validate_launch_presentation', 'validate_specimen_jar']:
        run(['python', f'Tools/{name}.py'])
    run(['python', '-m', 'compileall', '-q', 'Tools'])
    policy_count = int(re.search(r'PASS: (\d+) production specimen-policy assertions', policy)[1])
    card_count = int(re.search(r'PASS: (\d+) assertions', cards)[1])
    report['policy_assertions'] = policy_count
    report['card_assertions'] = card_count
    evidence = (f'The complete-checkout review run passed all existing repository source/syntax, metadata, '
                f'Level 1, spawn, keycard, interaction, PR15, boot, threat and launch checks, '
                f'{policy_count:,} production specimen-policy assertions and {card_count:,} existing card-state assertions, '
                'feature asset/negative-mutation checks and Python compilation. These are source/managed checks, '
                'not native Unity test execution. The final PR CI rerun and revision are recorded on '
                '[PR #78](https://github.com/GregStephen/RunawayChimps/pull/78).')
    for name in ['docs/design-and-lore.md', 'docs/repository-improvement-plan.md']:
        path = Path(name)
        text = path.read_text()
        assert text.count('REVIEW_RUN_EVIDENCE') == 1
        path.write_text(text.replace('REVIEW_RUN_EVIDENCE', evidence))
    paths = sorted(payload['hashes'])
    run(['git', 'add', '-f', '--', *paths])
    Path('Tools/_specimen_review.py').unlink()
    run(['git', 'add', '-u', '--', 'Tools/_specimen_review.py'])
    run(['git', 'diff', '--cached', '--check'])
    staged = set(run(['git', 'diff', '--cached', '--name-only']).splitlines())
    assert staged == set(paths + ['Tools/_specimen_review.py'])
    report['files'] = {name: {'sha256': hashlib.sha256(Path(name).read_bytes()).hexdigest(),
                            'blob_sha': subprocess.check_output(['git', 'hash-object', name], text=True).strip()}
                       for name in paths}
    run(['git', 'config', 'user.name', 'github-actions[bot]'])
    run(['git', 'config', 'user.email', '41898282+github-actions[bot]@users.noreply.github.com'])
    run(['git', 'commit', '-m', 'fix: make specimen preview authoring undoable and prefab-safe'])
    report['commit'] = run(['git', 'rev-parse', 'HEAD']).strip()
    run(['git', 'push', 'origin', 'HEAD:feature/reactive-specimen-jar'])


try:
    main()
finally:
    Path('/tmp/specimen-review-validation.json').write_text(json.dumps(report, indent=2))
