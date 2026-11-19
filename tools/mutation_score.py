"""The mutation score of the code the tests cover, from a Stryker JSON report (DEC-152 (7)).

  python tools/mutation_score.py <mutation-report.json> --break <0-100>

The jobs quality-api and quality-web run Stryker only on the code a pull request changed. Most services are tested by
the integration tests of ZenGear.Api.IntegrationTests, and some web files only by the screen flows, which mutation
testing cannot run (one run of the suite per mutant), so their mutants are NoCoverage for the unit tests. Stryker's
own break counts NoCoverage as undetected and would fail such a pull request with no defect behind it; neither
Stryker.NET nor StrykerJS has a threshold on the covered code alone. This script reads the report Stryker wrote (the
mutation-testing-elements schema, statuses Killed, Survived, Timeout, NoCoverage, CompileError, RuntimeError,
Ignored) and fails only when the score of the covered mutants, (Killed + Timeout) / (Killed + Timeout + Survived) in
percent, is below the break of api/stryker-config.json or web/stryker.config.json, which the job passes. NoCoverage
mutants are listed per file, not counted. No covered mutant passes. The whole projects keep Stryker's own break in
weekly-quality.yml.

The summary goes to stdout and, in GitHub Actions, to the job summary ($GITHUB_STEP_SUMMARY). Exit 0 at or above the
break, 1 below it, 2 for a report that cannot be read.
"""
import argparse
import json
import os
import sys
from collections import Counter

DETECTED = ('Killed', 'Timeout')


def counts(report):
    """(Counter of the statuses, Counter of the NoCoverage mutants per file)."""
    statuses, uncovered = Counter(), Counter()
    for path, data in sorted(report['files'].items()):
        for mutant in data.get('mutants', []):
            statuses[mutant['status']] += 1
            if mutant['status'] == 'NoCoverage':
                uncovered[os.path.relpath(path, report.get('projectRoot') or '.') if os.path.isabs(path) else path] += 1
    return statuses, uncovered


def covered_score(statuses):
    """The score of the covered mutants in percent, or None when no mutant was covered."""
    detected = sum(statuses[s] for s in DETECTED)
    covered = detected + statuses['Survived']
    return None if covered == 0 else 100.0 * detected / covered


def summary(statuses, uncovered, score, threshold):
    lines = ['### Mutation score of the covered code (DEC-152 (7))', '',
             f'Killed {statuses["Killed"]}, timeout {statuses["Timeout"]}, survived {statuses["Survived"]}, '
             f'no coverage {statuses["NoCoverage"]}, compile error {statuses["CompileError"]}.', '']
    if score is None:
        lines.append(f'No changed mutant is covered by the unit tests: nothing to judge (break {threshold}).')
    else:
        verdict = 'passes' if score >= threshold else 'is below'
        lines.append(f'Covered score {score:.2f} % {verdict} the break of {threshold} %.')
    if uncovered:
        lines += ['', 'Not covered by the unit tests (integration tests and screen flows are not mutation tested):']
        lines += [f'- `{path}`: {count}' for path, count in sorted(uncovered.items())]
    return '\n'.join(lines) + '\n'


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('report')
    parser.add_argument('--break', dest='threshold', type=float, required=True)
    args = parser.parse_args(argv)
    try:
        with open(args.report, encoding='utf-8') as file:
            report = json.load(file)
        statuses, uncovered = counts(report)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f'::error::the mutation report {args.report} cannot be read: {error}')
        return 2
    score = covered_score(statuses)
    text = summary(statuses, uncovered, score, args.threshold)
    print(text, end='')
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as file:
            file.write(text)
    if score is not None and score < args.threshold:
        print(f'::error::the covered mutation score {score:.2f} % is below the break of {args.threshold} %')
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
