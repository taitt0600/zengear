"""Tests of tools/structure_metrics.py on a small fake api/src tree.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools'))

import structure_metrics as metrics  # noqa: E402  (imported after the path is set)

SERVICES = metrics.SERVICES


def tree(root, files):
    for name in files:
        path = Path(root, name)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text('', encoding='utf-8')


def area(name, modules, files_per_module=1):
    return [f'{SERVICES}/{name}/Module{m}/Type{f}.cs' for m in range(modules) for f in range(files_per_module)]


class AreaSizesTest(unittest.TestCase):
    def sizes(self, files):
        with tempfile.TemporaryDirectory() as root:
            tree(root, files)
            return metrics.area_sizes(root)

    def test_counts_modules_and_files_including_mechanism_folders(self):
        sizes = self.sizes([
            f'{SERVICES}/Identity/Access/SignInService.cs',
            f'{SERVICES}/Identity/Access/AccessCache/RedisAccessCache.cs',
            f'{SERVICES}/Identity/Account/CurrentUserService.cs',
            f'{SERVICES}/Identity/README.md',
            f'{SERVICES}/Identity/Account/obj/Generated.cs',
        ])
        self.assertEqual(sizes, {'Identity': (2, 3)})

    def test_common_and_folders_without_code_are_not_counted(self):
        sizes = self.sizes([
            f'{SERVICES}/Common/Models/PagedDto.cs',
            f'{SERVICES}/Locations/BusinessLocations/BusinessLocationService.cs',
            f'{SERVICES}/Locations/Drafts/notes.md',
            f'{SERVICES}/DependencyInjection.cs',
        ])
        self.assertEqual(sizes, {'Locations': (1, 1)})

    def test_thresholds_are_above_8_modules_or_60_files(self):
        sizes = self.sizes(area('Eight', 8) + area('Nine', 9) + area('Sixty', 2, 30) + area('SixtyOne', 1, 61))
        self.assertEqual(metrics.over_limit(sizes), [('Nine', 9, 9), ('SixtyOne', 1, 61)])


class ModuleFolderTest(unittest.TestCase):
    def test_a_file_counts_as_its_module_or_its_own_folder(self):
        cases = {
            f'{SERVICES}/Identity/Access/AccessCache/RedisAccessCache.cs': f'{SERVICES}/Identity/Access',
            f'{SERVICES}/Identity/Access/SignInService.cs': f'{SERVICES}/Identity/Access',
            f'{SERVICES}/Identity/StaffService.cs': f'{SERVICES}/Identity',
            f'{SERVICES}/Common/Models/PagedDto.cs': f'{SERVICES}/Common/Models',
            'api/src/ZenGear.Api/Controllers/Identity/StaffController.cs': 'api/src/ZenGear.Api/Controllers/Identity',
        }
        for path, folder in cases.items():
            with self.subTest(path=path):
                self.assertEqual(metrics.module_folder(path), folder)

    def test_folders_touched_counts_a_module_once_and_skips_migrations(self):
        files = [
            f'{SERVICES}/Identity/Access/SignInService.cs',
            f'{SERVICES}/Identity/Access/AccessCache/RedisAccessCache.cs',
            'api/src/ZenGear.Api/Controllers/Identity/MeController.cs',
            'api/src/ZenGear.Data/Migrations/20261008115405_Phase1.cs',
        ]
        self.assertEqual(metrics.folders_touched(files), 2)


class FeatCommitsTest(unittest.TestCase):
    def test_range_lists_feat_commits_with_their_folders_at_module_level(self):
        with tempfile.TemporaryDirectory() as root:
            def commit(message, files):
                tree(root, files)
                subprocess.run(['git', '-C', root, 'add', '.'], check=True)
                subprocess.run(['git', '-C', root, '-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-qm', message],
                               check=True)

            subprocess.run(['git', 'init', '-q', root], check=True)
            commit('chore: start', ['README.md'])
            commit('feat(api): add the staff list', [
                f'{SERVICES}/Identity/StaffAccounts/StaffListRequest.cs',
                f'{SERVICES}/Identity/Access/AccessCache/CachedUser.cs',
                'api/src/ZenGear.Api/Controllers/Identity/StaffController.cs',
            ])
            commit('docs: describe it', [f'{SERVICES}/Identity/README.md'])

            lines = list(metrics.feat_lines(root, ['HEAD~2..HEAD']))

        self.assertEqual(len(lines), 1)
        self.assertEqual(lines[0][0], 3)
        self.assertIn('Identity', lines[0][1])
        self.assertIn('feat(api): add the staff list', lines[0][1])


if __name__ == '__main__':
    unittest.main()
