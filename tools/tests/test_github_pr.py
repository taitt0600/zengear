"""Tests of tools/github_pr.py: every check that auto-merge requires of main is the name of a job of a workflow in
.github/workflows/, so that renaming a job cannot leave auto-merge waiting for a check that never runs; and every
job is named after its id.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import re
import sys
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools'))
from github_pr import REQUIRED_CHECKS  # noqa: E402

# The name of a job: "name:" indented by four spaces under "jobs:" (the workflows are indented by two spaces).
JOB_NAME = re.compile(r'^    name: *(?P<quote>[\'"]?)(?P<name>.+?)(?P=quote) *$', re.MULTILINE)
# A job id ("  <id>:") followed at once by its name; every job of the repository is written this way.
JOB = re.compile(r'^  (?P<id>[A-Za-z0-9_-]+):\n    name: *(?P<quote>[\'"]?)(?P<name>.+?)(?P=quote) *$', re.MULTILINE)


def job_names():
    names = {}
    for workflow in sorted((REPO / '.github' / 'workflows').glob('*.yml')):
        text = workflow.read_text(encoding='utf-8')
        jobs = text.split('\njobs:\n', 1)
        for match in JOB_NAME.finditer(jobs[1] if len(jobs) == 2 else ''):
            names.setdefault(match['name'], []).append(workflow.name)
    return names


class RequiredChecksTests(unittest.TestCase):
    def test_every_required_check_is_a_job_of_a_workflow(self):
        names = job_names()
        for check in REQUIRED_CHECKS:
            with self.subTest(check=check):
                self.assertIn(check, names, f'no job of .github/workflows/ is named "{check}"')

    def test_every_job_is_named_after_its_id(self):
        for workflow in sorted((REPO / '.github' / 'workflows').glob('*.yml')):
            jobs = workflow.read_text(encoding='utf-8').split('\njobs:\n', 1)
            for match in JOB.finditer(jobs[1] if len(jobs) == 2 else ''):
                with self.subTest(workflow=workflow.name, job=match['id']):
                    self.assertEqual(match['name'], match['id'])

    def test_ci_ok_and_the_gates_run_where_the_docstring_says(self):
        names = job_names()
        self.assertEqual(names.get('ci-ok'), ['pipeline.yml'])
        self.assertEqual(names.get('pr-gates'), ['gates.yml'])
        self.assertEqual(names.get('pr-title'), ['gates.yml'])


if __name__ == '__main__':
    unittest.main()
