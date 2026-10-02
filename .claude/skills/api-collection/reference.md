# Postman collection format v3: what the ZenGear collection uses

The rules below are the part of the format the collection in `postman/` needs, so this skill does not depend on a
plugin. Sources, read on 2026-10-02 for Postman v12 and Postman CLI 1.69.0:

- Postman docs, *Postman Collections schemas* (schema 3.0.0, on-disk layout):
  https://learning.postman.com/latest-v-12/docs/use/use-collections/collections-schemas
- Postman docs, *Set up Native Git* and *Develop locally with Native Git*:
  https://learning.postman.com/latest-v-12/docs/use/native-git/setup,
  https://learning.postman.com/latest-v-12/docs/use/native-git/develop-locally
- Postman docs, *Collection commands* (`postman collection run`):
  https://learning.postman.com/docs/postman-cli/postman-cli-collections/
- The schema itself: https://schema.postman.com/

When a rule here and those pages disagree, the pages win: fix this file in the same pull request.

## Layout

```
postman/
  collections/ZenGear/                      the collection root: any folder can be one
    .resources/definition.yaml              collection metadata, variables, collection scripts
    <Feature ID>/                           one folder per feature
      .resources/definition.yaml            folder metadata (description, order)
      <NN> <what it checks>.request.yaml    one file per request
  environments/<name>.environment.yaml      dev, ci
  fixtures/<Feature ID>.sql, accounts.txt   dev-only data that tools/dev_reset.ps1 applies
```

- A request is a file `*.request.yaml`; a folder is a directory. Never put a request inside `.resources/`, which
  holds metadata only (and `<request-name>.resources/` beside a request, for its examples).
- `.yaml`, not `.yml`. Paths use forward slashes.
- File names: no `/ \ : * ? " < > |`; unique per directory, ignoring case. ZenGear starts each request name with a
  two-digit number so the files list in run order.

## `definition.yaml` (collection or folder)

```yaml
$kind: collection        # also for a folder
name: STF-01             # optional; defaults to the directory name
description: |-
  …
order: 1000              # folders: relative order
variables:               # optional: [{key, value, description?, disabled?}], values are strings
scripts:                 # optional
  - type: http:beforeRequest      # or http:afterResponse
    language: text/javascript
    code: |-
      …
```

## `*.request.yaml` (HTTP)

```yaml
$kind: http-request
description: |-
  R-SEC-01: a staff account holds at least one role.
method: POST                      # GET POST PUT PATCH DELETE HEAD OPTIONS
url: '{{baseUrl}}/api/v1/staff'
order: 6000                       # bare number, relative: space by 1000
headers:
  - key: X-ZenGear-As             # the account the collection script signs in (ZenGear convention)
    value: '{{adminEmail}}'
  - key: Content-Type
    value: 'application/json'
body:
  type: json                      # json, text, formdata, urlencoded, none, …
  content: |-
    { "roleIds": [] }
scripts:
  - type: beforeRequest           # request scripts: beforeRequest / afterResponse
    language: text/javascript
    code: |-
      …
  - type: afterResponse
    language: text/javascript
    code: |-
      pm.test('status 409', () => pm.response.to.have.status(409));
```

`name:` is needed only when the name cannot be the file name (for example it holds a `/`).

## Environment (`postman/environments/<name>.environment.yaml`)

```yaml
name: dev
values:
  - key: baseUrl
    value: 'http://localhost:5256'
    enabled: true
    type: default
```

Every `value` is a string. Shared values only: Postman's Native Git guide keeps secrets out of the repository, and
ZenGear needs none (sign-in uses emulator custom tokens).

## YAML rules that keep a file parsing

1. Single-quote every value that holds `{{variable}}`: `url: '{{baseUrl}}/api/v1/me'`.
2. Single-quote values with `: # & * ! [ ] { } > |`.
3. Bodies and scripts are `|-` block scalars, indented under the key.
4. Quote strings that look like booleans or numbers: `value: '123'`.
5. `order` is a bare number, never quoted.

## Running

`postman collection run postman/collections/ZenGear -e postman/environments/dev.environment.yaml [-i STF-01]
--no-report-events` runs from the files without signing in (a login or API key is needed only for a collection ID, to
send results to Postman, or for `--output`, which 1.69.0 refuses without a login). Exit code 1 when a test fails.
