"""An ID token of the Firebase Auth Emulator, for calling the API by hand on the development environment (docs/ACCEPTANCE.md).

  python tools/emulator_token.py <email> <password> [--create]

Signs in with email and password (--create signs up first), prints the Firebase uid on stderr and the ID token on
stdout, so that TOKEN=$(python tools/emulator_token.py ...) works. Only the emulator is called:
FIREBASE_AUTH_EMULATOR_HOST (default localhost:9099) must be a loopback or private address."""
import argparse, ipaddress, json, os, socket, sys, urllib.error, urllib.request

API_KEY = 'emulator'  # the emulator accepts any key; a real project would need its own


def emulator_host():
    host = os.environ.get('FIREBASE_AUTH_EMULATOR_HOST', 'localhost:9099')
    name = host.rsplit(':', 1)[0].strip('[]')
    try:
        addresses = {info[4][0] for info in socket.getaddrinfo(name, None)}
    except socket.gaierror:
        raise SystemExit(f'cannot resolve the emulator host {name}') from None
    local = [ipaddress.ip_address(address.split('%')[0]) for address in addresses]
    if not all(address.is_loopback or address.is_private for address in local):
        raise SystemExit(f'{host} is not a local emulator: only the Firebase Auth Emulator is called')
    return host


def call(host, method, body):
    url = f'http://{host}/identitytoolkit.googleapis.com/v1/accounts:{method}?key={API_KEY}'
    request = urllib.request.Request(url, data=json.dumps(body).encode(), method='POST',
                                     headers={'Content-Type': 'application/json'})
    # The emulator is local: no proxy, even when HTTPS_PROXY or HTTP_PROXY is set.
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    try:
        with opener.open(request, timeout=10) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        message = json.load(error).get('error', {}).get('message', error.reason)
        raise SystemExit(f'{method}: {message}') from None
    except urllib.error.URLError as error:
        raise SystemExit(f'the emulator at {host} does not answer ({error.reason}): '
                         'are the services of api/docker-compose.yml up?') from None


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('email')
    parser.add_argument('password')
    parser.add_argument('--create', action='store_true', help='sign up the account first')
    args = parser.parse_args()

    host = emulator_host()
    credentials = {'email': args.email, 'password': args.password, 'returnSecureToken': True}
    result = call(host, 'signUp' if args.create else 'signInWithPassword', credentials)
    print(f'uid: {result["localId"]}', file=sys.stderr)
    print(result['idToken'])


if __name__ == '__main__':
    main()
