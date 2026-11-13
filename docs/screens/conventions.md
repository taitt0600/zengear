# Conventions for every screen

- **Text**: every label, message and button comes from `web/messages/vi.json` (next-intl). The texts quoted in the
  screen files are the Vietnamese wording for that file; terms of `docs/GLOSSARY.md` keep its Vietnamese label. The text of an
  error the API answers is the API's (`title`, `detail` and the `detail` of each `errors` item, from its vi-VN
  resources, DEC-117): the tables of the screens say only where it shows.
- **Access**: a staff screen needs a signed-in staff account; the permission of each screen is the one its endpoints
  require. Menus and buttons the user's permissions do not allow are hidden (`permissions` of `GET /api/v1/admin/me`,
  the user's own permission keys; never a role name), and the API still answers 403.
- **Errors** (DEC-117 (4)): a 400, or a 409 about one field, puts the `detail` of each `errors` item under the
  form field its `pointer` or `parameter` names and keeps what was typed; an item no field shows goes in an alert at
  the top of the form; any other problem shows its `detail` in a toast, or on the page when the page cannot show
  without it; a 500 or an answer that cannot be read shows "Đã có lỗi xảy ra. Vui lòng thử lại." and no answer
  "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại." (the web's only texts for an answer of the API);
  `ruleId` is never shown. 401 signs the user out to the sign-in page; 403
  `/problems/permission-denied` shows the no-permission page; 403 `/problems/recent-login-required` asks for the
  password again and retries once (DEC-094); 403 `/problems/password-change-required` (any call while the default
  password is unchanged) goes to SCR-AUT-02; 403 `/problems/account-disabled` (an account disabled while signed in,
  R-SEC-04) and 403 `/problems/account-not-registered` sign the user out to SCR-AUT-01, which shows the problem's
  `detail` once.
- **Edits with an ETag** (`If-Match`): the form keeps the `ETag` of the loaded resource; 412 (or 428) shows its
  `detail` in a toast and reloads the form.
- **Leaving a form**: an admin edit form does not warn before the user leaves it with changes not saved (a menu
  link, a reload, closing the tab); the changes are lost. Chosen in the web-only pull request of STF-03 for every
  admin form alike (SCR-LOC-01 has no warning either), instead of one screen with a warning.
- **States**: loading shows skeleton rows or fields, never an empty screen; an empty list says why it is empty; a
  button that sends a request is disabled until the answer comes, so a double click sends one request.
- **Grouping**, by the shadcn components, never content loose on the page ground: related content sits in a
  `Card` (a form, a list with its filters, a group of details); a page of one card (sign-in, password change) has
  its heading in the card's `CardTitle`, a page of several cards keeps its heading above them. An area with nothing
  to show (an empty list, no permission, not found, a failed load) is an `Empty` with an outline (`border`, dashed):
  an icon in `EmptyMedia variant="icon"`, the message in `EmptyTitle` (the page's h1 on a page that is only this
  Empty), the way out, when there is one, in `EmptyContent`; an empty list's Empty takes the place of the rows inside
  the list's card, so that its filters stay. Chosen by the owner on the Layout of SCR-ADM-01; a screen built before
  (SCR-ACC-01, the placeholder page) follows it with its next Layout.
- **Help on screen**, in every design and its Layout, from the message files like every text:
  - a **tooltip** (`Tooltip`) on every button that shows only an icon, with its `aria-label` (the corner close
    button of `Dialog` and `Sheet` too, with `ui.close`, in the pull request that next changes those components),
    and on every label cut short, with its full text; never the only place of a text a user needs (a tooltip does
    not show on touch); the first tooltip opens after a delay and the next ones at once (`TooltipProvider` with
    Radix's `delayDuration` 700 and `skipDelayDuration` 300, not shadcn's 0);
  - a **description** under the title of each card, dialog, sheet and Empty that says what it is for
    (`CardDescription`, `DialogDescription`, `SheetDescription`, `EmptyDescription`), except an Empty whose title
    is the text of an API problem;
  - a **hint text** (`FieldDescription`, tied to its input with `aria-describedby`) under every field with a
    length, a format or a use to explain; a field that is only required, or must match another field, needs none;
  - a **placeholder** in every field with a format (an email, a phone number, a code with a fixed pattern, an
    amount, a time `HH:mm`): an example value as it would be typed, without `…` (unlike the Web Interface
    Guidelines, since an ellipsis would read as part of the value), in `muted-foreground`, with a reserved example
    domain (`example.com`, RFC 2606) and never a real person or company; never instead of the label nor repeating
    it, and the hint never repeats its example; none in a password field, a field without a format, a read-only
    field, a select or a date picker, except the search box that filters a list, which has one, an example of a
    value it finds (chosen by the owner on the Layout of SCR-STF-01).

  A Layout lists these texts with the screen's other new texts. Chosen by the owner on the Layout of SCR-ADM-01,
  the placeholder on the Layout of SCR-AUT-01; a screen built before (SCR-AUT-01, SCR-AUT-02, SCR-ACC-01) follows
  it with its next Layout.
- **Dates** `yyyy-MM-dd` from the API are shown `dd/MM/yyyy`; timestamps are shown in Asia/Ho_Chi_Minh.
- **Accessibility**: WCAG 2.2 AA; every form works with the keyboard alone, every field has a visible label, errors
  are announced to screen readers.
- **Design system** (DEC-119 (2)): <https://claude.ai/artifact/DAYPEgxSLaHrHYG99Tmntb>, built 2026-10-31 from
  `web/src/app/globals.css` and `web/src/components/ui/` and updated 2026-11-13 to commit 563a162 (from 39485c9:
  comments only, nothing to sync); `/design-screen`
  brings the system up to date when those files changed since, and then the update's date and commit.
- **Layout** (DEC-119 (3)): the approved design of a screen, a section `## Layout` at the end of its file that
  `/design-screen <SCR-ID>` writes and the merge of its Layout pull request approves; a later change of the design is
  a new Layout pull request. The component tree is the part a diff can review, and the runs build from it:

  ````markdown
  ## Layout

  - **Design**: <link>, drawn <yyyy-mm-dd> with the design system of <yyyy-mm-dd>; approved by the merge of the
    pull request that wrote this section (DEC-119).
  - **Artboards**: desktop (1440 px), narrow (390 px); states: loading, empty, error, no permission.
    (Drawn in the light theme, with one dark artboard more when the screen has colours outside neutral, such as a
    badge or a destructive button, DEC-123 (5).)
  - **shadcn**: the `shadcn add` of the components and blocks the design needs (the tree names them too,
    `/design-screen` step 2), with where a block's files move (DEC-116 (3)), or "no `add`".
  - **Texts** (`messages/vi.json`), new ones marked *new*: each text of **Help on screen** with its key and where
    it goes (the description of each card, dialog, sheet and Empty, but an Empty whose title is an API problem; the
    hint and placeholder of each field that has them; the tooltip of each icon-only button, its `aria-label`, and
    of each label cut short), with the screen's other new texts.
  - **Component tree** (DEC-118 (3)), under `web/src/features/<area>/<module>/components/`:

    ```text
    <screen>-layout.tsx                slots: <a> | <b> | <c>; narrow: <what changes>
    ├── <screen>-<block>-section.tsx   <what it shows> — <shadcn component or block>; CardDescription <key>;
    │                                  loading: <…>; empty: Empty with EmptyTitle <key> and EmptyDescription <key>
    └── <screen>-<block>-section.tsx   … the field <label> with its FieldDescription <key> and placeholder <key>
        └── <piece>.tsx                presentational — <shadcn component>; its icon-only button in a Tooltip,
                                       its text <key> passed as a prop
    ```
  ````
