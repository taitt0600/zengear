"""The GitHub REST and GraphQL client of tools/ (github_pr.py, pr_gates.py, issue.py, feature_context.py).

The token comes from GH_TOKEN, GITHUB_TOKEN or `gh auth token`; in a cloud session the GitHub proxy adds the credential
(GH_TOKEN reads "proxy-injected") and serves no GraphQL. The repository is GITHUB_REPOSITORY in a workflow, else the
origin remote. Reads are retried; a write is sent once, since it may have been applied before the error.
"""
import json, os, re, subprocess, time, urllib.error, urllib.parse, urllib.request

API = 'https://api.github.com'
PROXY_TOKEN = 'proxy-injected'
ATTEMPTS = 3
API_VERSION = '2022-11-28'


class GitHubError(Exception):
    exit_code = 1

    def __init__(self, message, status=None):
        super().__init__(message)
        self.status = status


class Unreachable(GitHubError):
    exit_code = 4


def run(*args):
    return subprocess.run(args, capture_output=True, text=True, encoding='utf-8', check=True).stdout.strip()


def repository_name():
    if os.environ.get('GITHUB_REPOSITORY'):
        return os.environ['GITHUB_REPOSITORY']
    url = run('git', 'remote', 'get-url', 'origin')
    match = re.search(r'[/:]([^/:]+)/([^/]+?)(?:\.git)?/?$', url)
    if not match:
        raise GitHubError(f'origin is not a GitHub repository: {url}')
    return f'{match[1]}/{match[2]}'


class GitHub:
    def __init__(self):
        self.token = os.environ.get('GH_TOKEN') or os.environ.get('GITHUB_TOKEN') or self._gh_token()
        self.proxy = self.token == PROXY_TOKEN
        self.repo = repository_name()
        self.owner = self.repo.split('/')[0]

    @staticmethod
    def _gh_token():
        try:
            return run('gh', 'auth', 'token')
        except (OSError, subprocess.CalledProcessError):
            raise GitHubError('No GitHub token: set GH_TOKEN, or sign in with `gh auth login`.') from None

    def call(self, method, path, body=None):
        data = None if body is None else json.dumps(body).encode()
        headers = {'Authorization': f'Bearer {self.token}', 'Accept': 'application/vnd.github+json',
                   'X-GitHub-Api-Version': API_VERSION, 'User-Agent': 'zengear-tools'}
        if data is not None:
            headers['Content-Type'] = 'application/json'
        attempts = ATTEMPTS if method == 'GET' else 1
        for attempt in range(1, attempts + 1):
            request = urllib.request.Request(f'{API}{path}', data=data, method=method, headers=headers)
            try:
                with urllib.request.urlopen(request, timeout=60) as response:
                    text = response.read().decode()
                return json.loads(text) if text else None
            except urllib.error.HTTPError as error:
                detail = error.read().decode(errors='replace')[:500]
                if error.code < 500 or attempt == attempts:
                    kind = Unreachable if error.code >= 500 else GitHubError
                    raise kind(f'{method} {path}: HTTP {error.code} {detail}', error.code) from None
            except (urllib.error.URLError, OSError) as error:
                if attempt == attempts:
                    raise Unreachable(f'{method} {path}: {error}') from None
            time.sleep(5 * attempt)

    def pages(self, path, limit=1000):
        """Every item of a paginated GET that returns a list, up to limit."""
        items, page = [], 1
        separator = '&' if '?' in path else '?'
        while len(items) < limit:
            batch = self.call('GET', f'{path}{separator}per_page=100&page={page}')
            items += batch
            if len(batch) < 100:
                break
            page += 1
        return items[:limit]

    def graphql(self, query, variables):
        if self.proxy:
            raise GitHubError('GraphQL is not served in a cloud session: use the GitHub MCP there')
        result = self.call('POST', '/graphql', {'query': query, 'variables': variables})
        if result.get('errors'):
            raise GitHubError('GraphQL: ' + '; '.join(error['message'] for error in result['errors']))
        return result['data']

    def pull(self, number):
        return self.call('GET', f'/repos/{self.repo}/pulls/{number}')

    def issue(self, number):
        return self.call('GET', f'/repos/{self.repo}/issues/{number}')


def quote(value):
    return urllib.parse.quote(str(value), safe='')
