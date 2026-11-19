# Review rounds (every change, not only features)

The procedure of the reviewers that the root `CLAUDE.md` names under *Reviewers on every change, not only features*
(DEC-152 (3)). `/implement-feature`, `/finish-feature`, `/fix-defect`, `/design-screen`, a change outside a skill run
and a cloud session reviewing its own pull request follow it; the rules (the three tiers, which reviewer reviews what,
posting the verdict, the round limit, the full review before a `pass`) stay in `CLAUDE.md`, and the three sentences
of the first item that say them are repeated here only so that the item reads whole; a change to them is made in both
files. This file is the one place for how the rounds run; the skills cite it.

- `code-reviewer`, at most three rounds (DEC-112 (1); two in `/design-screen`), each call one round, also in the line
  of issue #24. Round 1 reviews the full diff of the branch. A later round is incremental: the fixes since the last
  round and each Blocking item it left. So that the fixes can be told apart, when a round returns its verdict the
  session stages the tree (`git add -A`; `/design-screen` stages only `docs/screens/`) before it starts the fixes, so
  that the index is what that round saw and the next round reviews `git diff` and the untracked files; once the rounds
  end, `git reset -q` before the work is split into commits (`/design-screen` stages `docs/screens/` again instead,
  before its commit). A round after commits were made since the last one is a full review. Before a `pass` is posted,
  the head has had a full review (opus, high): the last allowed round is always one, and an earlier round that answers
  READY after an incremental review is followed by one; after a later push (a fix of CI, a defect, a rebase) the round
  is a full review of the head. Blocking items left by the last allowed round are Needs decision items. The model is
  changed to sonnet for a call only when every file of the diff is `docs/ACCEPTANCE.md` or the root `README.md`.
- `plan-reviewer` gets the dossier and the files the plan changes, and stops at its `maxTurns`, where Claude Code
  returns its output marked partial and it can be resumed (code.claude.com/docs/en/sub-agents, field `maxTurns`); a
  partial output without a verdict is resumed once (SendMessage), and still without one the plan is reported as not
  reviewed ("chưa rà").
